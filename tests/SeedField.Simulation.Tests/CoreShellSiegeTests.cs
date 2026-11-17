using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class CoreShellSiegeTests
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
    public void LosingAShellAnchorIncreasesRecoveryCostUntilItIsRebuilt()
    {
        var world = CreateShellWorld(ShellConfig(anchorCostPercent: 50));
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());

        var destroyed = engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(2), new EntityId(1)),
        });
        Assert.Contains(destroyed.Events, item => item is EntityDestroyed);

        var afterLoss = engine.TryBootstrapFromCore(
            "A",
            MinimumEntity,
            new Vec2(500, 500));

        Assert.True(afterLoss.Success);
        var lostEvent = Assert.IsType<EntityBootstrapped>(afterLoss.Event);
        Assert.Equal(60, lostEvent.EnergySpent);
        Assert.Equal(45, lostEvent.MaterialSpent);

        var anchor = world.Workshop.Publish(AnchorBlueprint());
        var deployed = engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            anchor,
            "shell",
            new Vec2(900, 900)));
        Assert.True(deployed.Success);

        var rebuilt = engine.TryBootstrapFromCore(
            "A",
            MinimumEntity,
            new Vec2(600, 500));

        Assert.True(rebuilt.Success);
        var rebuiltEvent = Assert.IsType<EntityBootstrapped>(rebuilt.Event);
        Assert.Equal(40, rebuiltEvent.EnergySpent);
        Assert.Equal(30, rebuiltEvent.MaterialSpent);
        Assert.Equal(1, world.Cores["A"].AnchorTarget);
    }

    [Fact]
    public void HostilePresenceAddsSiegeCostAndReducesTheEffectiveQuota()
    {
        var world = CreateShellWorld(ShellConfig(
            anchorCostPercent: 0,
            siegeCostPercent: 100,
            siegeQuotaPenalty: 1,
            windowTicks: 100,
            quotaPerWindow: 2));
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());

        var first = engine.TryBootstrapFromCore(
            "A",
            MinimumEntity,
            new Vec2(500, 500));
        var denied = engine.TryBootstrapFromCore(
            "A",
            MinimumEntity,
            new Vec2(600, 500));

        Assert.True(first.Success);
        var firstEvent = Assert.IsType<EntityBootstrapped>(first.Event);
        Assert.Equal(80, firstEvent.EnergySpent);
        Assert.Equal(60, firstEvent.MaterialSpent);
        Assert.Equal(SimulationErrorCode.RecoveryQuotaExhausted, denied.ErrorCode);
        var quotaEvent = Assert.IsType<RecoveryQuotaDenied>(denied.Event);
        Assert.Equal(1, quotaEvent.Limit);

        world.Regions["shell"].Entities.Remove(new EntityId(2));

        var afterLift = engine.TryBootstrapFromCore(
            "A",
            MinimumEntity,
            new Vec2(700, 500));

        Assert.True(afterLift.Success);
        var liftedEvent = Assert.IsType<EntityBootstrapped>(afterLift.Event);
        Assert.Equal(40, liftedEvent.EnergySpent);
        Assert.Equal(30, liftedEvent.MaterialSpent);
    }

    [Fact]
    public void ShellStateSurvivesSaveLoad()
    {
        var world = CreateShellWorld(ShellConfig(
            anchorCostPercent: 50,
            siegeCostPercent: 100,
            siegeQuotaPenalty: 1));
        var engine = new SimulationEngine(world);
        engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(2), new EntityId(1)),
        });

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(1, loaded.Cores["A"].AnchorTarget);
        Assert.Equal(50, loaded.Cores["A"].Config.AnchorRecoveryCostPercent);
        Assert.Equal(100, loaded.Cores["A"].Config.SiegeRecoveryCostPercent);
        Assert.Equal(1, loaded.Cores["A"].Config.SiegeQuotaPenalty);
        Assert.DoesNotContain(
            loaded.EnumerateEntities(),
            entity => entity.IsCoreAnchor);
    }

    [Fact]
    public void V14SaveMigratesToNoShellPenalty()
    {
        var world = new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("shell", 10_000, 10_000)
            .AddLink("l0", "core", "shell", latencyTicks: 1)
            .AddCore("A", "core", ShellConfig(anchorCostPercent: 50))
            .Build();
        var data = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 14,
            Cores = new[]
            {
                new CoreSaveData(
                    "A",
                    "core",
                    EnergyPerTick: 200,
                    MaterialPerTick: 200,
                    MaxEnergyReserve: 200,
                    MaxMaterialReserve: 200,
                    EnergyReserve: 200,
                    MaterialReserve: 200),
            },
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(data));
        var core = loaded.Cores["A"];

        Assert.Equal(0, core.Config.AnchorRecoveryCostPercent);
        Assert.Equal(0, core.Config.SiegeRecoveryCostPercent);
        Assert.Equal(0, core.Config.SiegeQuotaPenalty);
        Assert.Equal(0, core.AnchorTarget);
    }

    private static SeedCoreConfig ShellConfig(
        int anchorCostPercent,
        int siegeCostPercent = 0,
        int siegeQuotaPenalty = 0,
        long windowTicks = 100,
        int quotaPerWindow = 10) =>
        new(
            EnergyPerTick: 200,
            MaterialPerTick: 200,
            MaxEnergyReserve: 200,
            MaxMaterialReserve: 200,
            RecoveryWindowTicks: windowTicks,
            RecoveryQuotaPerWindow: quotaPerWindow,
            AnchorRecoveryCostPercent: anchorCostPercent,
            SiegeRecoveryCostPercent: siegeCostPercent,
            SiegeQuotaPenalty: siegeQuotaPenalty);

    private static DeviceBlueprint AnchorBlueprint() =>
        new(
            "anchor",
            0,
            "Shell Anchor",
            MaxHealth: 10,
            EnergyCapacity: 10,
            IsCoreAnchor: true);

    private static WorldState CreateShellWorld(SeedCoreConfig config) =>
        new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("shell", 10_000, 10_000)
            .AddLink("l0", "core", "shell", latencyTicks: 1)
            .AddCore("A", "core", config)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "shell",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                IsCoreAnchor: true))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "shell",
                new Vec2(500, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                Sensor: new SensorModule(1_000),
                Weapon: new WeaponModule(1_000, Damage: 10, EnergyCost: 0, CooldownTicks: 0)))
            .Build();
}
