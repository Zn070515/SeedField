namespace SeedField.Simulation;

/// <summary>
/// Versioned, explicit save shape. It stores the authoritative simulation state only;
/// controller programs and their deployment versions live in the composition layer.
/// </summary>
public sealed record WorldSaveData(
    int SchemaVersion,
    string GameVersion,
    long Tick,
    long NextEntityId,
    long NextMessageId,
    RegionSaveData[] Regions,
    CoreSaveData[] Cores,
    MessageSaveData[] InFlightMessages,
    InboxSaveData[] Inboxes,
    StatisticsSaveData Statistics,
    BlueprintSaveData[]? Blueprints = null,
    LinkSaveData[]? Links = null,
    MaterialShipmentSaveData[]? MaterialShipments = null,
    long NextShipmentId = 0,
    EnergyShipmentSaveData[]? EnergyShipments = null,
    long NextEnergyShipmentId = 0,
    ControlPointSaveData? ControlPoint = null,
    ControlPointSaveData[]? ControlPoints = null,
    MatchSaveData? Match = null,
    ConstructionOrderSaveData[]? ConstructionOrders = null,
    long NextConstructionOrderId = 0,
    MessagePolicySaveData[]? MessagePolicies = null,
    TreatyProposalSaveData[]? TreatyProposals = null,
    TreatySaveData[]? Treaties = null,
    TradeOfferSaveData[]? TradeOffers = null,
    long NextTreatyProposalId = 0,
    long NextTreatyId = 0,
    long NextTradeOfferId = 0,
    PowerPolicySaveData[]? PowerPolicies = null,
    PowerShipmentSaveData[]? PowerShipments = null,
    long NextPowerShipmentId = 0,
    GridCapacitySaveData[]? GridCapacities = null,
    PowerGridStateSaveData[]? PowerGridStates = null);

public sealed record PowerPolicySaveData(
    string CivilizationId,
    long ReserveEnergy,
    int HighPriorityThreshold = 0,
    long PriorityReserveEnergy = 0,
    bool AutoReconnect = true,
    int BreakerTripLimit = 0,
    long BreakerMaintenanceMaterialCost = 0);

public sealed record GridCapacitySaveData(
    string RegionId,
    string CivilizationId,
    long Capacity);

public sealed record PowerGridStateSaveData(
    string RegionId,
    string CivilizationId,
    bool BreakerOpen,
    bool ManualHold,
    int TripCount = 0,
    bool MaintenanceRequired = false);

public sealed record TreatyProposalSaveData(
    long Id,
    TreatyKind Kind,
    string ProposerCivilizationId,
    string TargetCivilizationId,
    long ProposedTick,
    long ExpiresTick);

public sealed record TreatySaveData(
    long Id,
    TreatyKind Kind,
    string CivilizationA,
    string CivilizationB,
    string ProposerCivilizationId,
    long SignedTick);

public sealed record TradeOfferSaveData(
    long Id,
    string ProposerCivilizationId,
    string TargetCivilizationId,
    string RegionId,
    long GiveMaterial,
    long GiveEnergy,
    long WantMaterial,
    long WantEnergy,
    long ProposedTick,
    long ExpiresTick,
    long NegotiationId = 0,
    int Round = 1);

public sealed record MessagePolicySaveData(
    string CivilizationId,
    bool AllowAllInbound,
    string[] BlockedCivilizations,
    string[] BlockedTopics,
    bool AllowAllTopics = true,
    string[]? AllowedCivilizations = null,
    string[]? AllowedTopics = null);

public sealed record ConstructionOrderSaveData(
    long Id,
    string CivilizationId,
    string BlueprintId,
    int BlueprintVersion,
    string RegionId,
    int X,
    int Y,
    long StartTick,
    long CompleteTick,
    long MaterialPaid = 0);

public sealed record MatchSaveData(
    int CapturedPointsToWin,
    string? WinnerCivilizationId);

public sealed record ControlPointSaveData(
    string Id,
    string RegionId,
    int X,
    int Y,
    int RadiusMm,
    int HoldTicksRequired,
    string? HoldingCivilizationId,
    int HoldTicks,
    string? OwnerCivilizationId);

public sealed record LinkSaveData(
    string Id,
    string RegionA,
    string RegionB,
    int LatencyTicks,
    int MessagesPerTick,
    int MaterialPerTick = 0,
    int EnergyPerTick = 0,
    int PowerPerTick = 0,
    int PowerLatencyTicks = 0,
    int PowerLossPercent = 0);

public sealed record PowerShipmentSaveData(
    long Id,
    string CivilizationId,
    string FromRegionId,
    string ToRegionId,
    string Route,
    long Amount,
    long DeliveredAmount,
    long SentTick,
    long ArriveTick);

