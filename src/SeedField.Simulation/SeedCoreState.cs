namespace SeedField.Simulation;

/// <summary>
/// Recovery quotas for the permanent seed core. These are deliberately explicit
/// scenario/config values: the design bible marks the real rates as open/validate.
/// </summary>
public sealed record SeedCoreConfig(
    long EnergyPerTick,
    long MaterialPerTick,
    long MaxEnergyReserve,
    long MaxMaterialReserve,
    long RecoveryWindowTicks = 0,
    int RecoveryQuotaPerWindow = 0,
    int RecoveryCostGrowthPercent = 0,
    long RecoveryStreakDecayTicks = 0,
    int RecoveryOverdraftCostPercent = 0,
    int AnchorRecoveryCostPercent = 0,
    int SiegeRecoveryCostPercent = 0,
    int SiegeQuotaPenalty = 0,
    long RecoveryMaintenanceMaterialCost = 0,
    long AnchorEnergyPerTick = 0,
    long AnchorMaterialPerTick = 0,
    long AnchorStorageBonus = 0)
{
    /// <summary>Zero window or quota disables the fast-recovery limit.</summary>
    public bool HasRecoveryQuota =>
        RecoveryWindowTicks > 0 && RecoveryQuotaPerWindow > 0;

    /// <summary>Zero growth keeps recovery costs flat.</summary>
    public bool HasRecoveryCostGrowth => RecoveryCostGrowthPercent > 0;

    /// <summary>
    /// Zero keeps the quota hard: attempts beyond it are rejected. Positive values
    /// allow "slow mode": the recovery succeeds but costs this much extra percent.
    /// </summary>
    public bool HasRecoveryOverdraft => RecoveryOverdraftCostPercent > 0;

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

        if (AnchorEnergyPerTick < 0 || AnchorMaterialPerTick < 0 ||
            AnchorStorageBonus < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AnchorEnergyPerTick),
                "Anchor support values must be non-negative.");
        }

        if (RecoveryWindowTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RecoveryWindowTicks));
        }

        if (RecoveryQuotaPerWindow < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RecoveryQuotaPerWindow));
        }

        if (RecoveryCostGrowthPercent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RecoveryCostGrowthPercent));
        }

        if (RecoveryStreakDecayTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RecoveryStreakDecayTicks));
        }

        if (RecoveryOverdraftCostPercent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RecoveryOverdraftCostPercent));
        }

        if (AnchorRecoveryCostPercent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AnchorRecoveryCostPercent));
        }

        if (SiegeRecoveryCostPercent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SiegeRecoveryCostPercent));
        }

        if (SiegeQuotaPenalty < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SiegeQuotaPenalty));
        }

        if (RecoveryMaintenanceMaterialCost < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RecoveryMaintenanceMaterialCost));
        }
    }
}

public sealed record SeedCoreProduct(string BlueprintId, int Version);

