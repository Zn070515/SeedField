namespace SeedField.Simulation;

/// <summary>
/// What one civilization is allowed to know at a tick. Deliberately a serialisable
/// snapshot with no reference back into <see cref="WorldState"/>.
/// </summary>
public sealed record ObservedEntity(
    long EntityId,
    string CivilizationId,
    string RegionId,
    Vec2 Position,
    int Health,
    int MaxHealth,
    bool IsSelf);

public sealed record ObservedMessage(
    long MessageId,
    long FromEntityId,
    string FromCivilizationId,
    string TargetRegionId,
    string Topic,
    string Payload,
    long SentTick,
    long DeliveredTick);

public sealed record ObservedShipment(
    long ShipmentId,
    string FromRegionId,
    string ToRegionId,
    long Amount,
    long SentTick,
    long ArriveTick,
    string Route);

public sealed record ObservedEnergyShipment(
    long ShipmentId,
    string FromRegionId,
    string ToRegionId,
    long Amount,
    long SentTick,
    long ArriveTick,
    string Route);

public sealed record Observation(
    long Tick,
    string CivilizationId,
    IReadOnlyList<ObservedEntity> Entities,
    IReadOnlyList<ObservedMessage> Messages,
    IReadOnlyList<ObservedShipment> Shipments,
    IReadOnlyList<ObservedEnergyShipment> EnergyShipments);
