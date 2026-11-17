using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class RecoveryQuotaTests
{
    private static readonly RecoveryBlueprint MinimumEntity = new(
        "minimum-entity",
        RequiredEnergy: 40,
        RequiredMaterial: 30,
        MaxHealth: 10,
        Energy: 0,
        EnergyCapacity: 10,
        Material: 0);

    [Fact]
    public void RecoveryQuotaRejectsAfterTheConfiguredAttempts()
    {
        var world = CreateWorld(QuotaConfig(
            windowTicks: 10,
            quotaPerWindow: 2,
            growthPercent: 0,
            decayTicks: 0));
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());

        Assert.True(Bootstrap(engine).Success);
        Assert.True(Bootstrap(engine).Success);

        var denied = Bootstrap(engine);

        Assert.False(denied.Success);
        Assert.Equal(SimulationErrorCode.RecoveryQuotaExhausted, denied.ErrorCode);
        var quotaEvent = Assert.IsType<RecoveryQuotaDenied>(denied.Event);
        Assert.Equal(2, quotaEvent.Used);
        Assert.Equal(2, quotaEvent.Limit);
        // The first window starts at world creation (tick 0); the attempts run at tick 1.
        Assert.Equal(9, quotaEvent.ResetsInTicks);
        Assert.Equal(2, world.Cores["A"].RecoveriesInWindow);
        Assert.Equal(2, world.EnumerateEntities().Count());
    }

    [Fact]
    public void RecoveryQuotaResetsAfterTheWindow()
    {
        var world = CreateWorld(QuotaConfig(
            windowTicks: 10,
            quotaPerWindow: 2,
            growthPercent: 0,
            decayTicks: 0));
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());
        Bootstrap(engine);
        Bootstrap(engine);
        Assert.False(Bootstrap(engine).Success);

        for (var tick = 0; tick < 10; tick++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var core = world.Cores["A"];
        Assert.Equal(10, core.RecoveryWindowStartTick);
        Assert.Equal(0, core.RecoveriesInWindow);

        var recovered = Bootstrap(engine);

        Assert.True(recovered.Success);
        Assert.Equal(1, core.RecoveriesInWindow);
    }

    [Fact]
    public void RecoveryCostGrowsWithTheStreakAndDecaysAfterQuietTicks()
    {
        var world = CreateWorld(QuotaConfig(
            windowTicks: 0,
            quotaPerWindow: 0,
            growthPercent: 50,
            decayTicks: 20));
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());

        var first = Assert.IsType<EntityBootstrapped>(Bootstrap(engine).Event);
        var second = Assert.IsType<EntityBootstrapped>(Bootstrap(engine).Event);

        Assert.Equal(40, first.EnergySpent);
        Assert.Equal(30, first.MaterialSpent);
        Assert.Equal(60, second.EnergySpent);
        Assert.Equal(45, second.MaterialSpent);
        Assert.Equal(2, world.Cores["A"].RecoveryStreak);
        Assert.Equal(SimulationErrorCode.NoEnergy, Bootstrap(engine).ErrorCode);

        for (var tick = 0; tick < 20; tick++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var decayed = Assert.IsType<EntityBootstrapped>(Bootstrap(engine).Event);

        Assert.Equal(40, decayed.EnergySpent);
        Assert.Equal(30, decayed.MaterialSpent);
        Assert.Equal(1, world.Cores["A"].RecoveryStreak);
    }

    [Fact]
    public void RecoveryQuotaStateSurvivesSaveLoad()
    {
        var world = CreateWorld(QuotaConfig(
            windowTicks: 10,
            quotaPerWindow: 2,
            growthPercent: 50,
            decayTicks: 20,
            overdraftPercent: 75));
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());
        Bootstrap(engine);

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var core = loaded.Cores["A"];

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(10, core.Config.RecoveryWindowTicks);
        Assert.Equal(2, core.Config.RecoveryQuotaPerWindow);
        Assert.Equal(50, core.Config.RecoveryCostGrowthPercent);
        Assert.Equal(20, core.Config.RecoveryStreakDecayTicks);
        Assert.Equal(75, core.Config.RecoveryOverdraftCostPercent);
        Assert.Equal(1, core.RecoveriesInWindow);
        Assert.Equal(1, core.RecoveryStreak);
        Assert.Equal(1, core.LastRecoveryTick);
    }

    [Fact]
    public void V13SaveMigratesToUnlimitedRecovery()
    {
        var world = CreateWorld(QuotaConfig(
            windowTicks: 10,
            quotaPerWindow: 1,
            growthPercent: 50,
            decayTicks: 20));
        var data = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 13,
            Cores = new[]
            {
                new CoreSaveData(
                    "A",
                    "core",
                    EnergyPerTick: 100,
                    MaterialPerTick: 100,
                    MaxEnergyReserve: 100,
                    MaxMaterialReserve: 100,
                    EnergyReserve: 100,
                    MaterialReserve: 100),
            },
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(data));
        var core = loaded.Cores["A"];

        Assert.Equal(0, core.Config.RecoveryWindowTicks);
        Assert.Equal(0, core.Config.RecoveryQuotaPerWindow);
        Assert.Equal(0, core.Config.RecoveryCostGrowthPercent);
        Assert.Equal(0, core.RecoveriesInWindow);
        Assert.Equal(0, core.RecoveryStreak);
    }

    [Fact]
    public void OverdraftAllowsRecoveryAtHigherCostInsteadOfRejecting()
    {
        var world = CreateWorld(QuotaConfig(
            windowTicks: 10,
            quotaPerWindow: 1,
            growthPercent: 0,
            decayTicks: 0,
            overdraftPercent: 200));
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());
        var cheap = new RecoveryBlueprint("cheap", 10, 10, 10, 0, 10, 0);

        var first = engine.TryBootstrapFromCore("A", cheap, new Vec2(500, 500));
        var overdraft = engine.TryBootstrapFromCore("A", cheap, new Vec2(600, 500));

        Assert.True(first.Success);
        Assert.True(overdraft.Success);
        var firstEvent = Assert.IsType<EntityBootstrapped>(first.Event);
        var overdraftEvent = Assert.IsType<EntityBootstrapped>(overdraft.Event);
        Assert.Equal(10, firstEvent.EnergySpent);
        Assert.Equal(30, overdraftEvent.EnergySpent);
        Assert.Equal(30, overdraftEvent.MaterialSpent);
        Assert.Equal(2, world.Cores["A"].RecoveriesInWindow);
        Assert.Equal(60, world.Cores["A"].EnergyReserve);
        Assert.Equal(60, world.Cores["A"].MaterialReserve);
    }

    [Fact]
    public void RestoreCommandUsesTheSameQuota()
    {
        var world = CreateWorld(QuotaConfig(
            windowTicks: 10,
            quotaPerWindow: 1,
            growthPercent: 0,
            decayTicks: 0));
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "execution",
            0,
            "Execution",
            10,
            100,
            Drive: new DriveModule(50, 1)));
        world.Cores["A"].RecordLatestProduct(DeviceBranch.Execution, blueprint);
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());

        var first = engine.ExecuteRecoveryCommand(new RestoreCoreProductCommand(
            "A",
            DeviceBranch.Execution,
            new Vec2(500, 500),
            10,
            10));
        var second = engine.ExecuteRecoveryCommand(new RestoreCoreProductCommand(
            "A",
            DeviceBranch.Execution,
            new Vec2(600, 500),
            10,
            10));

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(SimulationErrorCode.RecoveryQuotaExhausted, second.ErrorCode);
        Assert.IsType<RecoveryQuotaDenied>(second.Event);
        Assert.Single(world.EnumerateEntities());
    }

    private static BootstrapResult Bootstrap(SimulationEngine engine) =>
        engine.TryBootstrapFromCore("A", MinimumEntity, new Vec2(500, 500));

    private static SeedCoreConfig QuotaConfig(
        long windowTicks,
        int quotaPerWindow,
        int growthPercent,
        long decayTicks,
        int overdraftPercent = 0) =>
        new(
            EnergyPerTick: 100,
            MaterialPerTick: 100,
            MaxEnergyReserve: 100,
            MaxMaterialReserve: 100,
            RecoveryWindowTicks: windowTicks,
            RecoveryQuotaPerWindow: quotaPerWindow,
            RecoveryCostGrowthPercent: growthPercent,
            RecoveryStreakDecayTicks: decayTicks,
            RecoveryOverdraftCostPercent: overdraftPercent);

    private static WorldState CreateWorld(SeedCoreConfig config) =>
        new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("wilds", 10_000, 10_000)
            .AddCore("A", "core", config)
            .Build();
}