/// <summary>
/// The permanent, untouchable seed core of one civilization. It regenerates a small
/// capped reserve, records the latest confirmed product of each branch, and can
/// bootstrap without any external industry.
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

    /// <summary>Start of the current fast-recovery quota window.</summary>
    public long RecoveryWindowStartTick { get; internal set; }

    /// <summary>Successful fast recoveries inside the current window.</summary>
    public int RecoveriesInWindow { get; internal set; }

    /// <summary>Consecutive recoveries used by the escalating cost rule.</summary>
    public int RecoveryStreak { get; internal set; }

    /// <summary>Tick of the last successful recovery; -1 before the first one.</summary>
    public long LastRecoveryTick { get; internal set; } = -1;

    /// <summary>Largest number of shell anchors ever registered for this core.</summary>
    public int AnchorTarget { get; internal set; }

    /// <summary>Latest confirmed design version per branch.</summary>
    public SortedDictionary<DeviceBranch, SeedCoreProduct> LatestProducts { get; } = new();

    internal void RefreshRecoveryWindow(long tick)
    {
        if (!Config.HasRecoveryQuota)
        {
            return;
        }

        if (tick - RecoveryWindowStartTick >= Config.RecoveryWindowTicks)
        {
            RecoveryWindowStartTick = tick;
            RecoveriesInWindow = 0;
        }
    }

    internal void RefreshRecoveryDebt(long tick)
    {
        if (!Config.HasRecoveryCostGrowth ||
            Config.RecoveryStreakDecayTicks <= 0 ||
            LastRecoveryTick < 0)
        {
            return;
        }

        if (tick - LastRecoveryTick >= Config.RecoveryStreakDecayTicks)
        {
            RecoveryStreak = 0;
        }
    }

    internal enum RecoveryQuotaDecision
    {
        WithinQuota,
        Overdraft,
        Exhausted,
    }

    /// <summary>
    /// Classifies the attempt against the fast-recovery quota. A disabled quota allows
    /// every attempt. When overdraft is configured the attempt is allowed at extra cost;
    /// otherwise it is rejected until the window resets.
    /// </summary>
    internal RecoveryQuotaDecision EvaluateRecoveryQuota(
        long tick,
        int quotaPenalty,
        out long resetsInTicks,
        out int effectiveLimit)
    {
        resetsInTicks = 0;
        if (!Config.HasRecoveryQuota)
        {
            effectiveLimit = Config.RecoveryQuotaPerWindow;
            return RecoveryQuotaDecision.WithinQuota;
        }

        effectiveLimit = Math.Max(
            1,
            Config.RecoveryQuotaPerWindow - Math.Max(0, quotaPenalty));
        RefreshRecoveryWindow(tick);
        if (RecoveriesInWindow < effectiveLimit)
        {
            return RecoveryQuotaDecision.WithinQuota;
        }

        if (Config.HasRecoveryOverdraft)
        {
            return RecoveryQuotaDecision.Overdraft;
        }

        resetsInTicks = Math.Max(
            1,
            RecoveryWindowStartTick + Config.RecoveryWindowTicks - tick);
        return RecoveryQuotaDecision.Exhausted;
    }

    /// <summary>Cost multiplier in percent; 100 means the base cost.</summary>
    internal long RecoveryCostMultiplierPercent(
        long tick,
        bool overdraft,
        int siegeCostPercent)
    {
        RefreshRecoveryDebt(tick);
        var percent = 100L;
        if (Config.HasRecoveryCostGrowth)
        {
            percent += (long)Config.RecoveryCostGrowthPercent * RecoveryStreak;
        }

        if (overdraft)
        {
            percent += Config.RecoveryOverdraftCostPercent;
        }

        percent += Math.Max(0, siegeCostPercent);
        return Math.Min(percent, 1_000_000L);
    }

    internal void RecordRecovery(long tick)
    {
        RefreshRecoveryWindow(tick);
        RefreshRecoveryDebt(tick);
        if (Config.HasRecoveryQuota)
        {
            RecoveriesInWindow++;
        }

        if (Config.HasRecoveryCostGrowth)
        {
            RecoveryStreak++;
        }

        LastRecoveryTick = tick;
    }

    public void RecordLatestProduct(DeviceBranch branch, DeviceBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);

        if (branch == DeviceBranch.None || (branch & DeviceBranch.All) != branch)
        {
            throw new ArgumentOutOfRangeException(nameof(branch), "A single concrete branch is required.");
        }

        if ((blueprint.Branches & branch) != branch)
        {
            throw new BlueprintValidationException(
                "BRANCH_NOT_COVERED",
                $"Blueprint '{blueprint.BlueprintId}' does not provide {branch}.");
        }

        if (blueprint.Version <= 0)
        {
            throw new BlueprintValidationException(
                "UNPUBLISHED_BLUEPRINT",
                "Only a published blueprint can be recorded as a core product.");
        }

        LatestProducts[branch] = new SeedCoreProduct(blueprint.BlueprintId, blueprint.Version);
    }
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