public sealed record EnergyShipmentSaveData(
    long Id,
    long FromEntityId,
    string CivilizationId,
    string FromRegionId,
    string ToRegionId,
    string Route,
    long Amount,
    long SentTick,
    long ArriveTick);

public sealed record MaterialShipmentSaveData(
    long Id,
    long FromEntityId,
    string CivilizationId,
    string FromRegionId,
    string ToRegionId,
    string Route,
    long Amount,
    long SentTick,
    long ArriveTick);

public sealed record RegionSaveData(
    string Id,
    int WidthMm,
    int HeightMm,
    bool IsCoreRegion,
    EntitySaveData[] Entities,
    StockSaveData[]? Stocks = null);

public sealed record StockSaveData(string CivilizationId, long Material, long Energy = 0);

public sealed record EntitySaveData(
    long Id,
    string CivilizationId,
    string RegionId,
    int X,
    int Y,
    int MaxHealth,
    int Health,
    long Energy,
    long EnergyCapacity,
    long Material,
    long NextWeaponReadyTick,
    SensorSaveData? Sensor,
    WeaponSaveData? Weapon,
    DriveSaveData? Drive,
    CommSaveData? Comm,
    string? BlueprintId = null,
    int BlueprintVersion = 0,
    string? ControllerProgramId = null,
    PowerSaveData? Power = null,
    StateSaveData? State = null);

public sealed record SensorSaveData(int RangeMm);

public sealed record WeaponSaveData(int RangeMm, int Damage, long EnergyCost, int CooldownTicks);

public sealed record DriveSaveData(int SpeedMmPerTick, long EnergyCostPerMove);

public sealed record CommSaveData(
    int LatencyTicks,
    long EnergyCostPerMessage,
    int BandwidthPerTick = 0);

public sealed record PowerSaveData(
    int EnergyPerTick,
    long StorageCapacity,
    int Priority = 0);

public sealed record StateSaveData(int ComputePerTick, int StorageCapacity);

public sealed record BlueprintSaveData(
    string BlueprintId,
    int Version,
    string Name,
    int MaxHealth,
    long EnergyCapacity,
    SensorSaveData? Sensor,
    WeaponSaveData? Weapon,
    DriveSaveData? Drive,
    CommSaveData? Comm,
    PowerSaveData? Power,
    StateSaveData? State,
    string? ControllerProgramId,
    long MaterialCost = 0,
    int BuildTicks = 0);

public sealed record SeedCoreProductSaveData(
    DeviceBranch Branch,
    string BlueprintId,
    int Version);

public sealed record CoreSaveData(
    string CivilizationId,
    string RegionId,
    long EnergyPerTick,
    long MaterialPerTick,
    long MaxEnergyReserve,
    long MaxMaterialReserve,
    long EnergyReserve,
    long MaterialReserve,
    SeedCoreProductSaveData[]? LatestProducts = null);

public sealed record MessageSaveData(
    long Id,
    long FromEntityId,
    string FromCivilizationId,
    string ToCivilizationId,
    string Topic,
    string Payload,
    long SentTick,
    long DeliverTick,
    string TargetRegionId = "");

public sealed record DeliveredMessageSaveData(
    long Id,
    long FromEntityId,
    string FromCivilizationId,
    string Topic,
    string Payload,
    long SentTick,
    long DeliveredTick,
    string TargetRegionId = "");

public sealed record InboxSaveData(string CivilizationId, DeliveredMessageSaveData[] Messages);

public sealed record StatisticsSaveData(
    long EnergySpentTotal,
    long EnergyRegeneratedTotal,
    long EnergyGeneratedTotal,
    long MaterialSpentTotal,
    long MaterialRegeneratedTotal,
    long ShotsFired,
    long DamageDealt,
    long EntitiesDestroyed,
    long MessagesDelivered,
    long MessagesExpired = 0,
    long MaterialShippedTotal = 0,
    long MaterialDeliveredTotal = 0,
    long EnergyShippedTotal = 0,
    long EnergyDeliveredTotal = 0,
    long EnergyStoredTotal = 0,
    long EnergyDrawnTotal = 0,
    long EnergyLostTotal = 0,
    long MaterialLostTotal = 0,
    long ConstructionsStartedTotal = 0,
    long ConstructionsCompletedTotal = 0,
    long MaterialRefundedTotal = 0,
    long TradeMaterialSettledTotal = 0,
    long TradeEnergySettledTotal = 0,
    long EnergyFlowedTotal = 0,
    long EnergyOverflowedTotal = 0,
    long EnergyFlowLossTotal = 0,
    long EnergyAutoDrawnTotal = 0,
    long EnergyWastedTotal = 0);

public sealed class SaveFormatException : Exception
{
    public SaveFormatException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
