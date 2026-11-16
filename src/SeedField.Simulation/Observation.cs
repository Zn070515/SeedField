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
    bool IsSelf,
    bool HasSensor,
    int SensorRangeMm,
    bool HasWeapon,
    int WeaponRangeMm,
    bool HasDrive,
    bool HasComm);

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

public sealed record ObservedControlPoint(
    string Id,
    string RegionId,
    Vec2 Position,
    int RadiusMm,
    int HoldTicksRequired,
    string? HoldingCivilizationId,
    int HoldTicks,
    string? OwnerCivilizationId);

public sealed record ObservedMatch(
    int CapturedPointsToWin,
    string? WinnerCivilizationId);

public sealed record Observation(
    long Tick,
    string CivilizationId,
    IReadOnlyList<ObservedEntity> Entities,
    IReadOnlyList<ObservedMessage> Messages,
    IReadOnlyList<ObservedShipment> Shipments,
    IReadOnlyList<ObservedEnergyShipment> EnergyShipments,
    long? ViewerEntityId = null,
    IReadOnlyList<ObservedControlPoint>? ControlPoints = null,
    ObservedMatch? Match = null);
