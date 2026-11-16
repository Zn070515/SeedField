namespace SeedField.Simulation;

/// <summary>
/// Recovery quotas for the permanent seed core. These are deliberately explicit
/// scenario/config values: the design bible marks the real rates as open/validate.
/// </summary>
public sealed record SeedCoreConfig(
    long EnergyPerTick,
    long MaterialPerTick,
    long MaxEnergyReserve,
    long MaxMaterialReserve)
{
    public void Validate()
    {
        if (EnergyPerTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(EnergyPerTick));
        }

        if (MaterialPerTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaterialPerTick));
        }

        if (MaxEnergyReserve < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxEnergyReserve));
        }

        if (MaxMaterialReserve < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxMaterialReserve));
        }
    }
}

/// <summary>
/// The permanent, untouchable seed core of one civilization. It regenerates a small
/// capped reserve and can bootstrap a minimum entity without any external industry.
/// </summary>
public sealed class SeedCoreState
{
    internal SeedCoreState(string civilizationId, string regionId, SeedCoreConfig config)
    {
        config.Validate();
        CivilizationId = civilizationId;
        RegionId = regionId;
        Config = config;
    }

    public string CivilizationId { get; }

    public string RegionId { get; }

    public SeedCoreConfig Config { get; }

    public long EnergyReserve { get; internal set; }

    public long MaterialReserve { get; internal set; }
}

/// <summary>
/// Blueprint for the minimum recoverable entity. Cost and capability are scenario data,
/// not approved balance values.
/// </summary>
public sealed record RecoveryBlueprint(
    string Name,
    long RequiredEnergy,
    long RequiredMaterial,
    int MaxHealth,
    long Energy,
    long EnergyCapacity,
    long Material,
    SensorModule? Sensor = null,
    WeaponModule? Weapon = null,
    DriveModule? Drive = null,
    CommModule? Comm = null);
