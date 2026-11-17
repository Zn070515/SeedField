namespace SeedField.Simulation;

/// <summary>Read-only recovery view for host UI and diagnostics.</summary>
public sealed record CoreRecoverySnapshot(
    string CivilizationId,
    string RegionId,
    bool HasCore,
    long EnergyReserve,
    long MaterialReserve,
    long MaxEnergyReserve,
    long MaxMaterialReserve,
    long RecoveryWindowTicks,
    int QuotaPerWindow,
    int RecoveriesInWindow,
    int EffectiveQuota,
    bool CanRecover,
    long WindowResetsInTicks,
    int RecoveryStreak,
    long CostMultiplierPercent,
    int AnchorTarget,
    int AliveAnchors,
    int AnchorGap,
    int AnchorRecoveryCostPercent,
    bool IsBesieged,
    int SiegeRecoveryCostPercent,
    int SiegeQuotaPenalty,
    long RecoveryMaintenanceMaterialCost,
    int WreckCount);

public static class CoreRecoveryDiagnosticsBuilder
{
    public static CoreRecoverySnapshot Build(WorldState world, string civilizationId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrEmpty(civilizationId);

        if (!world.Cores.TryGetValue(civilizationId, out var core))
        {
            return new CoreRecoverySnapshot(
                CivilizationId: civilizationId,
                RegionId: string.Empty,
                HasCore: false,
                EnergyReserve: 0,
                MaterialReserve: 0,
                MaxEnergyReserve: 0,
                MaxMaterialReserve: 0,
                RecoveryWindowTicks: 0,
                QuotaPerWindow: 0,
                RecoveriesInWindow: 0,
                EffectiveQuota: 0,
                CanRecover: false,
                WindowResetsInTicks: 0,
                RecoveryStreak: 0,
                CostMultiplierPercent: 100,
                AnchorTarget: 0,
                AliveAnchors: 0,
                AnchorGap: 0,
                AnchorRecoveryCostPercent: 0,
                IsBesieged: false,
                SiegeRecoveryCostPercent: 0,
                SiegeQuotaPenalty: 0,
                RecoveryMaintenanceMaterialCost: 0,
                WreckCount: world.Wrecks.Count);
        }

        var pressure = CoreShellRules.EvaluatePressure(world, core);
        var effectiveQuota = core.Config.HasRecoveryQuota
            ? Math.Max(
                1,
                core.Config.RecoveryQuotaPerWindow - pressure.QuotaPenalty)
            : core.Config.RecoveryQuotaPerWindow;
        var resetsIn = core.Config.HasRecoveryQuota
            ? Math.Max(
                0,
                core.RecoveryWindowStartTick +
                core.Config.RecoveryWindowTicks -
                world.Tick)
            : 0;
        var overdraft = core.Config.HasRecoveryQuota &&
                        core.Config.HasRecoveryOverdraft &&
                        core.RecoveriesInWindow >= effectiveQuota;
        var costMultiplier = core.RecoveryCostMultiplierPercent(
            world.Tick,
            overdraft,
            pressure.CostPercent);
        var canRecover = !core.Config.HasRecoveryQuota ||
                         core.RecoveriesInWindow < effectiveQuota ||
                         core.Config.HasRecoveryOverdraft;

        return new CoreRecoverySnapshot(
            civilizationId,
            core.RegionId,
            true,
            core.EnergyReserve,
            core.MaterialReserve,
            core.Config.MaxEnergyReserve,
            core.Config.MaxMaterialReserve,
            core.Config.RecoveryWindowTicks,
            core.Config.RecoveryQuotaPerWindow,
            core.RecoveriesInWindow,
            effectiveQuota,
            canRecover,
            resetsIn,
            core.RecoveryStreak,
            costMultiplier,
            core.AnchorTarget,
            CoreShellRules.CountAliveAnchors(world, civilizationId),
            pressure.AnchorGap,
            core.Config.AnchorRecoveryCostPercent,
            pressure.IsBesieged,
            core.Config.SiegeRecoveryCostPercent,
            core.Config.SiegeQuotaPenalty,
            core.Config.RecoveryMaintenanceMaterialCost,
            world.Wrecks.Count);
    }
}
