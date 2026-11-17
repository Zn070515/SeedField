using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class CoreRecoveryMaintenanceTests
{
    private static readonly RecoveryBlueprint MinimumEntity = new(
        "minimum",
        RequiredEnergy: 40,
        RequiredMaterial: 30,
        MaxHealth: 10,
        Energy: 0,
        EnergyCapacity: 10,
        Material: 0);

    [Fact]
    public void MaintainCoreClearsQuotaAndDebtAndChargesMaterial()
    {
        var world = CreateWorld(maintenanceCost: 10, siegePenalty: 0);
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());
        engine.ExecuteRecoveryCommand(new BootstrapCoreCommand(
            "A",
            MinimumEntity,
            new Vec2(500, 500)));
        var core = world.Cores["A"];
        Assert.Equal(1, core.RecoveryStreak);
        Assert.Equal(1, core.RecoveriesInWindow);

        var maintained = engine.ExecuteRecoveryCommand(
            new MaintainCoreCommand("A"));

        Assert.True(maintained.Success);
        Assert.IsType<CoreMaintained>(maintained.Event);
        Assert.Equal(60, core.MaterialReserve);
        Assert.Equal(40, world.Statistics.MaterialSpentTotal);
        Assert.Equal(0, core.RecoveryStreak);
        Assert.Equal(0, core.RecoveriesInWindow);
        Assert.Equal(-1, core.LastRecoveryTick);

        var next = engine.ExecuteRecoveryCommand(new BootstrapCoreCommand(
            "A",
            MinimumEntity,
            new Vec2(600, 500)));
        var nextEvent = Assert.IsType<EntityBootstrapped>(next.Event);
        Assert.Equal(40, nextEvent.EnergySpent);
        Assert.Equal(30, nextEvent.MaterialSpent);
    }

    [Fact]
    public void MaintainCoreFailsWithoutEnoughReserve()
    {
        var world = CreateWorld(maintenanceCost: 50, siegePenalty: 0);
        var engine = new SimulationEngine(world);
        var core = world.Cores["A"];

        var maintained = engine.ExecuteRecoveryCommand(
            new MaintainCoreCommand("A"));

        Assert.False(maintained.Success);
        Assert.Equal(SimulationErrorCode.InsufficientMaterial, maintained.ErrorCode);
        Assert.Equal(0, core.MaterialReserve);
        Assert.Equal(0, world.Statistics.MaterialSpentTotal);
    }

    [Fact]
    public void MaintainCoreRejectsUnknownCivilization()
    {
        var world = CreateWorld(maintenanceCost: 10, siegePenalty: 0);

        var maintained = new SimulationEngine(world).ExecuteRecoveryCommand(
            new MaintainCoreCommand("X"));

        Assert.False(maintained.Success);
        Assert.Equal(SimulationErrorCode.CoreNotAvailable, maintained.ErrorCode);
    }

    [Fact]
    public void DiagnosticsReportQuotaDebtAnchorsAndSiege()
    {
        var world = CreateWorld(
            maintenanceCost: 10,
            siegePenalty: 1,
            besieged: true);
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());
        engine.ExecuteRecoveryCommand(new BootstrapCoreCommand(
            "A",
            MinimumEntity,
            new Vec2(500, 500)));

        var snapshot = CoreRecoveryDiagnosticsBuilder.Build(world, "A");

        Assert.True(snapshot.HasCore);
        Assert.Equal(1, snapshot.RecoveriesInWindow);
        Assert.Equal(1, snapshot.EffectiveQuota);
        Assert.Equal(1, snapshot.RecoveryStreak);
        Assert.Equal(250, snapshot.CostMultiplierPercent);
        Assert.True(snapshot.IsBesieged);
        Assert.Equal(1, snapshot.SiegeQuotaPenalty);
        Assert.Equal(10, snapshot.RecoveryMaintenanceMaterialCost);
        Assert.Equal(0, snapshot.AnchorGap);
        Assert.Equal(0, snapshot.WreckCount);
    }

    [Fact]
    public void MaintenanceCostSurvivesSaveLoad()
    {
        var world = CreateWorld(maintenanceCost: 25, siegePenalty: 0);
        new SimulationEngine(world).Step(Array.Empty<Intent>());

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(25, loaded.Cores["A"].Config.RecoveryMaintenanceMaterialCost);
    }

    private static WorldState CreateWorld(
        int maintenanceCost,
        int siegePenalty,
        bool besieged = false)
    {
        var builder = new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("shell", 10_000, 10_000)
            .AddLink("l0", "core", "shell", latencyTicks: 1);
        if (besieged)
        {
            builder.AddEntity(new EntityDefinition(
                new EntityId(1),
                "B",
                "shell",
                new Vec2(500, 500),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0));
        }

        return builder
            .AddCore(
                "A",
                "core",
                new SeedCoreConfig(
                    EnergyPerTick: 100,
                    MaterialPerTick: 100,
                    MaxEnergyReserve: 100,
                    MaxMaterialReserve: 100,
                    RecoveryWindowTicks: 100,
                    RecoveryQuotaPerWindow: 1,
                    RecoveryCostGrowthPercent: 50,
                    SiegeRecoveryCostPercent: 100,
                    SiegeQuotaPenalty: siegePenalty,
                    RecoveryMaintenanceMaterialCost: maintenanceCost))
            .Build();
    }
}
