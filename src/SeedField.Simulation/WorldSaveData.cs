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
    BlueprintSaveData[]? Blueprints = null);

public sealed record RegionSaveData(
    string Id,
    int WidthMm,
    int HeightMm,
    bool IsCoreRegion,
    EntitySaveData[] Entities);

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

public sealed record PowerSaveData(int EnergyPerTick, long StorageCapacity);

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
    string? ControllerProgramId);

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
    long DeliverTick);

public sealed record DeliveredMessageSaveData(
    long Id,
    long FromEntityId,
    string FromCivilizationId,
    string Topic,
    string Payload,
    long SentTick,
    long DeliveredTick);

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
    long MessagesDelivered);

public sealed class SaveFormatException : Exception
{
    public SaveFormatException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
