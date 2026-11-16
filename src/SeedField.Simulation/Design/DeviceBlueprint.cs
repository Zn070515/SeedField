namespace SeedField.Simulation;

/// <summary>Power generation/storage module. It is what makes a design cover Energy.</summary>
public sealed record PowerModule(int EnergyPerTick, long StorageCapacity);

/// <summary>Compute/storage module. It is what makes a design cover State.</summary>
public sealed record StateModule(int ComputePerTick, int StorageCapacity);

/// <summary>
/// Immutable workshop version. A deployed instance references a specific version;
/// publishing a newer version never changes existing instances.
/// </summary>
public sealed record DeviceBlueprint(
    string BlueprintId,
    int Version,
    string Name,
    int MaxHealth,
    long EnergyCapacity,
    SensorModule? Sensor = null,
    WeaponModule? Weapon = null,
    DriveModule? Drive = null,
    CommModule? Comm = null,
    PowerModule? Power = null,
    StateModule? State = null,
    string? ControllerProgramId = null)
{
    public DeviceBranch Branches
    {
        get
        {
            var branches = DeviceBranch.None;

            if (Power is not null || EnergyCapacity > 0)
            {
                branches |= DeviceBranch.Energy;
            }

            if (Sensor is not null)
            {
                branches |= DeviceBranch.Sensing;
            }

            if (Comm is not null)
            {
                branches |= DeviceBranch.Communication;
            }

            if (State is not null)
            {
                branches |= DeviceBranch.State;
            }

            if (Weapon is not null || Drive is not null)
            {
                branches |= DeviceBranch.Execution;
            }

            return branches;
        }
    }
}

public sealed class BlueprintValidationException : Exception
{
    public BlueprintValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// Workshop registry. Versions are assigned monotonically per blueprint id and are
/// immutable once published. Coverage is the union of the latest version per design.
/// </summary>
public sealed class WorkshopService
{
    private readonly SortedDictionary<string, List<DeviceBlueprint>> _versions =
        new(StringComparer.Ordinal);

    public IReadOnlyList<DeviceBlueprint> GetVersions(string blueprintId) =>
        _versions.TryGetValue(blueprintId, out var versions)
            ? versions
            : Array.Empty<DeviceBlueprint>();

    public DeviceBlueprint? GetVersion(string blueprintId, int version) =>
        _versions.TryGetValue(blueprintId, out var versions) &&
        version > 0 &&
        version <= versions.Count
            ? versions[version - 1]
            : null;

    public IEnumerable<DeviceBlueprint> GetAllVersions() =>
        _versions.Values.SelectMany(versions => versions);

    public DeviceBlueprint Publish(DeviceBlueprint draft)
    {
        Validate(draft);

        if (!_versions.TryGetValue(draft.BlueprintId, out var versions))
        {
            versions = new List<DeviceBlueprint>();
            _versions.Add(draft.BlueprintId, versions);
        }

        var published = draft with { Version = versions.Count + 1 };
        versions.Add(published);
        return published;
    }

    /// <summary>Restores an exact immutable version from a save file.</summary>
    public void Restore(DeviceBlueprint blueprint)
    {
        Validate(blueprint);

        if (blueprint.Version <= 0)
        {
            throw new BlueprintValidationException(
                "INVALID_VERSION",
                "Restored blueprints need a positive version.");
        }

        if (!_versions.TryGetValue(blueprint.BlueprintId, out var versions))
        {
            versions = new List<DeviceBlueprint>();
            _versions.Add(blueprint.BlueprintId, versions);
        }

        if (blueprint.Version != versions.Count + 1)
        {
            throw new BlueprintValidationException(
                "NON_SEQUENTIAL_VERSION",
                $"Blueprint '{blueprint.BlueprintId}' expected version {versions.Count + 1}, got {blueprint.Version}.");
        }

        versions.Add(blueprint);
    }

    private static void Validate(DeviceBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);

        if (string.IsNullOrWhiteSpace(blueprint.BlueprintId))
        {
            throw new BlueprintValidationException("MISSING_ID", "Blueprint id is required.");
        }

        if (string.IsNullOrWhiteSpace(blueprint.Name))
        {
            throw new BlueprintValidationException("MISSING_NAME", "Blueprint name is required.");
        }

        if (blueprint.MaxHealth <= 0)
        {
            throw new BlueprintValidationException("INVALID_HEALTH", "MaxHealth must be positive.");
        }

        if (blueprint.EnergyCapacity < 0)
        {
            throw new BlueprintValidationException("INVALID_ENERGY", "Energy capacity must be non-negative.");
        }

        if (blueprint.Branches == DeviceBranch.None)
        {
            throw new BlueprintValidationException(
                "NO_BRANCH",
                "A device design must provide at least one of the five branches.");
        }

        if (blueprint.Power is { EnergyPerTick: < 0 } or { StorageCapacity: < 0 })
        {
            throw new BlueprintValidationException("INVALID_POWER", "Power values must be non-negative.");
        }

        if (blueprint.State is { ComputePerTick: < 0 } or { StorageCapacity: < 0 })
        {
            throw new BlueprintValidationException("INVALID_STATE", "State values must be non-negative.");
        }

        if (blueprint.Comm is { LatencyTicks: < 0 } or { EnergyCostPerMessage: < 0 } or { BandwidthPerTick: < 0 })
        {
            throw new BlueprintValidationException("INVALID_COMM", "Comm values must be non-negative.");
        }

        if (blueprint.Sensor is { RangeMm: < 1 })
        {
            throw new BlueprintValidationException("INVALID_SENSOR", "Sensor range must be positive.");
        }

        if (blueprint.Weapon is { RangeMm: < 0 } or { Damage: < 0 } or { EnergyCost: < 0 } or { CooldownTicks: < 0 })
        {
            throw new BlueprintValidationException("INVALID_WEAPON", "Weapon values must be non-negative.");
        }

        if (blueprint.Drive is { SpeedMmPerTick: < 0 } or { EnergyCostPerMove: < 0 })
        {
            throw new BlueprintValidationException("INVALID_DRIVE", "Drive values must be non-negative.");
        }
    }

    public DeviceBranch Coverage
    {
        get
        {
            var coverage = DeviceBranch.None;
            foreach (var versions in _versions.Values)
            {
                if (versions.Count > 0)
                {
                    coverage |= versions[^1].Branches;
                }
            }

            return coverage;
        }
    }

    /// <summary>
    /// Minimum tutorial/PvE admission: one published design per branch. Efficiency is
    /// deliberately not part of this check.
    /// </summary>
    public bool IsMinimumUsable => (Coverage & DeviceBranch.All) == DeviceBranch.All;
}
