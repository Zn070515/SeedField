namespace SeedField.Simulation;

/// <summary>
/// A message committed to the world's delivery queue. It must remain in flight for at
/// least one full tick; sending never delivers in the same tick.
/// </summary>
public sealed record MessageEnvelope(
    long Id,
    EntityId FromEntityId,
    string FromCivilizationId,
    string ToCivilizationId,
    string TargetRegionId,
    string Topic,
    string Payload,
    long SentTick,
    long DeliverTick);

/// <summary>Material committed to a direct region link and not yet usable remotely.</summary>
public sealed record MaterialShipment(
    long Id,
    EntityId FromEntityId,
    string CivilizationId,
    string FromRegionId,
    string ToRegionId,
    string Route,
    long Amount,
    long SentTick,
    long ArriveTick);

/// <summary>Energy committed to a region link and not yet in the destination grid.</summary>
public sealed record EnergyShipment(
    long Id,
    EntityId FromEntityId,
    string CivilizationId,
    string FromRegionId,
    string ToRegionId,
    string Route,
    long Amount,
    long SentTick,
    long ArriveTick);

/// <summary>A message that has reached the recipient civilization's inbox.</summary>
public sealed record DeliveredMessage(
    long Id,
    EntityId FromEntityId,
    string FromCivilizationId,
    string TargetRegionId,
    string Topic,
    string Payload,
    long SentTick,
    long DeliveredTick);
