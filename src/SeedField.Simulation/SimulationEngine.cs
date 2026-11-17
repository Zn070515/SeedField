namespace SeedField.Simulation;

/// <summary>
/// The single authority for world mutation. Pipeline per tick:
/// intents (stable order) -> advance tick -> deliver due messages -> regenerate cores.
/// A message sent on tick T is available at the earliest in the observation for tick
/// T+1, never in the same tick.
/// </summary>
public sealed class SimulationEngine
{
    private readonly HashSet<string> _inactiveRegions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _inactiveSince = new(StringComparer.Ordinal);

    public SimulationEngine(WorldState world)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
    }

    public WorldState World { get; }

    /// <summary>
    /// Active-region gating is a session concern, not world state. Inactive regions
    /// still run passive systems (power generation, deliveries, core regeneration);
    /// only controller intents are rejected there.
    /// </summary>
    public bool IsRegionActive(string regionId) => !_inactiveRegions.Contains(regionId);

    public void DeactivateRegion(string regionId)
    {
        if (!World.Regions.ContainsKey(regionId))
        {
            throw new ArgumentException($"Unknown region '{regionId}'.", nameof(regionId));
        }

        if (_inactiveRegions.Add(regionId))
        {
            _inactiveSince[regionId] = World.Tick;
        }
    }

    public void ActivateRegion(string regionId)
    {
        if (!_inactiveRegions.Remove(regionId))
        {
            return;
        }

        if (_inactiveSince.TryGetValue(regionId, out var since))
        {
            CatchUpPower(regionId, Math.Max(0, World.Tick - since));
            _inactiveSince.Remove(regionId);
        }
    }

    public void CatchUpInactiveRegions()
    {
        foreach (var regionId in _inactiveRegions.ToList())
        {
            ActivateRegion(regionId);
        }
    }

    public void SetActiveRegions(IEnumerable<string> activeRegionIds)
    {
        var active = new HashSet<string>(activeRegionIds, StringComparer.Ordinal);
        foreach (var regionId in active)
        {
            if (!World.Regions.ContainsKey(regionId))
            {
                throw new ArgumentException($"Unknown region '{regionId}'.", nameof(activeRegionIds));
            }
        }

        foreach (var regionId in _inactiveRegions.ToList())
        {
            if (active.Contains(regionId))
            {
                ActivateRegion(regionId);
            }
        }

        foreach (var regionId in World.Regions.Keys)
        {
            if (!active.Contains(regionId) && !_inactiveRegions.Contains(regionId))
            {
                DeactivateRegion(regionId);
            }
        }
    }

    private void CatchUpPower(string regionId, long ticks)
    {
        if (ticks <= 0 || !World.Regions.TryGetValue(regionId, out var region))
        {
            return;
        }

        // Bound catch-up work for extremely long offscreen periods; the cap is a
        // prototype safety value, not a final production rule.
        ticks = Math.Min(ticks, 1_000_000);

        foreach (var entity in region.Entities.Values)
        {
            if (entity.Power is null)
            {
                continue;
            }

            var room = Math.Max(0, entity.EnergyCapacity - entity.Energy);
            var perTick = Math.Max(0L, entity.Power.EnergyPerTick);
            var generated = perTick * ticks;
            var toLocal = Math.Min(generated, room);
            var overflow = generated - toLocal;
            if (toLocal > 0)
            {
                entity.Energy += toLocal;
            }

            if (overflow > 0)
            {
                region.AddEnergy(entity.CivilizationId, overflow);
                World.Statistics.EnergyOverflowedTotal += overflow;
            }

            World.Statistics.EnergyGeneratedTotal += generated;
        }
    }

    public Observation BuildObservation(string civilizationId) =>
        ObservationBuilder.Build(World, civilizationId);

    public Observation BuildDeviceObservation(EntityId entityId) =>
        ObservationBuilder.BuildForEntity(World, entityId);

    public TickResult Step(IReadOnlyList<Intent>? intents = null)
    {
        var tick = World.Tick;
        var events = new List<SimulationEvent>();
        var results = new List<IntentResult>();
        var computeUsed = new Dictionary<EntityId, int>();
        var bandwidthUsed = new Dictionary<EntityId, int>();
        var linkUsage = new Dictionary<string, int>(StringComparer.Ordinal);
        var materialLinkUsage = new Dictionary<string, int>(StringComparer.Ordinal);
        var energyLinkUsage = new Dictionary<string, int>(StringComparer.Ordinal);

        GeneratePower();
        FlowPower(tick, events);

        if (intents is { Count: > 0 })
        {
            // Entity ids are globally unique and monotonic. Sorting by id gives a
            // canonical order regardless of how controllers were concatenated.
            foreach (var intent in intents.OrderBy(intent => intent.EntityId.Value))
            {
                ProcessIntent(
                    intent,
                    tick,
                    events,
                    results,
                    computeUsed,
                    bandwidthUsed,
                    linkUsage,
                    materialLinkUsage,
                    energyLinkUsage);
            }
        }

        EvaluateControlPoints(tick, events);

        World.Tick = tick + 1;

        ExpireDiplomacy(World.Tick, events);
        DeliverDueMessages(World.Tick, events);
        DeliverDueMaterialShipments(World.Tick, events);
        DeliverDueEnergyShipments(World.Tick, events);
        DeliverDuePowerShipments(World.Tick, events);
        DeliverDueConstructions(World.Tick, events);
        RegenerateCores(World.Tick, events);

        return new TickResult(tick, events, results);
    }

    /// <summary>
    /// Rebuilds a minimum entity from the permanent core reserve. Intended as the
    /// scripted proof of the core-recovery invariant, not as a normal build UI.
    /// </summary>
    public BootstrapResult TryBootstrapFromCore(
        string civilizationId,
        RecoveryBlueprint blueprint,
        Vec2 position)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ValidateBlueprint(blueprint);

        if (!World.Cores.TryGetValue(civilizationId, out var core))
        {
            return BootstrapFailure(SimulationErrorCode.CoreNotAvailable, "No seed core for civilization.");
        }

        if (!World.Regions.TryGetValue(core.RegionId, out var region) || !region.IsCoreRegion)
        {
            return BootstrapFailure(SimulationErrorCode.CoreNotAvailable, "Core region is unavailable.");
        }

        if (!region.Contains(position))
        {
            return BootstrapFailure(SimulationErrorCode.OutOfBounds, "Bootstrap position is outside the core region.");
        }

        if (core.EnergyReserve < blueprint.RequiredEnergy)
        {
            return BootstrapFailure(SimulationErrorCode.NoEnergy, "Core energy reserve is insufficient.");
        }

        if (core.MaterialReserve < blueprint.RequiredMaterial)
        {
            return BootstrapFailure(
                SimulationErrorCode.InsufficientMaterial,
                "Core material reserve is insufficient.");
        }

        core.EnergyReserve -= blueprint.RequiredEnergy;
        core.MaterialReserve -= blueprint.RequiredMaterial;
        World.Statistics.EnergySpentTotal += blueprint.RequiredEnergy;
        World.Statistics.MaterialSpentTotal += blueprint.RequiredMaterial;

        var entityId = new EntityId(World.NextEntityId++);
        var definition = new EntityDefinition(
            entityId,
            civilizationId,
            core.RegionId,
            position,
            blueprint.MaxHealth,
            blueprint.Energy,
            blueprint.EnergyCapacity,
            blueprint.Material,
            blueprint.Sensor,
            blueprint.Weapon,
            blueprint.Drive,
            blueprint.Comm);

        region.Entities.Add(entityId, new EntityState(definition));

        var bootstrapEvent = new EntityBootstrapped(
            World.Tick,
            entityId,
            civilizationId,
            blueprint.Name,
            blueprint.RequiredEnergy,
            blueprint.RequiredMaterial);

        return new BootstrapResult(
            true,
            entityId,
            SimulationErrorCode.None,
            "Minimum entity bootstrapped from the seed core.",
            bootstrapEvent);
    }

    /// <summary>
    /// The unified host command path for workshop deployment and explicit upgrades.
    /// UI callers must use this instead of mutating DeploymentService directly.
    /// </summary>
    public DeploymentCommandResult ExecuteDeploymentCommand(DeploymentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var tick = World.Tick;

        try
        {
            switch (command)
            {
                case DeployCommand deploy:
                {
                    DeploymentService.Validate(
                        World,
                        deploy.Blueprint,
                        deploy.CivilizationId,
                        deploy.RegionId,
                        deploy.Position);

                    if (deploy.Blueprint.BuildTicks > 0)
                    {
                        var materialPaid = DeploymentService.ConsumeMaterial(
                            World,
                            deploy.Blueprint,
                            deploy.CivilizationId,
                            deploy.RegionId);

                        var orderId = World.NextConstructionOrderId++;
                        var completeTick = tick + deploy.Blueprint.BuildTicks;
                        World.ConstructionOrders.Add(new ConstructionOrder(
                            orderId,
                            deploy.CivilizationId,
                            deploy.Blueprint.BlueprintId,
                            deploy.Blueprint.Version,
                            deploy.RegionId,
                            deploy.Position,
                            tick,
                            completeTick,
                            materialPaid));
                        World.Statistics.ConstructionsStartedTotal += 1;

                        var queued = new ConstructionQueued(
                            tick,
                            orderId,
                            deploy.CivilizationId,
                            deploy.Blueprint.BlueprintId,
                            deploy.Blueprint.Version,
                            deploy.RegionId,
                            deploy.Position,
                            completeTick);

                        return new DeploymentCommandResult(
                            true,
                            null,
                            SimulationErrorCode.None,
                            $"Queued {deploy.Blueprint.BlueprintId} v{deploy.Blueprint.Version} for tick {completeTick}.",
                            new SimulationEvent[] { queued });
                    }

                    var entity = DeploymentService.Deploy(
                        World,
                        deploy.Blueprint,
                        deploy.CivilizationId,
                        deploy.RegionId,
                        deploy.Position);
                    var deployed = new EntityDeployed(
                        tick,
                        entity.Id,
                        deploy.CivilizationId,
                        deploy.Blueprint.BlueprintId,
                        deploy.Blueprint.Version,
                        entity.RegionId,
                        entity.Position);
                    return new DeploymentCommandResult(
                        true,
                        entity.Id,
                        SimulationErrorCode.None,
                        $"Deployed {deploy.Blueprint.BlueprintId} v{deploy.Blueprint.Version}.",
                        new SimulationEvent[] { deployed });
                }
                case UpgradeCommand upgrade:
                    return ExecuteUpgrade(upgrade, tick);
                case CancelConstructionCommand cancel:
                    return ExecuteCancelConstruction(cancel, tick);
                default:
                    return new DeploymentCommandResult(
                        false,
                        null,
                        SimulationErrorCode.DeploymentRejected,
                        "Unknown deployment command.",
                        Array.Empty<SimulationEvent>());
            }
        }
        catch (BlueprintValidationException exception)
        {
            return new DeploymentCommandResult(
                false,
                null,
                MapDeploymentError(exception.Code),
                exception.Message,
                Array.Empty<SimulationEvent>());
        }
    }

    /// <summary>
    /// Unified host entry for core recovery. Dialogue/UI must use this command path
    /// rather than calling the low-level bootstrap methods directly.
    /// </summary>
    public RecoveryCommandResult ExecuteRecoveryCommand(RecoveryCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command switch
        {
            BootstrapCoreCommand bootstrap => FromBootstrap(
                TryBootstrapFromCore(
                    bootstrap.CivilizationId,
                    bootstrap.Blueprint,
                    bootstrap.Position)),
            RestoreCoreProductCommand restore => FromBootstrap(
                TryRestoreLatestProduct(
                    restore.CivilizationId,
                    restore.Branch,
                    restore.Position,
                    restore.EnergyCost,
                    restore.MaterialCost)),
            _ => new RecoveryCommandResult(
                false,
                null,
                SimulationErrorCode.DeploymentRejected,
                "Unknown recovery command.",
                null),
        };
    }

    private static RecoveryCommandResult FromBootstrap(BootstrapResult result) =>
        new(
            result.Success,
            result.EntityId,
            result.ErrorCode,
            result.Message,
            result.Event);

    /// <summary>
    /// Unified host entry for treaties and trade. Device scripts never reach this path.
    /// </summary>
    public DiplomacyCommandResult ExecuteDiplomacyCommand(DiplomacyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var events = new List<SimulationEvent>();
        ExpireDiplomacy(World.Tick, events);

        return command switch
        {
            ProposeTreatyCommand propose => ProposeTreaty(propose, events),
            RespondTreatyCommand respond => RespondTreaty(respond, events),
            BreakTreatyCommand breakTreaty => BreakTreaty(breakTreaty, events),
            ProposeTradeCommand proposeTrade => ProposeTrade(proposeTrade, events),
            RespondTradeCommand respondTrade => RespondTrade(respondTrade, events),
            CancelTradeCommand cancelTrade => CancelTrade(cancelTrade, events),
            CounterTradeCommand counterTrade => CounterTrade(counterTrade, events),
            _ => new DiplomacyCommandResult(
                false,
                null,
                null,
                null,
                SimulationErrorCode.InvalidAction,
                "Unknown diplomacy command.",
                events),
        };
    }

    private DiplomacyCommandResult BreakTreaty(
        BreakTreatyCommand command,
        List<SimulationEvent> events)
    {
        if (!World.Treaties.TryGetValue(command.TreatyId, out var treaty) ||
            !treaty.Involves(command.CivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyNotFound,
                "Treaty does not exist for this civilization.",
                events);
        }

        World.Treaties.Remove(treaty.Id);
        events.Add(new TreatyBroken(
            World.Tick,
            treaty.Id,
            treaty.Kind,
            treaty.CivilizationA,
            treaty.CivilizationB,
            command.CivilizationId));

        if (treaty.Kind == TreatyKind.Trade)
        {
            var cancelledOffers = World.TradeOffers.Values
                .Where(offer =>
                    offer.Involves(treaty.CivilizationA) &&
                    offer.Involves(treaty.CivilizationB))
                .OrderBy(offer => offer.Id)
                .ToList();
            foreach (var offer in cancelledOffers)
            {
                var refunded = RefundTradeOffer(offer);
                World.TradeOffers.Remove(offer.Id);
                events.Add(new TradeCancelled(
                    World.Tick,
                    offer.Id,
                    offer.ProposerCivilizationId,
                    offer.TargetCivilizationId,
                    command.CivilizationId,
                    refunded.Material,
                    refunded.Energy));
            }
        }

        return new DiplomacyCommandResult(
            true,
            treaty.Id,
            null,
            null,
            SimulationErrorCode.None,
            $"Treaty {treaty.Id} broken.",
            events);
    }

    private DiplomacyCommandResult ProposeTrade(
        ProposeTradeCommand command,
        List<SimulationEvent> events)
    {
        if (string.IsNullOrWhiteSpace(command.CivilizationId) ||
            string.IsNullOrWhiteSpace(command.TargetCivilizationId) ||
            string.IsNullOrWhiteSpace(command.RegionId) ||
            string.Equals(
                command.CivilizationId,
                command.TargetCivilizationId,
                StringComparison.Ordinal) ||
            command.GiveMaterial < 0 ||
            command.GiveEnergy < 0 ||
            command.WantMaterial < 0 ||
            command.WantEnergy < 0 ||
            (command.GiveMaterial == 0 && command.GiveEnergy == 0) ||
            (command.WantMaterial == 0 && command.WantEnergy == 0) ||
            command.ExpiresInTicks <= 0)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                "Trade offer needs two different civilizations, non-negative goods, " +
                "both sides non-empty and a positive expiry.",
                events);
        }

        if (!HasActiveTreaty(
                TreatyKind.Trade,
                command.CivilizationId,
                command.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyDenied,
                "A trade treaty is required.",
                events);
        }

        if (!HasCommunicationPath(command.CivilizationId, command.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoRoute,
                "No communication path between the civilizations.",
                events);
        }

        if (!World.Regions.TryGetValue(command.RegionId, out var region))
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                $"Trade region '{command.RegionId}' does not exist.",
                events);
        }

        if (region.GetMaterial(command.CivilizationId) < command.GiveMaterial)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InsufficientMaterial,
                "Not enough material to escrow the trade offer.",
                events);
        }

        if (region.GetEnergy(command.CivilizationId) < command.GiveEnergy)
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoEnergy,
                "Not enough energy to escrow the trade offer.",
                events);
        }

        region.TryConsumeMaterial(command.CivilizationId, command.GiveMaterial);
        region.TryConsumeEnergy(command.CivilizationId, command.GiveEnergy);

        var tick = World.Tick;
        var offerId = World.NextTradeOfferId++;
        var offer = new TradeOfferState(
            offerId,
            command.CivilizationId,
            command.TargetCivilizationId,
            command.RegionId,
            command.GiveMaterial,
            command.GiveEnergy,
            command.WantMaterial,
            command.WantEnergy,
            offerId,
            1,
            tick,
            tick + command.ExpiresInTicks);
        World.TradeOffers.Add(offer.Id, offer);
        events.Add(new TradeProposed(
            tick,
            offer.Id,
            offer.ProposerCivilizationId,
            offer.TargetCivilizationId,
            offer.RegionId,
            offer.GiveMaterial,
            offer.GiveEnergy,
            offer.WantMaterial,
            offer.WantEnergy,
            offer.ExpiresTick));

        return new DiplomacyCommandResult(
            true,
            null,
            null,
            offer.Id,
            SimulationErrorCode.None,
            $"Trade offer {offer.Id} created.",
            events);
    }

    private DiplomacyCommandResult RespondTrade(
        RespondTradeCommand command,
        List<SimulationEvent> events)
    {
        if (!World.TradeOffers.TryGetValue(command.TradeOfferId, out var offer) ||
            !string.Equals(
                offer.TargetCivilizationId,
                command.CivilizationId,
                StringComparison.Ordinal))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TradeNotFound,
                "Trade offer does not exist for this civilization.",
                events);
        }

        if (!HasCommunicationPath(
                offer.ProposerCivilizationId,
                offer.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoRoute,
                "No communication path between the civilizations.",
                events);
        }

        if (!command.Accept)
        {
            var refunded = RefundTradeOffer(offer);
            World.TradeOffers.Remove(offer.Id);
            events.Add(new TradeRejected(
                World.Tick,
                offer.Id,
                offer.ProposerCivilizationId,
                offer.TargetCivilizationId,
                refunded.Material,
                refunded.Energy));
            return new DiplomacyCommandResult(
                true,
                null,
                null,
                offer.Id,
                SimulationErrorCode.None,
                $"Trade offer {offer.Id} rejected.",
                events);
        }

        if (!HasActiveTreaty(
                TreatyKind.Trade,
                offer.ProposerCivilizationId,
                offer.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyDenied,
                "A trade treaty is required.",
                events);
        }

        if (!World.Regions.TryGetValue(offer.RegionId, out var region))
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                $"Trade region '{offer.RegionId}' does not exist.",
                events);
        }

        if (region.GetMaterial(offer.TargetCivilizationId) < offer.WantMaterial)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InsufficientMaterial,
                "Target civilization lacks the requested material.",
                events);
        }

        if (region.GetEnergy(offer.TargetCivilizationId) < offer.WantEnergy)
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoEnergy,
                "Target civilization lacks the requested energy.",
                events);
        }

        region.TryConsumeMaterial(offer.TargetCivilizationId, offer.WantMaterial);
        region.TryConsumeEnergy(offer.TargetCivilizationId, offer.WantEnergy);

        region.AddMaterial(offer.TargetCivilizationId, offer.GiveMaterial);
        region.AddEnergy(offer.TargetCivilizationId, offer.GiveEnergy);
        region.AddMaterial(offer.ProposerCivilizationId, offer.WantMaterial);
        region.AddEnergy(offer.ProposerCivilizationId, offer.WantEnergy);

        World.Statistics.TradeMaterialSettledTotal +=
            offer.GiveMaterial + offer.WantMaterial;
        World.Statistics.TradeEnergySettledTotal +=
            offer.GiveEnergy + offer.WantEnergy;

        World.TradeOffers.Remove(offer.Id);
        events.Add(new TradeAccepted(
            World.Tick,
            offer.Id,
            offer.ProposerCivilizationId,
            offer.TargetCivilizationId,
            offer.RegionId,
            offer.GiveMaterial,
            offer.GiveEnergy,
            offer.WantMaterial,
            offer.WantEnergy));

        return new DiplomacyCommandResult(
            true,
            null,
            null,
            offer.Id,
            SimulationErrorCode.None,
            $"Trade offer {offer.Id} accepted.",
            events);
    }

    private DiplomacyCommandResult CancelTrade(
        CancelTradeCommand command,
        List<SimulationEvent> events)
    {
        if (!World.TradeOffers.TryGetValue(command.TradeOfferId, out var offer) ||
            !string.Equals(
                offer.ProposerCivilizationId,
                command.CivilizationId,
                StringComparison.Ordinal))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TradeNotFound,
                "Trade offer does not exist for this civilization.",
                events);
        }

        var refunded = RefundTradeOffer(offer);
        World.TradeOffers.Remove(offer.Id);
        events.Add(new TradeCancelled(
            World.Tick,
            offer.Id,
            offer.ProposerCivilizationId,
            offer.TargetCivilizationId,
            command.CivilizationId,
            refunded.Material,
            refunded.Energy));

        return new DiplomacyCommandResult(
            true,
            null,
            null,
            offer.Id,
            SimulationErrorCode.None,
            $"Trade offer {offer.Id} cancelled.",
            events);
    }

    private DiplomacyCommandResult CounterTrade(
        CounterTradeCommand command,
        List<SimulationEvent> events)
    {
        if (!World.TradeOffers.TryGetValue(command.TradeOfferId, out var original) ||
            !string.Equals(
                original.TargetCivilizationId,
                command.CivilizationId,
                StringComparison.Ordinal))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TradeNotFound,
                "Trade offer does not exist for this civilization.",
                events);
        }

        if (original.Round >= TradeNegotiationRules.MaxRounds)
        {
            return DiplomacyFailure(
                SimulationErrorCode.NegotiationExhausted,
                "Trade negotiation has reached its round limit.",
                events);
        }

        if (command.GiveMaterial < 0 ||
            command.GiveEnergy < 0 ||
            command.WantMaterial < 0 ||
            command.WantEnergy < 0 ||
            (command.GiveMaterial == 0 && command.GiveEnergy == 0) ||
            (command.WantMaterial == 0 && command.WantEnergy == 0) ||
            command.ExpiresInTicks <= 0)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                "Counter offer needs non-negative goods, both sides non-empty and a positive expiry.",
                events);
        }

        if (!HasCommunicationPath(
                command.CivilizationId,
                original.ProposerCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoRoute,
                "No communication path between the civilizations.",
                events);
        }

        if (!HasActiveTreaty(
                TreatyKind.Trade,
                command.CivilizationId,
                original.ProposerCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyDenied,
                "A trade treaty is required.",
                events);
        }

        if (!World.Regions.TryGetValue(original.RegionId, out var region))
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                $"Trade region '{original.RegionId}' does not exist.",
                events);
        }

        if (region.GetMaterial(command.CivilizationId) < command.GiveMaterial)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InsufficientMaterial,
                "Not enough material to escrow the counter offer.",
                events);
        }

        if (region.GetEnergy(command.CivilizationId) < command.GiveEnergy)
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoEnergy,
                "Not enough energy to escrow the counter offer.",
                events);
        }

        var refunded = RefundTradeOffer(original);
        World.TradeOffers.Remove(original.Id);
        region.TryConsumeMaterial(command.CivilizationId, command.GiveMaterial);
        region.TryConsumeEnergy(command.CivilizationId, command.GiveEnergy);

        var tick = World.Tick;
        var offerId = World.NextTradeOfferId++;
        var offer = new TradeOfferState(
            offerId,
            command.CivilizationId,
            original.ProposerCivilizationId,
            original.RegionId,
            command.GiveMaterial,
            command.GiveEnergy,
            command.WantMaterial,
            command.WantEnergy,
            original.NegotiationId,
            original.Round + 1,
            tick,
            tick + command.ExpiresInTicks);
        World.TradeOffers.Add(offer.Id, offer);
        events.Add(new TradeCountered(
            tick,
            original.Id,
            offer.Id,
            command.CivilizationId,
            offer.NegotiationId,
            offer.Round,
            refunded.Material,
            refunded.Energy,
            offer.GiveMaterial,
            offer.GiveEnergy,
            offer.WantMaterial,
            offer.WantEnergy));

        return new DiplomacyCommandResult(
            true,
            null,
            null,
            offer.Id,
            SimulationErrorCode.None,
            $"Trade offer {original.Id} countered with {offer.Id}.",
            events);
    }

    private (long Material, long Energy) RefundTradeOffer(TradeOfferState offer)
    {
        if (!World.Regions.TryGetValue(offer.RegionId, out var region))
        {
            return (0, 0);
        }

        region.AddMaterial(offer.ProposerCivilizationId, offer.GiveMaterial);
        region.AddEnergy(offer.ProposerCivilizationId, offer.GiveEnergy);
        return (offer.GiveMaterial, offer.GiveEnergy);
    }

    private void ExpireDiplomacy(long tick, List<SimulationEvent> events)
    {
        var expiredProposals = World.TreatyProposals.Values
            .Where(proposal => proposal.ExpiresTick <= tick)
            .OrderBy(proposal => proposal.Id)
            .ToList();
        foreach (var proposal in expiredProposals)
        {
            World.TreatyProposals.Remove(proposal.Id);
            events.Add(new TreatyProposalExpired(
                tick,
                proposal.Id,
                proposal.Kind,
                proposal.ProposerCivilizationId,
                proposal.TargetCivilizationId));
        }

        var expiredOffers = World.TradeOffers.Values
            .Where(offer => offer.ExpiresTick <= tick)
            .OrderBy(offer => offer.Id)
            .ToList();
        foreach (var offer in expiredOffers)
        {
            var refunded = RefundTradeOffer(offer);
            World.TradeOffers.Remove(offer.Id);
            events.Add(new TradeExpired(
                tick,
                offer.Id,
                offer.ProposerCivilizationId,
                offer.TargetCivilizationId,
                refunded.Material,
                refunded.Energy));
        }
    }

    private DiplomacyCommandResult ProposeTreaty(
        ProposeTreatyCommand command,
        List<SimulationEvent> events)
    {
        if (string.IsNullOrWhiteSpace(command.CivilizationId) ||
            string.IsNullOrWhiteSpace(command.TargetCivilizationId) ||
            string.Equals(
                command.CivilizationId,
                command.TargetCivilizationId,
                StringComparison.Ordinal) ||
            command.ExpiresInTicks <= 0)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                "Treaty proposal needs two different civilizations and a positive expiry.",
                events);
        }

        if (!HasCommunicationPath(command.CivilizationId, command.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoRoute,
                "No communication path between the civilizations.",
                events);
        }

        if (HasActiveTreaty(
                command.Kind,
                command.CivilizationId,
                command.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyExists,
                "A treaty of this kind is already active.",
                events);
        }

        if (HasPendingProposal(
                command.Kind,
                command.CivilizationId,
                command.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyExists,
                "A proposal of this kind is already pending.",
                events);
        }

        var tick = World.Tick;
        var proposal = new TreatyProposalState(
            World.NextTreatyProposalId++,
            command.Kind,
            command.CivilizationId,
            command.TargetCivilizationId,
            tick,
            tick + command.ExpiresInTicks);
        World.TreatyProposals.Add(proposal.Id, proposal);
        events.Add(new TreatyProposed(
            tick,
            proposal.Id,
            proposal.Kind,
            proposal.ProposerCivilizationId,
            proposal.TargetCivilizationId,
            proposal.ExpiresTick));

        return new DiplomacyCommandResult(
            true,
            null,
            proposal.Id,
            null,
            SimulationErrorCode.None,
            $"Treaty proposal {proposal.Id} created.",
            events);
    }

    private DiplomacyCommandResult RespondTreaty(
        RespondTreatyCommand command,
        List<SimulationEvent> events)
    {
        if (!World.TreatyProposals.TryGetValue(command.ProposalId, out var proposal))
        {
            return DiplomacyFailure(
                SimulationErrorCode.ProposalNotFound,
                "Treaty proposal does not exist.",
                events);
        }

        if (!string.Equals(
                proposal.TargetCivilizationId,
                command.CivilizationId,
                StringComparison.Ordinal))
        {
            return DiplomacyFailure(
                SimulationErrorCode.ProposalNotFound,
                "Treaty proposal does not exist.",
                events);
        }

        if (!HasCommunicationPath(
                proposal.ProposerCivilizationId,
                proposal.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoRoute,
                "No communication path between the civilizations.",
                events);
        }

        var tick = World.Tick;

        if (!command.Accept)
        {
            World.TreatyProposals.Remove(proposal.Id);
            events.Add(new TreatyRejected(
                tick,
                proposal.Id,
                proposal.Kind,
                proposal.ProposerCivilizationId,
                proposal.TargetCivilizationId));
            return new DiplomacyCommandResult(
                true,
                null,
                proposal.Id,
                null,
                SimulationErrorCode.None,
                $"Treaty proposal {proposal.Id} rejected.",
                events);
        }

        if (HasActiveTreaty(
                proposal.Kind,
                proposal.ProposerCivilizationId,
                proposal.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyExists,
                "A treaty of this kind is already active.",
                events);
        }

        World.TreatyProposals.Remove(proposal.Id);
        var (civilizationA, civilizationB) = OrderParties(
            proposal.ProposerCivilizationId,
            proposal.TargetCivilizationId);
        var treaty = new TreatyState(
            World.NextTreatyId++,
            proposal.Kind,
            civilizationA,
            civilizationB,
            proposal.ProposerCivilizationId,
            tick);
        World.Treaties.Add(treaty.Id, treaty);
        events.Add(new TreatyAccepted(
            tick,
            treaty.Id,
            treaty.Kind,
            treaty.CivilizationA,
            treaty.CivilizationB));

        return new DiplomacyCommandResult(
            true,
            treaty.Id,
            proposal.Id,
            null,
            SimulationErrorCode.None,
            $"Treaty {treaty.Id} accepted.",
            events);
    }

    private bool HasActiveTreaty(TreatyKind kind, string civilizationA, string civilizationB) =>
        World.Treaties.Values.Any(treaty =>
            treaty.Kind == kind &&
            treaty.Involves(civilizationA) &&
            treaty.Involves(civilizationB));

    private bool HasPendingProposal(TreatyKind kind, string civilizationA, string civilizationB) =>
        World.TreatyProposals.Values.Any(proposal =>
            proposal.Kind == kind &&
            proposal.Involves(civilizationA) &&
            proposal.Involves(civilizationB));

    private static (string A, string B) OrderParties(string first, string second) =>
        string.CompareOrdinal(first, second) <= 0
            ? (first, second)
            : (second, first);

    /// <summary>
    /// Read-only query used by the host UI: both civilizations need a Comm endpoint and
    /// either a shared region or a link path between their endpoints.
    /// </summary>
    public bool HasCommunicationPath(string civilizationA, string civilizationB)
    {
        foreach (var regionA in World.Regions.Values)
        {
            if (!HasCommEndpoint(regionA.Id, civilizationA))
            {
                continue;
            }

            foreach (var regionB in World.Regions.Values)
            {
                if (!HasCommEndpoint(regionB.Id, civilizationB))
                {
                    continue;
                }

                if (string.Equals(regionA.Id, regionB.Id, StringComparison.Ordinal) ||
                    FindMessagePath(regionA.Id, regionB.Id) is not null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static DiplomacyCommandResult DiplomacyFailure(
        SimulationErrorCode code,
        string message,
        IReadOnlyList<SimulationEvent>? events = null) =>
        new(
            false,
            null,
            null,
            null,
            code,
            message,
            events ?? Array.Empty<SimulationEvent>());

    /// <summary>
    /// Restores the latest confirmed product for one branch directly from the core.
    /// Costs are supplied by the caller because the real recovery quotas are still an
    /// open design value.
    /// </summary>
    public BootstrapResult TryRestoreLatestProduct(
        string civilizationId,
        DeviceBranch branch,
        Vec2 position,
        long energyCost,
        long materialCost)
    {
        if (energyCost < 0 || materialCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(energyCost), "Recovery costs must be non-negative.");
        }

        var tick = World.Tick;

        if (!World.Cores.TryGetValue(civilizationId, out var core))
        {
            return BootstrapFailure(SimulationErrorCode.CoreNotAvailable, "No seed core for civilization.");
        }

        if (!core.LatestProducts.TryGetValue(branch, out var product))
        {
            return BootstrapFailure(
                SimulationErrorCode.DeploymentRejected,
                $"No confirmed product is registered for {branch}.");
        }

        var blueprint = World.Workshop.GetVersion(product.BlueprintId, product.Version);
        if (blueprint is null)
        {
            return BootstrapFailure(
                SimulationErrorCode.DeploymentRejected,
                $"Registered product {product.BlueprintId} v{product.Version} is missing from the workshop.");
        }

        if (!World.Regions.TryGetValue(core.RegionId, out var region) || !region.IsCoreRegion)
        {
            return BootstrapFailure(SimulationErrorCode.CoreNotAvailable, "Core region is unavailable.");
        }

        if (!region.Contains(position))
        {
            return BootstrapFailure(SimulationErrorCode.OutOfBounds, "Restore position is outside the core region.");
        }

        if (core.EnergyReserve < energyCost)
        {
            return BootstrapFailure(SimulationErrorCode.NoEnergy, "Core energy reserve is insufficient.");
        }

        if (core.MaterialReserve < materialCost)
        {
            return BootstrapFailure(
                SimulationErrorCode.InsufficientMaterial,
                "Core material reserve is insufficient.");
        }

        core.EnergyReserve -= energyCost;
        core.MaterialReserve -= materialCost;
        World.Statistics.EnergySpentTotal += energyCost;
        World.Statistics.MaterialSpentTotal += materialCost;

        var entity = DeploymentService.Deploy(World, blueprint, civilizationId, core.RegionId, position);
        var bootstrapEvent = new EntityBootstrapped(
            tick,
            entity.Id,
            civilizationId,
            blueprint.Name,
            energyCost,
            materialCost);

        return new BootstrapResult(
            true,
            entity.Id,
            SimulationErrorCode.None,
            $"Restored {blueprint.Name} v{blueprint.Version} from the seed core.",
            bootstrapEvent);
    }

    private DeploymentCommandResult ExecuteCancelConstruction(
        CancelConstructionCommand cancel,
        long tick)
    {
        var order = World.ConstructionOrders.FirstOrDefault(candidate =>
            candidate.Id == cancel.OrderId);
        if (order is null ||
            !string.Equals(
                order.CivilizationId,
                cancel.CivilizationId,
                StringComparison.Ordinal))
        {
            return new DeploymentCommandResult(
                false,
                null,
                SimulationErrorCode.DeploymentRejected,
                $"Construction order '{cancel.OrderId}' is not available to this civilization.",
                Array.Empty<SimulationEvent>());
        }

        World.ConstructionOrders.Remove(order);

        if (World.Regions.TryGetValue(order.RegionId, out var region))
        {
            region.AddMaterial(order.CivilizationId, order.MaterialPaid);
        }

        World.Statistics.MaterialRefundedTotal += order.MaterialPaid;

        var cancelled = new ConstructionCancelled(
            tick,
            order.Id,
            order.CivilizationId,
            order.BlueprintId,
            order.MaterialPaid);

        return new DeploymentCommandResult(
            true,
            null,
            SimulationErrorCode.None,
            $"Cancelled order {order.Id}; refunded {order.MaterialPaid} M.",
            new SimulationEvent[] { cancelled });
    }

    private DeploymentCommandResult ExecuteUpgrade(UpgradeCommand upgrade, long tick)
    {
        var entity = World.FindEntity(upgrade.EntityId);
        if (entity is null)
        {
            return new DeploymentCommandResult(
                false,
                null,
                SimulationErrorCode.DeploymentRejected,
                $"Entity '{upgrade.EntityId}' does not exist.",
                Array.Empty<SimulationEvent>());
        }

        if (!string.Equals(entity.CivilizationId, upgrade.CivilizationId, StringComparison.Ordinal))
        {
            return new DeploymentCommandResult(
                false,
                null,
                SimulationErrorCode.DeploymentRejected,
                "Entity does not belong to this civilization.",
                Array.Empty<SimulationEvent>());
        }

        var oldVersion = entity.BlueprintVersion;
        DeploymentService.Upgrade(entity, upgrade.Blueprint);
        var upgraded = new EntityUpgraded(
            tick,
            entity.Id,
            entity.CivilizationId,
            upgrade.Blueprint.BlueprintId,
            oldVersion,
            upgrade.Blueprint.Version);

        return new DeploymentCommandResult(
            true,
            entity.Id,
            SimulationErrorCode.None,
            $"Upgraded {upgrade.Blueprint.BlueprintId} v{oldVersion} -> v{upgrade.Blueprint.Version}.",
            new SimulationEvent[] { upgraded });
    }

    private static SimulationErrorCode MapDeploymentError(string code) =>
        code switch
        {
            "CORE_PROTECTED" => SimulationErrorCode.CoreProtected,
            "INSUFFICIENT_MATERIAL" => SimulationErrorCode.InsufficientMaterial,
            "OUT_OF_BOUNDS" => SimulationErrorCode.OutOfBounds,
            _ => SimulationErrorCode.DeploymentRejected,
        };

    private void ProcessIntent(
        Intent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results,
        Dictionary<EntityId, int> computeUsed,
        Dictionary<EntityId, int> bandwidthUsed,
        Dictionary<string, int> linkUsage,
        Dictionary<string, int> materialLinkUsage,
        Dictionary<string, int> energyLinkUsage)
    {
        if (intent is null)
        {
            return;
        }

        var entity = World.FindEntity(intent.EntityId, out var region);
        if (entity is null || region is null)
        {
            Reject(intent, SimulationErrorCode.UnknownEntity, "Entity does not exist.", tick, events, results);
            return;
        }

        if (intent.IssuerCivilizationId is { } issuer &&
            !string.Equals(issuer, entity.CivilizationId, StringComparison.Ordinal))
        {
            // Deliberately identical to "unknown entity" so a controller cannot probe
            // for foreign ids by observing different rejection codes.
            Reject(
                intent,
                SimulationErrorCode.UnknownEntity,
                "Entity is not available to this controller.",
                tick,
                events,
                results);
            return;
        }

        if (CoreProtection.IsProtectedFrom(World, region.Id, entity.CivilizationId))
        {
            Reject(
                intent,
                SimulationErrorCode.CoreProtected,
                $"Region '{region.Id}' is a protected core region for another civilization.",
                tick,
                events,
                results);
            return;
        }

        if (!IsRegionActive(region.Id))
        {
            Reject(
                intent,
                SimulationErrorCode.RegionInactive,
                $"Region '{region.Id}' is not active; controller intents are rejected.",
                tick,
                events,
                results);
            return;
        }

        var computeCost = IntentComputeCost(intent);
        if (computeCost > 0 && entity.State is { } state)
        {
            var used = computeUsed.GetValueOrDefault(entity.Id);
            var budget = Math.Max(0, state.ComputePerTick);
            if (used + computeCost > budget)
            {
                Reject(
                    intent,
                    SimulationErrorCode.ComputeBudgetExceeded,
                    $"Compute budget {budget} per tick is exhausted.",
                    tick,
                    events,
                    results);
                return;
            }

            computeUsed[entity.Id] = used + computeCost;
        }

        switch (intent)
        {
            case MoveIntent move:
                ProcessMove(entity, region, move, tick, events, results);
                break;
            case AttackIntent attack:
                ProcessAttack(entity, region, attack, tick, events, results);
                break;
            case SendMessageIntent send:
                ProcessSendMessage(entity, send, tick, events, results, bandwidthUsed, linkUsage);
                break;
            case TransferMaterialIntent transfer:
                ProcessTransferMaterial(entity, region, transfer, tick, events, results, materialLinkUsage);
                break;
            case SendEnergyIntent sendEnergy:
                ProcessSendEnergy(entity, region, sendEnergy, tick, events, results, energyLinkUsage);
                break;
            case DrawEnergyIntent drawEnergy:
                ProcessDrawEnergy(entity, region, drawEnergy, tick, events, results);
                break;
            case WaitIntent:
                Accept(intent, "Entity waits.", results);
                break;
            default:
                Reject(intent, SimulationErrorCode.InvalidAction, "Unknown intent type.", tick, events, results);
                break;
        }
    }

    private static int IntentComputeCost(Intent intent) => intent is WaitIntent ? 0 : 1;

    private void GeneratePower()
    {
        foreach (var region in World.Regions.Values)
        {
            if (!IsRegionActive(region.Id))
            {
                continue;
            }

            foreach (var entity in region.Entities.Values)
            {
                if (entity.Power is null)
                {
                    continue;
                }

                var generated = Math.Max(0, entity.Power.EnergyPerTick);
                if (generated <= 0)
                {
                    continue;
                }

                var room = Math.Max(0, entity.EnergyCapacity - entity.Energy);
                var toLocal = Math.Min(generated, room);
                var overflow = generated - toLocal;
                if (toLocal > 0)
                {
                    entity.Energy += toLocal;
                }

                if (overflow > 0)
                {
                    region.AddEnergy(entity.CivilizationId, overflow);
                    World.Statistics.EnergyOverflowedTotal += overflow;
                }

                World.Statistics.EnergyGeneratedTotal += generated;
            }
        }
    }

    private void FlowPower(long tick, List<SimulationEvent> events)
    {
        if (World.Links.Values.All(link => link.PowerPerTick <= 0))
        {
            return;
        }

        var components = PowerGridTopology.BuildComponents(World);
        var civilizations = CollectPowerCivilizations();
        var linkUsage = new Dictionary<string, int>(StringComparer.Ordinal);
        var flowAmounts =
            new Dictionary<(string Civ, string From, string To), long>();

        foreach (var civilizationId in civilizations)
        {
            foreach (var component in components)
            {
                if (component.Count < 2)
                {
                    continue;
                }

                FlowPowerComponent(
                    civilizationId,
                    component,
                    tick,
                    linkUsage,
                    flowAmounts,
                    events);
            }
        }

        foreach (var pair in flowAmounts
                     .OrderBy(pair => pair.Key.Civ, StringComparer.Ordinal)
                     .ThenBy(pair => pair.Key.From, StringComparer.Ordinal)
                     .ThenBy(pair => pair.Key.To, StringComparer.Ordinal))
        {
            events.Add(new PowerFlowed(
                tick,
                pair.Key.Civ,
                pair.Key.From,
                pair.Key.To,
                pair.Value));
        }
    }

    private void FlowPowerComponent(
        string civilizationId,
        IReadOnlyList<string> component,
        long tick,
        Dictionary<string, int> linkUsage,
        Dictionary<(string Civ, string From, string To), long> flowAmounts,
        List<SimulationEvent> events)
    {
        var componentSet = new HashSet<string>(component, StringComparer.Ordinal);
        var incoming = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var shipment in World.InTransitPower)
        {
            if (!string.Equals(
                    shipment.CivilizationId,
                    civilizationId,
                    StringComparison.Ordinal) ||
                shipment.ArriveTick <= tick ||
                !componentSet.Contains(shipment.ToRegionId))
            {
                continue;
            }

            incoming[shipment.ToRegionId] =
                incoming.GetValueOrDefault(shipment.ToRegionId) +
                shipment.DeliveredAmount;
        }

        var projected = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var regionId in component)
        {
            projected[regionId] =
                World.Regions[regionId].GetEnergy(civilizationId) +
                incoming.GetValueOrDefault(regionId);
        }

        var total = projected.Values.Sum();
        if (total <= 0)
        {
            return;
        }

        var target = total / component.Count;
        var remainder = (int)(total % component.Count);
        var desired = new Dictionary<string, long>(StringComparer.Ordinal);
        var ordered = component
            .OrderBy(regionId => projected[regionId])
            .ThenBy(regionId => regionId, StringComparer.Ordinal)
            .ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            desired[ordered[index]] = target + (index < remainder ? 1 : 0);
        }

        var distances = ComputePowerDistances(
            civilizationId,
            componentSet,
            desired,
            linkUsage);
        var deficits = component
            .Where(regionId => projected[regionId] < desired[regionId])
            .OrderBy(regionId =>
                distances.TryGetValue(regionId, out var distance)
                    ? distance
                    : int.MaxValue)
            .ThenBy(regionId => regionId, StringComparer.Ordinal)
            .ToList();

        foreach (var deficitRegionId in deficits)
        {
            while (true)
            {
                var projectedStock =
                    World.Regions[deficitRegionId].GetEnergy(civilizationId) +
                    incoming.GetValueOrDefault(deficitRegionId);
                var deficit = desired[deficitRegionId] - projectedStock;
                if (deficit <= 0)
                {
                    break;
                }

                var path = FindPowerPath(
                    civilizationId,
                    deficitRegionId,
                    componentSet,
                    desired,
                    linkUsage);
                if (path is null)
                {
                    break;
                }

                var sourceEnergy =
                    World.Regions[path.SourceRegionId].GetEnergy(civilizationId);
                var sourceSurplus = sourceEnergy - desired[path.SourceRegionId];
                var pathCapacity = path.Links.Min(link =>
                    link.PowerPerTick - linkUsage.GetValueOrDefault(link.Id));
                var amount = Math.Min(
                    Math.Min(sourceSurplus, deficit),
                    pathCapacity);
                if (amount <= 0)
                {
                    break;
                }

                var delivered = amount;
                foreach (var link in path.Links)
                {
                    delivered = delivered * (100 - link.PowerLossPercent) / 100;
                }

                var lost = amount - delivered;
                if (!World.Regions[path.SourceRegionId]
                        .TryConsumeEnergy(civilizationId, amount))
                {
                    break;
                }

                foreach (var link in path.Links)
                {
                    linkUsage[link.Id] =
                        linkUsage.GetValueOrDefault(link.Id) + (int)amount;
                }

                var latency = path.Links.Sum(link => link.PowerLatencyTicks);
                if (latency <= 0)
                {
                    World.Regions[deficitRegionId].AddEnergy(
                        civilizationId,
                        delivered);
                    World.Statistics.EnergyFlowedTotal += delivered;
                    World.Statistics.EnergyFlowLossTotal += lost;

                    if (delivered > 0)
                    {
                        var key = (
                            civilizationId,
                            path.SourceRegionId,
                            deficitRegionId);
                        flowAmounts[key] =
                            flowAmounts.GetValueOrDefault(key) + delivered;
                    }
                }
                else
                {
                    var shipmentId = World.NextPowerShipmentId++;
                    var arriveTick = tick + latency;
                    var route = string.Join(
                        ">",
                        path.Links.Select(link => link.Id));
                    World.InTransitPower.Add(new PowerShipment(
                        shipmentId,
                        civilizationId,
                        path.SourceRegionId,
                        deficitRegionId,
                        route,
                        amount,
                        delivered,
                        tick,
                        arriveTick));
                    incoming[deficitRegionId] =
                        incoming.GetValueOrDefault(deficitRegionId) + delivered;
                    events.Add(new PowerShipped(
                        tick,
                        shipmentId,
                        civilizationId,
                        path.SourceRegionId,
                        deficitRegionId,
                        amount,
                        delivered,
                        arriveTick,
                        route));
                }
            }
        }
    }

    private Dictionary<string, int> ComputePowerDistances(
        string civilizationId,
        HashSet<string> componentSet,
        IReadOnlyDictionary<string, long> desired,
        Dictionary<string, int> linkUsage)
    {
        var distances = new Dictionary<string, int>(StringComparer.Ordinal);
        var pending = new Queue<string>();

        foreach (var regionId in componentSet.OrderBy(regionId => regionId, StringComparer.Ordinal))
        {
            if (World.Regions[regionId].GetEnergy(civilizationId) > desired[regionId])
            {
                distances[regionId] = 0;
                pending.Enqueue(regionId);
            }
        }

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            var nextDistance = distances[current] + 1;

            foreach (var link in PowerLinks(current, componentSet, linkUsage))
            {
                var next = link.Other(current);
                if (distances.ContainsKey(next))
                {
                    continue;
                }

                distances[next] = nextDistance;
                pending.Enqueue(next);
            }
        }

        return distances;
    }

    private PowerPath? FindPowerPath(
        string civilizationId,
        string deficitRegionId,
        HashSet<string> componentSet,
        IReadOnlyDictionary<string, long> desired,
        Dictionary<string, int> linkUsage)
    {
        var parents = new Dictionary<string, (string Previous, RegionLink Link)>(
            StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { deficitRegionId };
        var pending = new Queue<string>();
        pending.Enqueue(deficitRegionId);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();

            if (!string.Equals(current, deficitRegionId, StringComparison.Ordinal) &&
                World.Regions[current].GetEnergy(civilizationId) > desired[current])
            {
                var links = new List<RegionLink>();
                var node = current;
                while (parents.TryGetValue(node, out var parent))
                {
                    links.Add(parent.Link);
                    node = parent.Previous;
                }

                links.Reverse();
                return new PowerPath(current, links);
            }

            foreach (var link in PowerLinks(current, componentSet, linkUsage))
            {
                var next = link.Other(current);
                if (!visited.Add(next))
                {
                    continue;
                }

                parents[next] = (current, link);
                pending.Enqueue(next);
            }
        }

        return null;
    }

    private IEnumerable<RegionLink> PowerLinks(
        string regionId,
        HashSet<string> componentSet,
        Dictionary<string, int> linkUsage) =>
        World.Links.Values
            .Where(link =>
                link.PowerPerTick > 0 &&
                link.Connects(regionId) &&
                componentSet.Contains(link.Other(regionId)) &&
                link.PowerPerTick - linkUsage.GetValueOrDefault(link.Id) > 0)
            .OrderBy(link => link.Id, StringComparer.Ordinal)
            .ThenBy(link => link.Other(regionId), StringComparer.Ordinal);

    private IReadOnlyList<string> CollectPowerCivilizations()
    {
        var civilizations = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var region in World.Regions.Values)
        {
            foreach (var civilizationId in region.Stocks.Keys)
            {
                civilizations.Add(civilizationId);
            }

            foreach (var entity in region.Entities.Values)
            {
                civilizations.Add(entity.CivilizationId);
            }
        }

        return civilizations.ToList();
    }

    private sealed record PowerPath(
        string SourceRegionId,
        IReadOnlyList<RegionLink> Links);

    private void ProcessMove(
        EntityState entity,
        RegionState region,
        MoveIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results)
    {
        if (entity.Drive is null)
        {
            Reject(intent, SimulationErrorCode.NoDrive, "Entity has no drive module.", tick, events, results);
            return;
        }

        if (!region.Contains(intent.Target))
        {
            Reject(intent, SimulationErrorCode.OutOfBounds, "Move target is outside the region.", tick, events, results);
            return;
        }

        var from = entity.Position;
        var to = from.MoveToward(intent.Target, entity.Drive.SpeedMmPerTick);
        if (to == from)
        {
            Accept(intent, "Entity is already at the target.", results);
            return;
        }

        var cost = entity.Drive.EnergyCostPerMove;
        if (entity.Energy < cost &&
            !TryAutoDrawForAction(entity, region, cost, tick, events))
        {
            AddPowerShortfall(
                entity,
                region.Id,
                cost,
                ActionAvailableEnergy(entity, region),
                ActionShortfallSource(entity, region, cost),
                tick,
                events);
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough energy to move.", tick, events, results);
            return;
        }

        entity.Energy -= cost;
        World.Statistics.EnergySpentTotal += cost;
        entity.Position = to;

        events.Add(new EntityMoved(tick, entity.Id, from, to, cost));
        Accept(intent, $"Moved to {to}.", results);
    }

    private void ProcessAttack(
        EntityState entity,
        RegionState region,
        AttackIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results)
    {
        if (entity.Weapon is null)
        {
            Reject(intent, SimulationErrorCode.NoWeapon, "Entity has no weapon module.", tick, events, results);
            return;
        }

        if (intent.TargetId == entity.Id)
        {
            Reject(intent, SimulationErrorCode.InvalidAction, "An entity cannot target itself.", tick, events, results);
            return;
        }

        var target = World.FindEntity(intent.TargetId, out var targetRegion);
        if (target is null || targetRegion is null || targetRegion.Id != region.Id)
        {
            Reject(intent, SimulationErrorCode.TargetUnknown, "Target is not present in this region.", tick, events, results);
            return;
        }

        if (!VisibilityRules.IsVisibleTo(World, entity.CivilizationId, target))
        {
            // Same code as a missing target: guessing an id must not reveal existence.
            Reject(intent, SimulationErrorCode.TargetUnknown, "Target is not visible.", tick, events, results);
            return;
        }

        if (!string.Equals(
                entity.CivilizationId,
                target.CivilizationId,
                StringComparison.Ordinal) &&
            HasActiveTreaty(
                TreatyKind.NonAggression,
                entity.CivilizationId,
                target.CivilizationId))
        {
            Reject(
                intent,
                SimulationErrorCode.TreatyDenied,
                "A non-aggression treaty forbids this attack.",
                tick,
                events,
                results);
            return;
        }

        if (tick < entity.NextWeaponReadyTick)
        {
            Reject(intent, SimulationErrorCode.WeaponCoolingDown, "Weapon is still cooling down.", tick, events, results);
            return;
        }

        if (!entity.Position.IsWithinRange(target.Position, entity.Weapon.RangeMm))
        {
            Reject(intent, SimulationErrorCode.OutOfRange, "Target is outside weapon range.", tick, events, results);
            return;
        }

        var cost = entity.Weapon.EnergyCost;
        if (entity.Energy < cost &&
            !TryAutoDrawForAction(entity, region, cost, tick, events))
        {
            AddPowerShortfall(
                entity,
                region.Id,
                cost,
                ActionAvailableEnergy(entity, region),
                ActionShortfallSource(entity, region, cost),
                tick,
                events);
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough energy to fire.", tick, events, results);
            return;
        }

        entity.Energy -= cost;
        World.Statistics.EnergySpentTotal += cost;
        World.Statistics.ShotsFired += 1;

        entity.NextWeaponReadyTick = tick + Math.Max(0, entity.Weapon.CooldownTicks);

        var appliedDamage = Math.Min(entity.Weapon.Damage, target.Health);
        target.Health -= appliedDamage;
        World.Statistics.DamageDealt += appliedDamage;

        events.Add(new ShotFired(
            tick,
            entity.Id,
            target.Id,
            appliedDamage,
            entity.Position,
            target.Position,
            cost));
        events.Add(new EntityDamaged(tick, target.Id, appliedDamage, Math.Max(0, target.Health)));

        if (target.Health <= 0)
        {
            targetRegion.Entities.Remove(target.Id);
            World.Statistics.EntitiesDestroyed += 1;
            World.Statistics.EnergyLostTotal += target.Energy;
            World.Statistics.MaterialLostTotal += target.Material;
            events.Add(new EntityDestroyed(tick, target.Id));
            events.Add(new EntityInventoryLost(
                tick,
                target.Id,
                target.Energy,
                target.Material));
        }

        Accept(intent, $"Hit {target.Id} for {appliedDamage}.", results);
    }

    private void ProcessSendMessage(
        EntityState entity,
        SendMessageIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results,
        Dictionary<EntityId, int> bandwidthUsed,
        Dictionary<string, int> linkUsage)
    {
        if (entity.Comm is null)
        {
            Reject(intent, SimulationErrorCode.NoComm, "Entity has no communications module.", tick, events, results);
            return;
        }

        if (string.IsNullOrWhiteSpace(intent.ToCivilizationId))
        {
            Reject(
                intent,
                SimulationErrorCode.InvalidAction,
                "Message recipient civilization is required.",
                tick,
                events,
                results);
            return;
        }

        if (string.IsNullOrWhiteSpace(intent.Topic))
        {
            Reject(intent, SimulationErrorCode.InvalidAction, "Message topic is required.", tick, events, results);
            return;
        }

        var targetRegionId = string.IsNullOrWhiteSpace(intent.TargetRegionId)
            ? entity.RegionId
            : intent.TargetRegionId;

        if (!World.Regions.ContainsKey(targetRegionId))
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"Target region '{targetRegionId}' does not exist.",
                tick,
                events,
                results);
            return;
        }

        var sameCivilization = string.Equals(
            intent.ToCivilizationId,
            entity.CivilizationId,
            StringComparison.Ordinal);

        if (!sameCivilization &&
            !HasCommEndpoint(targetRegionId, intent.ToCivilizationId))
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"Target civilization has no comm endpoint in region '{targetRegionId}'.",
                tick,
                events,
                results);
            return;
        }

        if (!sameCivilization &&
            !MessagePolicyRules.IsAllowed(
                World,
                intent.ToCivilizationId,
                entity.CivilizationId,
                intent.Topic))
        {
            Reject(
                intent,
                SimulationErrorCode.MessageDenied,
                "Target civilization refuses this sender or topic.",
                tick,
                events,
                results);
            return;
        }

        IReadOnlyList<RegionLink> route = Array.Empty<RegionLink>();
        if (!string.Equals(targetRegionId, entity.RegionId, StringComparison.Ordinal))
        {
            if (sameCivilization && !HasCommEndpoint(targetRegionId, entity.CivilizationId))
            {
                Reject(
                    intent,
                    SimulationErrorCode.NoRoute,
                    $"No own comm endpoint in target region '{targetRegionId}'.",
                    tick,
                    events,
                    results);
                return;
            }

            var path = FindMessagePath(entity.RegionId, targetRegionId);
            if (path is null)
            {
                Reject(
                    intent,
                    SimulationErrorCode.NoRoute,
                    $"No link path from '{entity.RegionId}' to '{targetRegionId}'.",
                    tick,
                    events,
                    results);
                return;
            }

            foreach (var link in path)
            {
                if (link.MessagesPerTick > 0 &&
                    linkUsage.GetValueOrDefault(link.Id) >= link.MessagesPerTick)
                {
                    Reject(
                        intent,
                        SimulationErrorCode.NoBandwidth,
                        $"Link '{link.Id}' bandwidth is exhausted for this tick.",
                        tick,
                        events,
                        results);
                    return;
                }
            }

            route = path;
        }

        var cost = entity.Comm.EnergyCostPerMessage;
        var powerRegion = World.Regions[entity.RegionId];
        if (entity.Energy < cost &&
            !TryAutoDrawForAction(entity, powerRegion, cost, tick, events))
        {
            AddPowerShortfall(
                entity,
                powerRegion.Id,
                cost,
                ActionAvailableEnergy(entity, powerRegion),
                ActionShortfallSource(entity, powerRegion, cost),
                tick,
                events);
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough energy to send.", tick, events, results);
            return;
        }

        if (entity.Comm.BandwidthPerTick > 0)
        {
            var used = bandwidthUsed.GetValueOrDefault(entity.Id);
            if (used >= entity.Comm.BandwidthPerTick)
            {
                Reject(
                    intent,
                    SimulationErrorCode.NoBandwidth,
                    $"Bandwidth {entity.Comm.BandwidthPerTick} messages per tick is exhausted.",
                    tick,
                    events,
                    results);
                return;
            }

            bandwidthUsed[entity.Id] = used + 1;
        }

        foreach (var link in route)
        {
            if (link.MessagesPerTick > 0)
            {
                linkUsage[link.Id] = linkUsage.GetValueOrDefault(link.Id) + 1;
            }
        }

        entity.Energy -= cost;
        World.Statistics.EnergySpentTotal += cost;

        var messageId = World.NextMessageId++;
        var latency = Math.Max(1, entity.Comm.LatencyTicks + route.Sum(link => link.LatencyTicks));
        var envelope = new MessageEnvelope(
            messageId,
            entity.Id,
            entity.CivilizationId,
            intent.ToCivilizationId,
            targetRegionId,
            intent.Topic,
            intent.Payload ?? string.Empty,
            tick,
            tick + latency);

        World.InFlightMessages.Add(envelope);

        events.Add(new MessageQueued(
            tick,
            messageId,
            entity.Id,
            intent.ToCivilizationId,
            targetRegionId,
            intent.Topic,
            envelope.DeliverTick,
            cost,
            route.Count == 0 ? "local" : string.Join(">", route.Select(link => link.Id))));

        Accept(intent, $"Message {messageId} queued for tick {envelope.DeliverTick}.", results);
    }

    private void ProcessTransferMaterial(
        EntityState entity,
        RegionState sourceRegion,
        TransferMaterialIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results,
        Dictionary<string, int> materialLinkUsage)
    {
        if (entity.Drive is null)
        {
            Reject(intent, SimulationErrorCode.NoDrive, "Entity has no drive module.", tick, events, results);
            return;
        }

        if (intent.Amount <= 0)
        {
            Reject(intent, SimulationErrorCode.InvalidAction, "Transfer amount must be positive.", tick, events, results);
            return;
        }

        if (string.Equals(intent.TargetRegionId, sourceRegion.Id, StringComparison.Ordinal))
        {
            Reject(
                intent,
                SimulationErrorCode.InvalidAction,
                "Material transfer target must be a different region.",
                tick,
                events,
                results);
            return;
        }

        if (!World.Regions.ContainsKey(intent.TargetRegionId))
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"Target region '{intent.TargetRegionId}' does not exist.",
                tick,
                events,
                results);
            return;
        }

        var path = FindMessagePath(sourceRegion.Id, intent.TargetRegionId);
        if (path is null)
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"No link path from '{sourceRegion.Id}' to '{intent.TargetRegionId}'.",
                tick,
                events,
                results);
            return;
        }

        foreach (var link in path)
        {
            if (link.MaterialPerTick > 0 &&
                materialLinkUsage.GetValueOrDefault(link.Id) >= link.MaterialPerTick)
            {
                Reject(
                    intent,
                    SimulationErrorCode.NoBandwidth,
                    $"Link '{link.Id}' material capacity is exhausted for this tick.",
                    tick,
                    events,
                    results);
                return;
            }
        }

        if (!sourceRegion.TryConsumeMaterial(entity.CivilizationId, intent.Amount))
        {
            Reject(
                intent,
                SimulationErrorCode.InsufficientMaterial,
                $"Region '{sourceRegion.Id}' has {sourceRegion.GetMaterial(entity.CivilizationId)} M; " +
                $"transfer needs {intent.Amount}.",
                tick,
                events,
                results);
            return;
        }

        var route = string.Join(">", path.Select(link => link.Id));
        var shipmentId = World.NextShipmentId++;
        var arriveTick = tick + Math.Max(1, path.Sum(link => link.LatencyTicks));
        var shipment = new MaterialShipment(
            shipmentId,
            entity.Id,
            entity.CivilizationId,
            sourceRegion.Id,
            intent.TargetRegionId,
            route,
            intent.Amount,
            tick,
            arriveTick);

        foreach (var link in path)
        {
            if (link.MaterialPerTick > 0)
            {
                materialLinkUsage[link.Id] = materialLinkUsage.GetValueOrDefault(link.Id) + 1;
            }
        }

        World.InTransitMaterial.Add(shipment);
        World.Statistics.MaterialShippedTotal += intent.Amount;

        events.Add(new MaterialShipped(
            tick,
            shipmentId,
            entity.Id,
            sourceRegion.Id,
            intent.TargetRegionId,
            intent.Amount,
            arriveTick,
            route));

        Accept(
            intent,
            $"Shipment {shipmentId} queued: {intent.Amount} M arrives at tick {arriveTick}.",
            results);
    }

    private void DeliverDueMaterialShipments(long tick, List<SimulationEvent> events)
    {
        if (World.InTransitMaterial.Count == 0)
        {
            return;
        }

        var due = World.InTransitMaterial
            .Where(shipment => shipment.ArriveTick <= tick)
            .OrderBy(shipment => shipment.ArriveTick)
            .ThenBy(shipment => shipment.Id)
            .ToList();

        foreach (var shipment in due)
        {
            World.InTransitMaterial.Remove(shipment);

            if (!World.Regions.TryGetValue(shipment.ToRegionId, out var destination))
            {
                continue;
            }

            destination.AddMaterial(shipment.CivilizationId, shipment.Amount);
            World.Statistics.MaterialDeliveredTotal += shipment.Amount;
            events.Add(new MaterialArrived(
                tick,
                shipment.Id,
                shipment.ToRegionId,
                shipment.Amount));
        }
    }

    private void ProcessSendEnergy(
        EntityState entity,
        RegionState sourceRegion,
        SendEnergyIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results,
        Dictionary<string, int> energyLinkUsage)
    {
        if (entity.Power is null)
        {
            Reject(intent, SimulationErrorCode.NoPower, "Entity has no power module.", tick, events, results);
            return;
        }

        if (intent.Amount <= 0)
        {
            Reject(intent, SimulationErrorCode.InvalidAction, "Energy amount must be positive.", tick, events, results);
            return;
        }

        var targetRegionId = string.IsNullOrWhiteSpace(intent.TargetRegionId)
            ? sourceRegion.Id
            : intent.TargetRegionId;

        if (!World.Regions.ContainsKey(targetRegionId))
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"Target region '{targetRegionId}' does not exist.",
                tick,
                events,
                results);
            return;
        }

        if (entity.Energy < intent.Amount)
        {
            AddPowerShortfall(
                entity,
                sourceRegion.Id,
                intent.Amount,
                entity.Energy,
                "local",
                tick,
                events);
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough local energy.", tick, events, results);
            return;
        }

        if (string.Equals(targetRegionId, sourceRegion.Id, StringComparison.Ordinal))
        {
            entity.Energy -= intent.Amount;
            sourceRegion.AddEnergy(entity.CivilizationId, intent.Amount);
            World.Statistics.EnergyStoredTotal += intent.Amount;
            events.Add(new EnergyStored(tick, entity.Id, sourceRegion.Id, intent.Amount));
            Accept(intent, $"Stored {intent.Amount} E in the local grid.", results);
            return;
        }

        var path = FindMessagePath(sourceRegion.Id, targetRegionId);
        if (path is null)
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"No link path from '{sourceRegion.Id}' to '{targetRegionId}'.",
                tick,
                events,
                results);
            return;
        }

        foreach (var link in path)
        {
            if (link.EnergyPerTick > 0 &&
                energyLinkUsage.GetValueOrDefault(link.Id) >= link.EnergyPerTick)
            {
                Reject(
                    intent,
                    SimulationErrorCode.NoBandwidth,
                    $"Link '{link.Id}' energy capacity is exhausted for this tick.",
                    tick,
                    events,
                    results);
                return;
            }
        }

        var route = string.Join(">", path.Select(link => link.Id));
        var shipmentId = World.NextEnergyShipmentId++;
        var arriveTick = tick + Math.Max(1, path.Sum(link => link.LatencyTicks));

        entity.Energy -= intent.Amount;
        foreach (var link in path)
        {
            if (link.EnergyPerTick > 0)
            {
                energyLinkUsage[link.Id] = energyLinkUsage.GetValueOrDefault(link.Id) + 1;
            }
        }

        World.InTransitEnergy.Add(new EnergyShipment(
            shipmentId,
            entity.Id,
            entity.CivilizationId,
            sourceRegion.Id,
            targetRegionId,
            route,
            intent.Amount,
            tick,
            arriveTick));

        World.Statistics.EnergyShippedTotal += intent.Amount;
        events.Add(new EnergyShipped(
            tick,
            shipmentId,
            entity.Id,
            sourceRegion.Id,
            targetRegionId,
            intent.Amount,
            arriveTick,
            route));

        Accept(
            intent,
            $"Energy shipment {shipmentId} queued: {intent.Amount} E arrives at tick {arriveTick}.",
            results);
    }

    private void ProcessDrawEnergy(
        EntityState entity,
        RegionState region,
        DrawEnergyIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results)
    {
        if (entity.Power is null)
        {
            Reject(intent, SimulationErrorCode.NoPower, "Entity has no power module.", tick, events, results);
            return;
        }

        if (intent.Amount <= 0)
        {
            Reject(intent, SimulationErrorCode.InvalidAction, "Energy amount must be positive.", tick, events, results);
            return;
        }

        if (entity.EnergyCapacity - entity.Energy < intent.Amount)
        {
            Reject(
                intent,
                SimulationErrorCode.InvalidAction,
                "Local storage does not have enough headroom.",
                tick,
                events,
                results);
            return;
        }

        var reserve = PowerReserve(entity.CivilizationId);
        var gridEnergy = region.GetEnergy(entity.CivilizationId);
        var availableAboveReserve = Math.Max(0, gridEnergy - reserve);
        if (availableAboveReserve < intent.Amount ||
            !region.TryConsumeEnergy(entity.CivilizationId, intent.Amount))
        {
            AddPowerShortfall(
                entity,
                region.Id,
                intent.Amount,
                availableAboveReserve,
                reserve > 0 && gridEnergy < intent.Amount + reserve
                    ? "reserve"
                    : "grid",
                tick,
                events);
            Reject(
                intent,
                SimulationErrorCode.NoEnergy,
                reserve > 0 && gridEnergy < intent.Amount + reserve
                    ? $"Region '{region.Id}' grid reserve leaves insufficient energy."
                    : $"Region '{region.Id}' grid has insufficient energy.",
                tick,
                events,
                results);
            return;
        }

        entity.Energy += intent.Amount;
        World.Statistics.EnergyDrawnTotal += intent.Amount;
        events.Add(new EnergyDrawn(tick, entity.Id, region.Id, intent.Amount));
        Accept(intent, $"Drew {intent.Amount} E from the local grid.", results);
    }

    private void DeliverDueEnergyShipments(long tick, List<SimulationEvent> events)
    {
        if (World.InTransitEnergy.Count == 0)
        {
            return;
        }

        var due = World.InTransitEnergy
            .Where(shipment => shipment.ArriveTick <= tick)
            .OrderBy(shipment => shipment.ArriveTick)
            .ThenBy(shipment => shipment.Id)
            .ToList();

        foreach (var shipment in due)
        {
            World.InTransitEnergy.Remove(shipment);

            if (!World.Regions.TryGetValue(shipment.ToRegionId, out var destination))
            {
                continue;
            }

            destination.AddEnergy(shipment.CivilizationId, shipment.Amount);
            World.Statistics.EnergyDeliveredTotal += shipment.Amount;
            events.Add(new EnergyArrived(
                tick,
                shipment.Id,
                shipment.ToRegionId,
                shipment.Amount));
        }
    }

    private void EvaluateControlPoints(long tick, List<SimulationEvent> events)
    {
        if (World.ControlPoints.Count == 0 ||
            World.Match?.IsFinished == true)
        {
            return;
        }

        foreach (var point in World.ControlPoints.Values)
        {
            if (point.IsCaptured ||
                !World.Regions.TryGetValue(point.RegionId, out var region))
            {
                continue;
            }

            var present = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var entity in region.Entities.Values)
            {
                if (entity.Position.IsWithinRange(point.Position, point.RadiusMm))
                {
                    present.Add(entity.CivilizationId);
                }
            }

            if (present.Count != 1)
            {
                point.HoldingCivilizationId = null;
                point.HoldTicks = 0;
                continue;
            }

            var civilizationId = present.Min!;
            if (string.Equals(
                    point.HoldingCivilizationId,
                    civilizationId,
                    StringComparison.Ordinal))
            {
                point.HoldTicks += 1;
            }
            else
            {
                point.HoldingCivilizationId = civilizationId;
                point.HoldTicks = 1;
            }

            if (point.HoldTicks < point.HoldTicksRequired)
            {
                continue;
            }

            point.OwnerCivilizationId = civilizationId;
            events.Add(new ControlPointCaptured(
                tick,
                point.Id,
                civilizationId,
                point.HoldTicks));
        }

        if (World.Match is not { IsFinished: false } match)
        {
            return;
        }

        var captured = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var point in World.ControlPoints.Values)
        {
            if (point.OwnerCivilizationId is { } owner)
            {
                captured[owner] = captured.GetValueOrDefault(owner) + 1;
            }
        }

        foreach (var pair in captured)
        {
            if (pair.Value < match.Rules.CapturedPointsToWin)
            {
                continue;
            }

            match.WinnerCivilizationId = pair.Key;
            events.Add(new MatchEnded(tick, pair.Key, null, pair.Value));
            break;
        }
    }

    private void DeliverDuePowerShipments(long tick, List<SimulationEvent> events)
    {
        if (World.InTransitPower.Count == 0)
        {
            return;
        }

        var due = World.InTransitPower
            .Where(shipment => shipment.ArriveTick <= tick)
            .OrderBy(shipment => shipment.ArriveTick)
            .ThenBy(shipment => shipment.Id)
            .ToList();

        foreach (var shipment in due)
        {
            World.InTransitPower.Remove(shipment);
            var lost = shipment.Amount - shipment.DeliveredAmount;

            if (!World.Regions.TryGetValue(shipment.ToRegionId, out var destination))
            {
                World.Statistics.EnergyFlowLossTotal += shipment.Amount;
                events.Add(new PowerArrived(
                    tick,
                    shipment.Id,
                    shipment.CivilizationId,
                    shipment.ToRegionId,
                    0,
                    shipment.Amount));
                continue;
            }

            destination.AddEnergy(
                shipment.CivilizationId,
                shipment.DeliveredAmount);
            World.Statistics.EnergyFlowedTotal += shipment.DeliveredAmount;
            World.Statistics.EnergyFlowLossTotal += lost;
            events.Add(new PowerArrived(
                tick,
                shipment.Id,
                shipment.CivilizationId,
                shipment.ToRegionId,
                shipment.DeliveredAmount,
                lost));
        }
    }

    private void DeliverDueConstructions(long tick, List<SimulationEvent> events)
    {
        if (World.ConstructionOrders.Count == 0)
        {
            return;
        }

        var due = World.ConstructionOrders
            .Where(order => order.CompleteTick <= tick)
            .OrderBy(order => order.CompleteTick)
            .ThenBy(order => order.Id)
            .ToList();

        foreach (var order in due)
        {
            World.ConstructionOrders.Remove(order);
            var blueprint = World.Workshop.GetVersion(
                order.BlueprintId,
                order.BlueprintVersion);
            if (blueprint is null)
            {
                var refunded = RefundConstruction(order);
                events.Add(new ConstructionFailed(
                    tick,
                    order.Id,
                    order.CivilizationId,
                    order.BlueprintId,
                    "Blueprint version is missing.",
                    refunded));
                continue;
            }

            try
            {
                DeploymentService.Validate(
                    World,
                    blueprint,
                    order.CivilizationId,
                    order.RegionId,
                    order.Position);
                var entity = DeploymentService.CreateInstance(
                    World,
                    blueprint,
                    order.CivilizationId,
                    order.RegionId,
                    order.Position);
                World.Statistics.ConstructionsCompletedTotal += 1;
                events.Add(new EntityDeployed(
                    tick,
                    entity.Id,
                    order.CivilizationId,
                    blueprint.BlueprintId,
                    blueprint.Version,
                    entity.RegionId,
                    entity.Position));
            }
            catch (BlueprintValidationException exception)
            {
                var refunded = RefundConstruction(order);
                events.Add(new ConstructionFailed(
                    tick,
                    order.Id,
                    order.CivilizationId,
                    order.BlueprintId,
                    exception.Message,
                    refunded));
            }
        }
    }

    private long RefundConstruction(ConstructionOrder order)
    {
        if (!World.Regions.TryGetValue(order.RegionId, out var region))
        {
            return 0;
        }

        region.AddMaterial(order.CivilizationId, order.MaterialPaid);
        World.Statistics.MaterialRefundedTotal += order.MaterialPaid;
        return order.MaterialPaid;
    }

    private bool HasCommEndpoint(string regionId, string civilizationId) =>
        World.Regions[regionId].Entities.Values.Any(entity =>
            string.Equals(entity.CivilizationId, civilizationId, StringComparison.Ordinal) &&
            entity.Comm is not null);

    /// <summary>
    /// Bidirectional BFS. Each level is expanded from the smaller frontier so dense
    /// graphs do not blow up one side. Neighbour order is deterministic by link id.
    /// On meeting, the forward half is reversed and the backward half is appended.
    /// </summary>
    private IReadOnlyList<RegionLink>? FindMessagePath(string sourceRegionId, string targetRegionId)
    {
        if (string.Equals(sourceRegionId, targetRegionId, StringComparison.Ordinal))
        {
            return Array.Empty<RegionLink>();
        }

        var forwardVisited = new HashSet<string>(StringComparer.Ordinal) { sourceRegionId };
        var backwardVisited = new HashSet<string>(StringComparer.Ordinal) { targetRegionId };
        var forwardFrontier = new List<string> { sourceRegionId };
        var backwardFrontier = new List<string> { targetRegionId };
        var forwardParents =
            new Dictionary<string, (string Parent, RegionLink Link)>(StringComparer.Ordinal);
        var backwardParents =
            new Dictionary<string, (string Next, RegionLink Link)>(StringComparer.Ordinal);

        while (forwardFrontier.Count > 0 && backwardFrontier.Count > 0)
        {
            var expandForward = forwardFrontier.Count <= backwardFrontier.Count;
            var meeting = expandForward
                ? ExpandFrontier(
                    forwardFrontier,
                    forwardVisited,
                    backwardVisited,
                    forwardParents)
                : ExpandFrontier(
                    backwardFrontier,
                    backwardVisited,
                    forwardVisited,
                    backwardParents);

            if (meeting is not null)
            {
                return BuildPath(
                    meeting,
                    sourceRegionId,
                    targetRegionId,
                    forwardParents,
                    backwardParents);
            }
        }

        return null;
    }

    private string? ExpandFrontier(
        List<string> frontier,
        HashSet<string> ownVisited,
        HashSet<string> otherVisited,
        Dictionary<string, (string Parent, RegionLink Link)> parents)
    {
        var nextFrontier = new List<string>();

        foreach (var regionId in frontier)
        {
            foreach (var link in World.Links.Values
                         .Where(link => link.Connects(regionId))
                         .OrderBy(link => link.Id, StringComparer.Ordinal))
            {
                var next = link.Other(regionId);
                if (!ownVisited.Add(next))
                {
                    continue;
                }

                parents[next] = (regionId, link);

                if (otherVisited.Contains(next))
                {
                    return next;
                }

                nextFrontier.Add(next);
            }
        }

        frontier.Clear();
        frontier.AddRange(nextFrontier);
        return null;
    }

    private static IReadOnlyList<RegionLink> BuildPath(
        string meetingRegionId,
        string sourceRegionId,
        string targetRegionId,
        Dictionary<string, (string Parent, RegionLink Link)> forwardParents,
        Dictionary<string, (string Next, RegionLink Link)> backwardParents)
    {
        var path = new List<RegionLink>();

        var current = meetingRegionId;
        while (!string.Equals(current, sourceRegionId, StringComparison.Ordinal))
        {
            var parent = forwardParents[current];
            path.Add(parent.Link);
            current = parent.Parent;
        }

        path.Reverse();

        current = meetingRegionId;
        while (!string.Equals(current, targetRegionId, StringComparison.Ordinal))
        {
            var next = backwardParents[current];
            path.Add(next.Link);
            current = next.Next;
        }

        return path;
    }

    private void DeliverDueMessages(long tick, List<SimulationEvent> events)
    {
        if (World.InFlightMessages.Count == 0)
        {
            return;
        }

        var due = World.InFlightMessages
            .Where(message => message.DeliverTick <= tick)
            .OrderBy(message => message.DeliverTick)
            .ThenBy(message => message.Id)
            .ToList();

        foreach (var message in due)
        {
            World.InFlightMessages.Remove(message);

            if (!World.Inboxes.TryGetValue(message.ToCivilizationId, out var inbox))
            {
                inbox = new List<DeliveredMessage>();
                World.Inboxes.Add(message.ToCivilizationId, inbox);
            }

            var (hasStorage, capacity) = GetMessageStorage(message.ToCivilizationId);
            if (hasStorage)
            {
                if (capacity <= 0)
                {
                    World.Statistics.MessagesExpired += 1;
                    events.Add(new MessageExpired(
                        tick,
                        message.Id,
                        message.ToCivilizationId,
                        "storage capacity is zero"));
                    continue;
                }

                while (inbox.Count >= capacity)
                {
                    var expired = inbox[0];
                    inbox.RemoveAt(0);
                    World.Statistics.MessagesExpired += 1;
                    events.Add(new MessageExpired(
                        tick,
                        expired.Id,
                        message.ToCivilizationId,
                        "inbox storage full"));
                }
            }

            inbox.Add(new DeliveredMessage(
                message.Id,
                message.FromEntityId,
                message.FromCivilizationId,
                message.TargetRegionId,
                message.Topic,
                message.Payload,
                message.SentTick,
                tick));

            World.Statistics.MessagesDelivered += 1;
            events.Add(new MessageDelivered(
                tick,
                message.Id,
                message.FromEntityId,
                message.ToCivilizationId,
                message.TargetRegionId,
                message.Topic));
        }
    }

    /// <summary>
    /// Storage S is the sum of own State module capacities. No State module means the
    /// legacy unlimited inbox; a zero-capacity module retains no history.
    /// </summary>
    private (bool HasStorage, int Capacity) GetMessageStorage(string civilizationId)
    {
        var hasStorage = false;
        var capacity = 0;

        foreach (var entity in World.EnumerateEntities())
        {
            if (!string.Equals(entity.CivilizationId, civilizationId, StringComparison.Ordinal) ||
                entity.State is null)
            {
                continue;
            }

            hasStorage = true;
            capacity += Math.Max(0, entity.State.StorageCapacity);
        }

        return (hasStorage, capacity);
    }

    private void RegenerateCores(long tick, List<SimulationEvent> events)
    {
        foreach (var core in World.Cores.Values)
        {
            var energyRoom = Math.Max(0, core.Config.MaxEnergyReserve - core.EnergyReserve);
            var materialRoom = Math.Max(0, core.Config.MaxMaterialReserve - core.MaterialReserve);

            var energyAdded = Math.Min(core.Config.EnergyPerTick, energyRoom);
            var materialAdded = Math.Min(core.Config.MaterialPerTick, materialRoom);

            if (energyAdded <= 0 && materialAdded <= 0)
            {
                continue;
            }

            core.EnergyReserve += energyAdded;
            core.MaterialReserve += materialAdded;
            World.Statistics.EnergyRegeneratedTotal += energyAdded;
            World.Statistics.MaterialRegeneratedTotal += materialAdded;

            events.Add(new CoreRegenerated(tick, core.CivilizationId, energyAdded, materialAdded));
        }
    }

    private static void ValidateBlueprint(RecoveryBlueprint blueprint)
    {
        if (blueprint.MaxHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blueprint), "Blueprint MaxHealth must be positive.");
        }

        if (blueprint.RequiredEnergy < 0 || blueprint.RequiredMaterial < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blueprint), "Recovery costs must be non-negative.");
        }

        if (blueprint.Energy < 0 || blueprint.Energy > blueprint.EnergyCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(blueprint), "Blueprint energy must be within capacity.");
        }
    }

    private static BootstrapResult BootstrapFailure(SimulationErrorCode code, string message) =>
        new(false, null, code, message, null);

    private long PowerReserve(string civilizationId) =>
        World.PowerPolicies.TryGetValue(civilizationId, out var policy)
            ? policy.ReserveEnergy
            : 0;

    private bool TryAutoDrawForAction(
        EntityState entity,
        RegionState region,
        long cost,
        long tick,
        List<SimulationEvent> events)
    {
        if (entity.Energy >= cost)
        {
            return true;
        }

        if (entity.Power is null)
        {
            return false;
        }

        var reserve = PowerReserve(entity.CivilizationId);
        var available = Math.Max(
            0,
            region.GetEnergy(entity.CivilizationId) - reserve);
        var needed = cost - entity.Energy;
        var headroom = entity.EnergyCapacity - entity.Energy;
        if (needed > headroom ||
            entity.Energy + available < cost)
        {
            return false;
        }

        var amount = Math.Min(needed, available);
        if (amount <= 0 ||
            !region.TryConsumeEnergy(entity.CivilizationId, amount))
        {
            return false;
        }

        entity.Energy += amount;
        World.Statistics.EnergyDrawnTotal += amount;
        World.Statistics.EnergyAutoDrawnTotal += amount;
        events.Add(new EnergyAutoDrawn(
            tick,
            entity.Id,
            region.Id,
            amount));
        return entity.Energy >= cost;
    }

    private long ActionAvailableEnergy(EntityState entity, RegionState region)
    {
        if (entity.Power is null)
        {
            return entity.Energy;
        }

        var reserve = PowerReserve(entity.CivilizationId);
        return entity.Energy + Math.Max(
            0,
            region.GetEnergy(entity.CivilizationId) - reserve);
    }

    private string ActionShortfallSource(
        EntityState entity,
        RegionState region,
        long requested)
    {
        if (entity.Power is null)
        {
            return "local";
        }

        var grid = region.GetEnergy(entity.CivilizationId);
        if (grid <= 0)
        {
            return "local";
        }

        var reserve = PowerReserve(entity.CivilizationId);
        return reserve > 0 && grid < requested + reserve
            ? "reserve"
            : "grid";
    }

    private static void AddPowerShortfall(
        EntityState entity,
        string regionId,
        long requested,
        long available,
        string source,
        long tick,
        List<SimulationEvent> events) =>
        events.Add(new PowerShortfall(
            tick,
            entity.CivilizationId,
            regionId,
            entity.Id,
            requested,
            available,
            source));

    private static void Accept(Intent intent, string message, List<IntentResult> results) =>
        results.Add(new IntentResult(intent, true, SimulationErrorCode.None, message));

    private static void Reject(
        Intent intent,
        SimulationErrorCode code,
        string message,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results)
    {
        results.Add(new IntentResult(intent, false, code, message));
        events.Add(new IntentRejected(tick, intent.EntityId, code, message));
    }
}
