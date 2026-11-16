namespace SeedField.Simulation;

/// <summary>
/// Read-only facts produced by the simulation. Presentation may render these but may
/// never substitute its own truth for them.
/// </summary>
public abstract record SimulationEvent(long Tick);

public sealed record EntityMoved(
    long Tick,
    EntityId EntityId,
    Vec2 From,
    Vec2 To,
    long EnergySpent) : SimulationEvent(Tick);

public sealed record ShotFired(
    long Tick,
    EntityId AttackerId,
    EntityId TargetId,
    int Damage,
    Vec2 From,
    Vec2 Target,
    long EnergySpent) : SimulationEvent(Tick);

public sealed record EntityDamaged(
    long Tick,
    EntityId EntityId,
    int Damage,
    int HealthRemaining) : SimulationEvent(Tick);

public sealed record EntityDestroyed(long Tick, EntityId EntityId) : SimulationEvent(Tick);

public sealed record MessageQueued(
    long Tick,
    long MessageId,
    EntityId FromEntityId,
    string ToCivilizationId,
    string TargetRegionId,
    string Topic,
    long DeliverTick,
    long EnergySpent,
    string Route) : SimulationEvent(Tick);

public sealed record MessageDelivered(
    long Tick,
    long MessageId,
    EntityId FromEntityId,
    string ToCivilizationId,
    string TargetRegionId,
    string Topic) : SimulationEvent(Tick);

public sealed record MessageExpired(
    long Tick,
    long MessageId,
    string CivilizationId,
    string Reason) : SimulationEvent(Tick);

public sealed record IntentRejected(
    long Tick,
    EntityId EntityId,
    SimulationErrorCode ErrorCode,
    string Detail) : SimulationEvent(Tick);

public sealed record CoreRegenerated(
    long Tick,
    string CivilizationId,
    long EnergyAdded,
    long MaterialAdded) : SimulationEvent(Tick);

public sealed record EntityBootstrapped(
    long Tick,
    EntityId EntityId,
    string CivilizationId,
    string BlueprintName,
    long EnergySpent,
    long MaterialSpent) : SimulationEvent(Tick);
