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

    /// <summary>
    /// Offscreen controller gate: false when the entity is missing or lives in an
    /// inactive region. Inactive regions already reject intents, so skipping the
    /// script call cannot change the authoritative world state.
    /// </summary>
    public bool IsEntityActive(EntityId entityId) =>
        World.FindEntity(entityId) is { } entity && IsRegionActive(entity.RegionId);

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
        InvalidateObservationCache();

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

    private ObservationCache? _observationCache;

    public Observation BuildDeviceObservation(EntityId entityId) =>
        ObservationBuilder.BuildForEntity(World, entityId, GetObservationCache());

    /// <summary>
    /// Device-scoped view for controller execution: self plus sensed enemies.
    /// The civilization observation still includes all own entities.
    /// </summary>
    public Observation BuildControllerObservation(EntityId entityId) =>
        ObservationBuilder.BuildForEntity(
            World,
            entityId,
            GetObservationCache(),
            includeAllies: false);

    /// <summary>
    /// Builds many controller views at once. Each worker owns its own observation
    /// cache, so the shared per-tick cache (which is not thread-safe) is never
    /// touched. Results keep the caller's input order.
    /// </summary>
    public Observation[] BuildControllerObservations(
        IReadOnlyList<EntityId> entityIds,
        int maxDegreeOfParallelism = 0)
    {
        ArgumentNullException.ThrowIfNull(entityIds);
        var results = new Observation[entityIds.Count];
        if (entityIds.Count == 0)
        {
            return results;
        }

        var degree = maxDegreeOfParallelism > 0
            ? Math.Min(maxDegreeOfParallelism, entityIds.Count)
            : Math.Min(Math.Clamp(Environment.ProcessorCount, 1, 16), entityIds.Count);
        var sharedCache = GetObservationCache();
        sharedCache.PrepareForControllers();

        // A blocked thread-pool caller can starve the pool when many callers
        // parallelize at once; the game/headless main threads keep the parallel
        // path, pool callers stay sequential instead of deadlocking.
        if (degree <= 1 ||
            entityIds.Count < 8 ||
            Thread.CurrentThread.IsThreadPoolThread)
        {
            for (var index = 0; index < entityIds.Count; index++)
            {
                results[index] = ObservationBuilder.BuildForEntity(
                    World,
                    entityIds[index],
                    sharedCache,
                    includeAllies: false);
            }

            return results;
        }

        Parallel.For(
            0,
            entityIds.Count,
            new ParallelOptions { MaxDegreeOfParallelism = degree },
            () => sharedCache,
            (index, _, cache) =>
            {
                results[index] = ObservationBuilder.BuildForEntity(
                    World,
                    entityIds[index],
                    cache,
                    includeAllies: false);
                return cache;
            },
            _ => { });

        return results;
    }

    /// <summary>
    /// Per-tick cache, rebuilt after a tick advance or after any host command that
    /// mutated the world without advancing the tick (for example a deployment).
    /// Command paths call <see cref="InvalidateObservationCache"/>.
    /// </summary>
    private ObservationCache GetObservationCache()
    {
        if (_observationCache is null || _observationCache.Tick != World.Tick)
        {
            _observationCache = ObservationCache.Build(World);
        }

        return _observationCache;
    }

    private void InvalidateObservationCache() => _observationCache = null;

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
        RegenerateShields(tick);
        ProduceMaterials(tick, events);
        DiscoverDeposits();

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
    public DeploymentCommandResult ExecuteDeploymentCommand(DeploymentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        InvalidateObservationCache();
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
                        var costs = DeploymentService.ConsumeCosts(
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
                            costs.Material,
                            costs.Components));
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
