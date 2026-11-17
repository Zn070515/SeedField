namespace SeedField.Simulation.Tests;

public sealed class CoreVisualStateRulesTests
{
    [Fact]
    public void MissingCoreIsAbsent()
    {
        var snapshot = Snapshot(hasCore: false, besieged: false);

        Assert.Equal(
            CoreVisualState.Absent,
            CoreVisualStateRules.Classify(snapshot, tick: 10, bootstrapUntilTick: 20));
    }

    [Fact]
    public void FullReserveOutsideSiegeIsIdle()
    {
        var snapshot = Snapshot(hasCore: true, besieged: false);

        Assert.Equal(
            CoreVisualState.Idle,
            CoreVisualStateRules.Classify(snapshot, tick: 10, bootstrapUntilTick: 0));
    }

    [Theory]
    [InlineData(60, 100)]
    [InlineData(100, 60)]
    public void BelowEitherReserveCapIsRegenerating(long energy, long material)
    {
        var snapshot = Snapshot(
            hasCore: true,
            besieged: false,
            energyReserve: energy,
            materialReserve: material);

        Assert.Equal(
            CoreVisualState.Regenerating,
            CoreVisualStateRules.Classify(snapshot, tick: 10, bootstrapUntilTick: 0));
    }

    [Fact]
    public void BootstrapWindowOutranksRegenerating()
    {
        var snapshot = Snapshot(hasCore: true, besieged: false, energyReserve: 40);

        Assert.Equal(
            CoreVisualState.Bootstrap,
            CoreVisualStateRules.Classify(snapshot, tick: 10, bootstrapUntilTick: 20));
    }

    [Fact]
    public void ExpiredBootstrapFallsBackToRegenerating()
    {
        var snapshot = Snapshot(hasCore: true, besieged: false, energyReserve: 40);

        Assert.Equal(
            CoreVisualState.Regenerating,
            CoreVisualStateRules.Classify(snapshot, tick: 20, bootstrapUntilTick: 20));
    }

    [Fact]
    public void SiegeOutranksBootstrapAndRegenerating()
    {
        var snapshot = Snapshot(hasCore: true, besieged: true, energyReserve: 40);

        Assert.Equal(
            CoreVisualState.Besieged,
            CoreVisualStateRules.Classify(snapshot, tick: 10, bootstrapUntilTick: 20));
    }

    private static CoreRecoverySnapshot Snapshot(
        bool hasCore,
        bool besieged,
        long energyReserve = 100,
        long materialReserve = 100) =>
        new(
            CivilizationId: "A",
            RegionId: "core-a",
            HasCore: hasCore,
            EnergyReserve: energyReserve,
            MaterialReserve: materialReserve,
            MaxEnergyReserve: 100,
            MaxMaterialReserve: 100,
            RecoveryWindowTicks: 60,
            QuotaPerWindow: 2,
            RecoveriesInWindow: 0,
            EffectiveQuota: 2,
            CanRecover: true,
            WindowResetsInTicks: 60,
            RecoveryStreak: 0,
            CostMultiplierPercent: 100,
            AnchorTarget: 1,
            AliveAnchors: 1,
            AnchorGap: 0,
            AnchorRecoveryCostPercent: 0,
            IsBesieged: besieged,
            SiegeRecoveryCostPercent: 0,
            SiegeQuotaPenalty: 0,
            RecoveryMaintenanceMaterialCost: 0,
            WreckCount: 0);
}
