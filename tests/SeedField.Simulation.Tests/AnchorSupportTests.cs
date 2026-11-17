using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class AnchorSupportTests
{
    [Fact]
    public void AnchorsAddEnergyMaterialAndStorage()
    {
        var world = BuildWorld(
            new SeedCoreConfig(
                EnergyPerTick: 1,
                MaterialPerTick: 1,
                MaxEnergyReserve: 100,
                MaxMaterialReserve: 100,
                AnchorEnergyPerTick: 2,
                AnchorMaterialPerTick: 3,
                AnchorStorageBonus: 50),
            withAnchor: true);
        var core = world.Cores["A"];

        new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(3, core.EnergyReserve);
        Assert.Equal(4, core.MaterialReserve);
    }

    [Fact]
    public void AnchorStorageBonusRaisesTheCap()
    {
        var world = BuildWorld(
            new SeedCoreConfig(
                EnergyPerTick: 100,
                MaterialPerTick: 100,
                MaxEnergyReserve: 100,
                MaxMaterialReserve: 100,
                AnchorStorageBonus: 50),
            withAnchor: true);
        var core = world.Cores["A"];

        var engine = new SimulationEngine(world);
        for (var tick = 0; tick < 3; tick++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        Assert.Equal(150, core.EnergyReserve);
        Assert.Equal(150, core.MaterialReserve);
    }

    [Fact]
    public void NoAnchorsKeepLegacyRegeneration()
    {
        var world = BuildWorld(
            new SeedCoreConfig(
                EnergyPerTick: 1,
                MaterialPerTick: 1,
                MaxEnergyReserve: 100,
                MaxMaterialReserve: 100,
                AnchorEnergyPerTick: 2,
                AnchorMaterialPerTick: 3,
                AnchorStorageBonus: 50),
            withAnchor: false);
        var core = world.Cores["A"];

        new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(1, core.EnergyReserve);
        Assert.Equal(1, core.MaterialReserve);
    }

    [Fact]
    public void AnchorConfigRoundTripsAndOldSavesDefaultToZero()
    {
        var world = BuildWorld(
            new SeedCoreConfig(
                EnergyPerTick: 1,
                MaterialPerTick: 1,
                MaxEnergyReserve: 100,
                MaxMaterialReserve: 100,
                AnchorEnergyPerTick: 2,
                AnchorMaterialPerTick: 3,
                AnchorStorageBonus: 50),
            withAnchor: true);

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        Assert.Equal(2, loaded.Cores["A"].Config.AnchorEnergyPerTick);
        Assert.Equal(3, loaded.Cores["A"].Config.AnchorMaterialPerTick);
        Assert.Equal(50, loaded.Cores["A"].Config.AnchorStorageBonus);

        var data = WorldSaveSerializer.ToData(world) with { SchemaVersion = 18 };
        var migrated = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(data));
        Assert.Equal(0, migrated.Cores["A"].Config.AnchorEnergyPerTick);
        Assert.Equal(0, migrated.Cores["A"].Config.AnchorMaterialPerTick);
        Assert.Equal(0, migrated.Cores["A"].Config.AnchorStorageBonus);
    }

    private static WorldState BuildWorld(SeedCoreConfig config, bool withAnchor)
    {
        var builder = new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddCore("A", "core", config)
            .AddRegion("frontier", 1_000, 1_000);
        if (withAnchor)
        {
            builder.AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "frontier",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                IsCoreAnchor: true));
        }

        return builder.Build();
    }
}
