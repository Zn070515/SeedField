namespace SeedField.Simulation;

/// <summary>
/// The single authority for world mutation. Pipeline per tick:
/// intents (stable order) -> advance tick -> deliver due messages -> regenerate cores.
/// A message sent on tick T is available at the earliest in the observation for tick
/// T+1, never in the same tick.
/// </summary>
public sealed partial class SimulationEngine
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

        // Breaker state is passive: after catch-up, align it with the active run.
        // Catch-up events are not surfaced; state/hash equivalence is what matters.
        UpdatePowerBreakers(World.Tick, new List<SimulationEvent>());
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
                var free = region.GetGridFree(entity.CivilizationId);
                var toGrid = free == long.MaxValue
                    ? overflow
                    : Math.Min(overflow, free);
                if (toGrid > 0)
                {
                    region.AddEnergy(entity.CivilizationId, toGrid);
                    World.Statistics.EnergyOverflowedTotal += toGrid;
                }

                World.Statistics.EnergyWastedTotal += overflow - toGrid;
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
        UpdatePowerBreakers(tick, events);

        if (intents is { Count: > 0 })
        {
            // Power-consuming intents are scheduled preemptively: higher device
            // priority claims grid energy first. Entity id and input index are stable
            // tie-breakers, so equal priorities preserve the old id order. The
            // priority lookup is built once instead of rescanning every region per
            // intent in the comparator path.
            var priorities = new Dictionary<EntityId, int>();
            foreach (var entity in World.EnumerateEntities())
            {
                if (entity.Power is { } power)
                {
                    priorities[entity.Id] = power.Priority;
                }
            }

            var processingOrder = intents
                .Select((intent, index) => (
                    Intent: intent,
                    Index: index,
                    Priority: priorities.GetValueOrDefault(intent.EntityId)))
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.Intent.EntityId.Value)
                .ThenBy(item => item.Index);

            foreach (var item in processingOrder)
            {
                ProcessIntent(
                    item.Intent,
                    tick,
                    events,
                    results,
                    computeUsed,
                    bandwidthUsed,
                    linkUsage,
                    materialLinkUsage,
                    energyLinkUsage);
            }

            results = RestoreInputOrder(intents, results);
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
