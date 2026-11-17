namespace SeedField.Simulation;

/// <summary>
/// Automatic power committed to a delayed grid path. The source stock is debited when
/// the shipment is created; DeliveredAmount is credited on ArriveTick.
/// </summary>
public sealed record PowerShipment(
    long Id,
    string CivilizationId,
    string FromRegionId,
    string ToRegionId,
    string Route,
    long Amount,
    long DeliveredAmount,
    long SentTick,
    long ArriveTick);
