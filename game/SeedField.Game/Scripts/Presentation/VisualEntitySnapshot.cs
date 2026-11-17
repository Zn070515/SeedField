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

    public string VisualAssetName =>
        string.Equals(BlueprintId, "power", StringComparison.Ordinal)
            ? "power_node"
            : HasWeapon
                ? "worker_frame_drill"
                : HasSensor
                    ? "worker_frame_sensor"
                    : "worker_frame";
}
