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

public sealed record ShotMissed(
    long Tick,
    EntityId AttackerId,
    EntityId TargetId,
    Vec2 From,
    Vec2 Target,
    long EnergySpent) : SimulationEvent(Tick);

public sealed record CombatLootGranted(
    long Tick,
    string CivilizationId,
    string RegionId,
    EntityId SourceEntityId,
    long Material) : SimulationEvent(Tick);

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

/// <summary>Core recovery quota/debt were cleared by a maintenance action.</summary>
public sealed record CoreMaintained(
    long Tick,
    string CivilizationId,
    string RegionId,
    long MaterialSpent) : SimulationEvent(Tick);

/// <summary>A destroyed blueprint instance left salvageable material.</summary>
public sealed record WreckCreated(
    long Tick,
    long WreckId,
    string RegionId,
    string OwnerCivilizationId,
    string BlueprintId,
    int BlueprintVersion,
    long MaterialRecoverable) : SimulationEvent(Tick);

public sealed record WreckSalvaged(
    long Tick,
    long WreckId,
    string CivilizationId,
    string RegionId,
    long MaterialRecovered) : SimulationEvent(Tick);

/// <summary>An offline snapshot slice was converted into wrecks in a shell region.</summary>
public sealed record RuinImported(
    long Tick,
    string CivilizationId,
    string RegionId,
    string SourceCivilizationId,
    int EntriesInSlice,
    int WrecksCreated,
    long MaterialTotal) : SimulationEvent(Tick);

/// <summary>The fast-recovery quota is exhausted; the attempt was rejected.</summary>
public sealed record RecoveryQuotaDenied(
    long Tick,
    string CivilizationId,
    string RegionId,
    int Used,
    int Limit,
    long ResetsInTicks) : SimulationEvent(Tick);

public sealed record TreatyProposed(
    long Tick,
    long ProposalId,
    TreatyKind Kind,
    string ProposerCivilizationId,
    string TargetCivilizationId,
    long ExpiresTick) : SimulationEvent(Tick);

public sealed record TreatyAccepted(
    long Tick,
    long TreatyId,
    TreatyKind Kind,
    string CivilizationA,
    string CivilizationB) : SimulationEvent(Tick);

public sealed record TreatyRejected(
    long Tick,
    long ProposalId,
    TreatyKind Kind,
    string ProposerCivilizationId,
    string TargetCivilizationId) : SimulationEvent(Tick);

public sealed record TreatyBroken(
    long Tick,
    long TreatyId,
    TreatyKind Kind,
    string CivilizationA,
    string CivilizationB,
    string BrokenByCivilizationId) : SimulationEvent(Tick);

public sealed record TreatyProposalExpired(
    long Tick,
    long ProposalId,
    TreatyKind Kind,
    string ProposerCivilizationId,
    string TargetCivilizationId) : SimulationEvent(Tick);

public sealed record TradeProposed(
    long Tick,
    long TradeOfferId,
    string ProposerCivilizationId,
    string TargetCivilizationId,
    string RegionId,
    long GiveMaterial,
    long GiveEnergy,
    long WantMaterial,
    long WantEnergy,
    long ExpiresTick) : SimulationEvent(Tick);

public sealed record TradeAccepted(
    long Tick,
    long TradeOfferId,
    string ProposerCivilizationId,
    string TargetCivilizationId,
    string RegionId,
    long GiveMaterial,
    long GiveEnergy,
    long WantMaterial,
    long WantEnergy) : SimulationEvent(Tick);

public sealed record TradeRejected(
    long Tick,
    long TradeOfferId,
    string ProposerCivilizationId,
    string TargetCivilizationId,
    long RefundedMaterial,
    long RefundedEnergy) : SimulationEvent(Tick);

public sealed record TradeCancelled(
    long Tick,
    long TradeOfferId,
    string ProposerCivilizationId,
    string TargetCivilizationId,
    string CancelledByCivilizationId,
    long RefundedMaterial,
    long RefundedEnergy) : SimulationEvent(Tick);

public sealed record TradeExpired(
    long Tick,
    long TradeOfferId,
    string ProposerCivilizationId,
    string TargetCivilizationId,
    long RefundedMaterial,
    long RefundedEnergy) : SimulationEvent(Tick);

public sealed record PowerFlowed(
    long Tick,
    string CivilizationId,
    string FromRegionId,
    string ToRegionId,
    long Amount) : SimulationEvent(Tick);

public sealed record EnergyWasted(
    long Tick,
    string CivilizationId,
    string RegionId,
    long Amount,
    string Source) : SimulationEvent(Tick);

public sealed record PowerBlackout(
    long Tick,
    string CivilizationId,
    string RegionId) : SimulationEvent(Tick);

public sealed record PowerRestored(
    long Tick,
    string CivilizationId,
    string RegionId,
    long GridEnergy) : SimulationEvent(Tick);

/// <summary>
/// The breaker stays open until maintained. Reason is "fault" for a random fault or
/// "trips" when the configured trip limit was reached.
/// </summary>
public sealed record BreakerLockedOut(
    long Tick,
    string CivilizationId,
    string RegionId,
    int TripCount,
    string Reason) : SimulationEvent(Tick);

public sealed record BreakerMaintained(
    long Tick,
    string CivilizationId,
    string RegionId,
    long MaterialSpent) : SimulationEvent(Tick);

public sealed record PowerShipped(
    long Tick,
    long PowerShipmentId,
    string CivilizationId,
    string FromRegionId,
    string ToRegionId,
    long Amount,
    long DeliveredAmount,
    long ArriveTick,
    string Route) : SimulationEvent(Tick);

public sealed record PowerArrived(
    long Tick,
    long PowerShipmentId,
    string CivilizationId,
    string ToRegionId,
    long Amount,
    long LostAmount) : SimulationEvent(Tick);

public sealed record EnergyAutoDrawn(
    long Tick,
    EntityId EntityId,
    string RegionId,
    long Amount) : SimulationEvent(Tick);

public sealed record PowerShortfall(
    long Tick,
    string CivilizationId,
    string RegionId,
    EntityId EntityId,
    long Requested,
    long Available,
    string Source) : SimulationEvent(Tick);

public sealed record TradeCountered(
    long Tick,
    long OriginalTradeOfferId,
    long NewTradeOfferId,
    string CivilizationId,
    long NegotiationId,
    int Round,
    long RefundedMaterial,
    long RefundedEnergy,
    long GiveMaterial,
    long GiveEnergy,
    long WantMaterial,
    long WantEnergy) : SimulationEvent(Tick);
