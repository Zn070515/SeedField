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
    bool HasComm,
    bool HasDrill = false,
    int DrillRangeMm = 0);

public sealed record ObservedDeposit(
    long DepositId,
    string RegionId,
    OreKind Kind,
    Vec2 Position,
    int RadiusMm,
    long Remaining,
    long YieldPerTick);

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

public sealed record ObservedTreaty(
    long TreatyId,
    TreatyKind Kind,
    string OtherCivilizationId,
    long SignedTick);

public sealed record ObservedTreatyProposal(
    long ProposalId,
    TreatyKind Kind,
    string ProposerCivilizationId,
    string TargetCivilizationId,
    long ProposedTick,
    long ExpiresTick);

public sealed record ObservedTradeOffer(
    long TradeOfferId,
    string ProposerCivilizationId,
    string TargetCivilizationId,
    string RegionId,
    long GiveMaterial,
    long GiveEnergy,
    long WantMaterial,
    long WantEnergy,
    long ProposedTick,
    long ExpiresTick,
    long NegotiationId,
    int Round);

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
    ObservedMatch? Match = null,
    IReadOnlyList<ObservedTreaty>? Treaties = null,
    IReadOnlyList<ObservedTreatyProposal>? TreatyProposals = null,
    IReadOnlyList<ObservedTradeOffer>? TradeOffers = null,
    IReadOnlyList<ObservedDeposit>? Deposits = null);
