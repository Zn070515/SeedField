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

public sealed record EntityInventoryLost(
    long Tick,
    EntityId EntityId,
    long Energy,
    long Material) : SimulationEvent(Tick);

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

public sealed record ControlPointCaptured(
    long Tick,
    string ControlPointId,
    string CivilizationId,
    int HoldTicks) : SimulationEvent(Tick);

public sealed record MatchEnded(
    long Tick,
    string CivilizationId,
    string? ControlPointId,
    int CapturedPoints) : SimulationEvent(Tick);

public sealed record MaterialShipped(
    long Tick,
    long ShipmentId,
    EntityId FromEntityId,
    string FromRegionId,
    string ToRegionId,
    long Amount,
    long ArriveTick,
    string Route) : SimulationEvent(Tick);

public sealed record EnergyStored(
    long Tick,
    EntityId EntityId,
    string RegionId,
    long Amount) : SimulationEvent(Tick);

public sealed record EnergyShipped(
    long Tick,
    long ShipmentId,
    EntityId FromEntityId,
    string FromRegionId,
    string ToRegionId,
    long Amount,
    long ArriveTick,
    string Route) : SimulationEvent(Tick);

public sealed record EnergyArrived(
    long Tick,
    long ShipmentId,
    string ToRegionId,
    long Amount) : SimulationEvent(Tick);

public sealed record EnergyDrawn(
    long Tick,
    EntityId EntityId,
    string RegionId,
    long Amount) : SimulationEvent(Tick);

public sealed record MaterialArrived(
    long Tick,
    long ShipmentId,
    string ToRegionId,
    long Amount) : SimulationEvent(Tick);

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

public sealed record EntityDeployed(
    long Tick,
    EntityId EntityId,
    string CivilizationId,
    string BlueprintId,
    int BlueprintVersion,
    string RegionId,
    Vec2 Position) : SimulationEvent(Tick);

public sealed record ConstructionQueued(
    long Tick,
    long OrderId,
    string CivilizationId,
    string BlueprintId,
    int BlueprintVersion,
    string RegionId,
    Vec2 Position,
    long CompleteTick) : SimulationEvent(Tick);

public sealed record ConstructionCancelled(
    long Tick,
    long OrderId,
    string CivilizationId,
    string BlueprintId,
    long MaterialRefunded) : SimulationEvent(Tick);

public sealed record ConstructionFailed(
    long Tick,
    long OrderId,
    string CivilizationId,
    string BlueprintId,
    string Reason,
    long MaterialRefunded) : SimulationEvent(Tick);

public sealed record EntityUpgraded(
    long Tick,
    EntityId EntityId,
    string CivilizationId,
    string BlueprintId,
    int OldVersion,
    int NewVersion) : SimulationEvent(Tick);

public sealed record EntityBootstrapped(
    long Tick,
    EntityId EntityId,
    string CivilizationId,
    string BlueprintName,
    long EnergySpent,
    long MaterialSpent) : SimulationEvent(Tick);
