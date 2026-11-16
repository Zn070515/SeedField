namespace SeedField.Simulation;

/// <summary>
/// The single authority for world mutation. Pipeline per tick:
/// intents (stable order) -> advance tick -> deliver due messages -> regenerate cores.
/// A message sent on tick T is available at the earliest in the observation for tick
/// T+1, never in the same tick.
/// </summary>
public sealed class SimulationEngine
{
    public SimulationEngine(WorldState world)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
    }

    public WorldState World { get; }

    public Observation BuildObservation(string civilizationId) =>
        ObservationBuilder.Build(World, civilizationId);

    public TickResult Step(IReadOnlyList<Intent>? intents = null)
    {
        var tick = World.Tick;
        var events = new List<SimulationEvent>();
        var results = new List<IntentResult>();
        var computeUsed = new Dictionary<EntityId, int>();
        var bandwidthUsed = new Dictionary<EntityId, int>();

        GeneratePower();

        if (intents is { Count: > 0 })
        {
            // Entity ids are globally unique and monotonic. Sorting by id gives a
            // canonical order regardless of how controllers were concatenated.
            foreach (var intent in intents.OrderBy(intent => intent.EntityId.Value))
            {
                ProcessIntent(intent, tick, events, results, computeUsed, bandwidthUsed);
            }
        }

        World.Tick = tick + 1;

        DeliverDueMessages(World.Tick, events);
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

    private void ProcessIntent(
        Intent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results,
        Dictionary<EntityId, int> computeUsed,
        Dictionary<EntityId, int> bandwidthUsed)
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
                ProcessSendMessage(entity, send, tick, events, results, bandwidthUsed);
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
        foreach (var entity in World.EnumerateEntities())
        {
            if (entity.Power is null)
            {
                continue;
            }

            var room = Math.Max(0, entity.EnergyCapacity - entity.Energy);
            var added = Math.Min(Math.Max(0, entity.Power.EnergyPerTick), room);
            if (added <= 0)
            {
                continue;
            }

            entity.Energy += added;
            World.Statistics.EnergyGeneratedTotal += added;
        }
    }

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
        if (entity.Energy < cost)
        {
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough local energy to move.", tick, events, results);
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
        if (entity.Energy < cost)
        {
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough local energy to fire.", tick, events, results);
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
            events.Add(new EntityDestroyed(tick, target.Id));
        }

        Accept(intent, $"Hit {target.Id} for {appliedDamage}.", results);
    }

    private void ProcessSendMessage(
        EntityState entity,
        SendMessageIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results,
        Dictionary<EntityId, int> bandwidthUsed)
    {
        if (entity.Comm is null)
        {
            Reject(intent, SimulationErrorCode.NoComm, "Entity has no communications module.", tick, events, results);
            return;
        }

        if (!string.Equals(intent.ToCivilizationId, entity.CivilizationId, StringComparison.Ordinal))
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                "This slice only routes messages inside the sender's civilization.",
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

        var cost = entity.Comm.EnergyCostPerMessage;
        if (entity.Energy < cost)
        {
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough local energy to send.", tick, events, results);
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

        entity.Energy -= cost;
        World.Statistics.EnergySpentTotal += cost;

        var messageId = World.NextMessageId++;
        var latency = Math.Max(1, entity.Comm.LatencyTicks);
        var envelope = new MessageEnvelope(
            messageId,
            entity.Id,
            entity.CivilizationId,
            intent.ToCivilizationId,
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
            intent.Topic,
            envelope.DeliverTick,
            cost));

        Accept(intent, $"Message {messageId} queued for tick {envelope.DeliverTick}.", results);
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

            inbox.Add(new DeliveredMessage(
                message.Id,
                message.FromEntityId,
                message.FromCivilizationId,
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
                message.Topic));
        }
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
