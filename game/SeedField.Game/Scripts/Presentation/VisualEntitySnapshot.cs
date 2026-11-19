using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Immutable presentation fields copied from an entity. Drawing code receives this
/// instead of EntityState so it cannot mutate simulation state.
/// </summary>
public readonly record struct VisualEntitySnapshot(
    long EntityId,
    string CivilizationId,
    Vec2 Position,
    int Health,
    int MaxHealth,
    long Energy,
    long EnergyCapacity,
    string? BlueprintId,
    int BlueprintVersion,
    bool HasSensor,
    int SensorRangeMm,
    bool HasWeapon,
    int WeaponRangeMm,
    bool HasDrive,
    bool HasComm)
{
    public static VisualEntitySnapshot FromObserved(ObservedEntity entity) => new(
        entity.EntityId,
        entity.CivilizationId,
        entity.Position,
        entity.Health,
        entity.MaxHealth,
        Energy: 0,
        EnergyCapacity: 0,
        BlueprintId: null,
        BlueprintVersion: 0,
        entity.HasSensor,
        entity.SensorRangeMm,
        entity.HasWeapon,
        entity.WeaponRangeMm,
        entity.HasDrive,
        entity.HasComm);

    public static VisualEntitySnapshot From(EntityState entity) => new(
        entity.Id.Value,
        entity.CivilizationId,
        entity.Position,
        entity.Health,
        entity.MaxHealth,
        entity.Energy,
        entity.EnergyCapacity,
        entity.BlueprintId,
        entity.BlueprintVersion,
        entity.Sensor is not null,
        entity.Sensor?.RangeMm ?? 0,
        entity.Weapon is not null,
        entity.Weapon?.RangeMm ?? 0,
        entity.Drive is not null,
        entity.Comm is not null);

    public string VisualAssetName => AssetFor(BlueprintId, HasWeapon, HasSensor);

    public static string AssetForBlueprint(DeviceBlueprint blueprint) =>
        AssetFor(
            blueprint.BlueprintId,
            blueprint.Weapon is not null,
            blueprint.Sensor is not null);

    public static string AssetFor(
        string? blueprintId,
        bool hasWeapon,
        bool hasSensor) =>
        blueprintId switch
        {
            "power" => "waste_generator",
            "sensing" => "waste_sensor",
            "comm" => "waste_comm",
            "state" => "waste_state",
            "drill" => "waste_drill",
            "smelter" or "smelter-lead" => "waste_smelter",
            "fabricator" => "waste_foundry",
            "repair_bay" => "waste_repair_bay",
            "shield_generator" => "waste_shield_generator",
            "barricade" => "waste_barricade",
            "scout" => hasWeapon ? "waste_sentry" : "waste_scout",
            "strike" or "beam" => "waste_sentry",
            _ => hasWeapon
                ? "waste_sentry"
                : hasSensor
                    ? "waste_scout"
                    : "worker_frame",
        };
}
