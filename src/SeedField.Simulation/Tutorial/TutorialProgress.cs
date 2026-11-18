namespace SeedField.Simulation;

/// <summary>Minimum-usable branches a player must verify before entering PvE.</summary>
public enum TutorialStep
{
    Energy = 0,
    Sensing = 1,
    Communication = 2,
    State = 3,
    Execution = 4,
}

/// <summary>Presentation-neutral description of one tutorial/challenge step.</summary>
public sealed record TutorialStepDefinition(
    TutorialStep Step,
    string Title,
    string Requirement,
    DeviceBranch Branch);

/// <summary>
/// The first M1 construction challenge set. The five entries are the confirmed
/// minimum-usable branches; they are not a fixed technology tree.
/// </summary>
public static class TutorialSteps
{
    public static IReadOnlyList<TutorialStepDefinition> All { get; } =
        new[]
        {
            new TutorialStepDefinition(
                TutorialStep.Energy,
                "Energy",
                "Deploy a design with local generation and let it charge.",
                DeviceBranch.Energy),
            new TutorialStepDefinition(
                TutorialStep.Sensing,
                "Sensing",
                "Detect a foreign entity inside a sensor's range.",
                DeviceBranch.Sensing),
            new TutorialStepDefinition(
                TutorialStep.Communication,
                "Communication",
                "Deliver a message to your civilization.",
                DeviceBranch.Communication),
            new TutorialStepDefinition(
                TutorialStep.State,
                "State",
                "Deploy a design with state storage and have it act.",
                DeviceBranch.State),
            new TutorialStepDefinition(
                TutorialStep.Execution,
                "Execution",
                "Move or fire a deployed device.",
                DeviceBranch.Execution),
        };
}

/// <summary>
/// Read-only tutorial tracker. It derives progress from the authoritative world and
/// from events the simulation already produced; it never mutates the world and is not
/// part of the world hash. Callers persist the completed ticks outside the world save.
/// </summary>
public sealed class TutorialProgress
{
    private readonly SortedDictionary<TutorialStep, long> _completed = new();

    public TutorialProgress(string civilizationId)
    {
        if (string.IsNullOrWhiteSpace(civilizationId))
        {
            throw new ArgumentException("Civilization id is required.", nameof(civilizationId));
        }

        CivilizationId = civilizationId;
    }

    public string CivilizationId { get; }

    /// <summary>Tutorial step to the first tick on which it was verified.</summary>
    public IReadOnlyDictionary<TutorialStep, long> Completed => _completed;

    public int CompletedCount => _completed.Count;

    public bool IsComplete => CompletedCount == TutorialSteps.All.Count;

    /// <summary>PvE is admitted as soon as all five minimum branches are verified.</summary>
    public bool PveUnlocked => IsComplete;

    public DeviceBranch CoveredBranches
    {
        get
        {
            var branches = DeviceBranch.None;
            foreach (var definition in TutorialSteps.All)
            {
                if (_completed.ContainsKey(definition.Step))
                {
                    branches |= definition.Branch;
                }
            }

            return branches;
        }
    }

    public bool HasCompleted(TutorialStep step) => _completed.ContainsKey(step);

    /// <summary>Returns -1 when the step has not been verified.</summary>
    public long CompletionTick(TutorialStep step) =>
        _completed.TryGetValue(step, out var tick) ? tick : -1;

    /// <summary>
    /// Marks every newly satisfied step and returns them in stable branch order.
    /// Repeated calls with the same facts are idempotent.
    /// </summary>
    public IReadOnlyList<TutorialStep> Observe(
        WorldState world,
        IReadOnlyList<SimulationEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);

        var newlyCompleted = new List<TutorialStep>(TutorialSteps.All.Count);
        foreach (var definition in TutorialSteps.All)
        {
            if (_completed.ContainsKey(definition.Step))
            {
                continue;
            }

            if (!IsSatisfied(definition.Step, world, events))
            {
                continue;
            }

            _completed.Add(definition.Step, world.Tick);
            newlyCompleted.Add(definition.Step);
        }

        return newlyCompleted;
    }

    private bool IsSatisfied(
        TutorialStep step,
        WorldState world,
        IReadOnlyList<SimulationEvent> events) =>
        step switch
        {
            TutorialStep.Energy => HasChargedEnergyEntity(world),
            TutorialStep.Sensing => HasForeignSensorContact(world),
            TutorialStep.Communication => HasDeliveredMessage(events),
            TutorialStep.State => HasStateAction(world, events),
            TutorialStep.Execution => HasExecutedAction(world, events),
            _ => false,
        };

    private bool HasChargedEnergyEntity(WorldState world)
    {
        foreach (var entity in world.EnumerateEntities())
        {
            if (!Owns(entity) || entity.Health <= 0 || entity.Energy <= 0)
            {
                continue;
            }

            if (entity.Power is { EnergyPerTick: > 0 })
            {
                return true;
            }
        }

        return false;
    }

    private bool HasForeignSensorContact(WorldState world)
    {
        foreach (var own in world.EnumerateEntities())
        {
            if (!Owns(own) || own.Health <= 0 || own.Sensor is not { RangeMm: > 0 })
            {
                continue;
            }

            foreach (var other in world.EnumerateEntities())
            {
                if (other.Health <= 0 ||
                    ReferenceEquals(own, other) ||
                    string.Equals(other.CivilizationId, CivilizationId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (VisibilityRules.IsSensedBy(own, other))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasDeliveredMessage(IReadOnlyList<SimulationEvent> events)
    {
        foreach (var simulationEvent in events)
        {
            if (simulationEvent is MessageDelivered delivered &&
                string.Equals(delivered.ToCivilizationId, CivilizationId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasStateAction(WorldState world, IReadOnlyList<SimulationEvent> events)
    {
        foreach (var simulationEvent in events)
        {
            var actor = ActorOf(simulationEvent);
            if (actor is null || !world.TryFindOwned(actor.Value, CivilizationId, out var entity))
            {
                continue;
            }

            if (entity.State is not null)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasExecutedAction(WorldState world, IReadOnlyList<SimulationEvent> events)
    {
        foreach (var simulationEvent in events)
        {
            if (simulationEvent is not (EntityMoved or ShotFired or ShotMissed))
            {
                continue;
            }

            var actor = ActorOf(simulationEvent);
            if (actor is not null && world.TryFindOwned(actor.Value, CivilizationId, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static EntityId? ActorOf(SimulationEvent simulationEvent) =>
        simulationEvent switch
        {
            EntityMoved moved => moved.EntityId,
            ShotFired fired => fired.AttackerId,
            ShotMissed missed => missed.AttackerId,
            MessageQueued queued => queued.FromEntityId,
            _ => null,
        };

    private bool Owns(EntityState entity) =>
        string.Equals(entity.CivilizationId, CivilizationId, StringComparison.Ordinal);
}

internal static class WorldStateTutorialExtensions
{
    /// <summary>
    /// Actor lookup for tutorial checks. Destroyed entities are intentionally not
    /// counted: the step needs a live device that produced the action.
    /// </summary>
    public static bool TryFindOwned(
        this WorldState world,
        EntityId id,
        string civilizationId,
        out EntityState entity)
    {
        entity = world.FindEntity(id)!;
        return entity is not null &&
               entity.Health > 0 &&
               string.Equals(entity.CivilizationId, civilizationId, StringComparison.Ordinal);
    }
}
