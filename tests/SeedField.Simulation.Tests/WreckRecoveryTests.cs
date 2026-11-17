using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class WreckRecoveryTests
{
    [Fact]
    public void DestroyedDeviceLeavesAWreckWithBlueprintMaterialFraction()
    {
        var (world, engine, blueprint) = CreateWorldWithDeployedVictim(percent: 50);

        var result = engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });

        Assert.Contains(result.Events, item => item is EntityDestroyed);
        Assert.Contains(result.Events, item => item is WreckCreated);
        var wreck = Assert.Single(world.Wrecks.Values);
        Assert.Equal("r0", wreck.RegionId);
        Assert.Equal("A", wreck.OwnerCivilizationId);
        Assert.Equal(blueprint.BlueprintId, wreck.BlueprintId);
        Assert.Equal(blueprint.Version, wreck.BlueprintVersion);
        Assert.Equal(50, wreck.MaterialRecoverable);
        Assert.Equal(0, world.Regions["r0"].GetMaterial("A"));
    }

    [Fact]
    public void OwnerCanRecoverTheWreckAndGetsMaterialBack()
    {
        var (world, engine, _) = CreateWorldWithDeployedVictim(percent: 50);
        engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });
        var wreckId = Assert.Single(world.Wrecks.Keys);

        var result = engine.ExecuteWreckCommand(
            new RecoverWreckCommand("A", wreckId));

        Assert.True(result.Success);
        Assert.Contains(result.Events, item => item is WreckSalvaged);
        Assert.Empty(world.Wrecks);
        Assert.Equal(50, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(50, world.Statistics.MaterialSalvagedTotal);
    }

    [Fact]
    public void EnemyCanSalvageOnlyWhilePresentInTheRegion()
    {
        var (world, engine, _) = CreateWorldWithDeployedVictim(percent: 50);
        engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });
        var wreckId = Assert.Single(world.Wrecks.Keys);

        var absent = engine.ExecuteWreckCommand(
            new RecoverWreckCommand("C", wreckId));
        Assert.False(absent.Success);
        Assert.Equal(SimulationErrorCode.WreckUnavailable, absent.ErrorCode);

        var salvaged = engine.ExecuteWreckCommand(
            new RecoverWreckCommand("B", wreckId));
        Assert.True(salvaged.Success);
        Assert.Equal(50, world.Regions["r0"].GetMaterial("B"));
        Assert.Empty(world.Wrecks);
    }

    [Fact]
    public void UnknownWreckIsRejectedWithoutSideEffects()
    {
        var (world, engine, _) = CreateWorldWithDeployedVictim(percent: 50);

        var result = engine.ExecuteWreckCommand(
            new RecoverWreckCommand("A", 999));

        Assert.False(result.Success);
        Assert.Equal(SimulationErrorCode.WreckNotFound, result.ErrorCode);
        Assert.Empty(world.Wrecks);
    }

    [Fact]
    public void WreckStateSurvivesSaveLoad()
    {
        var (world, engine, _) = CreateWorldWithDeployedVictim(percent: 50);
        engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        var wreck = Assert.Single(loaded.Wrecks.Values);
        Assert.Equal(50, wreck.MaterialRecoverable);
        Assert.Equal(world.NextWreckId, loaded.NextWreckId);
        Assert.Equal(50, loaded.WreckRecoveryPercent);
    }

    [Fact]
    public void ZeroRecoveryPercentLeavesNoWreck()
    {
        var (world, engine, _) = CreateWorldWithDeployedVictim(percent: 0);

        engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });

        Assert.Empty(world.Wrecks);
    }

    [Fact]
    public void V15SaveMigratesToNoWrecks()
    {
        var (world, _, _) = CreateWorldWithDeployedVictim(percent: 50);
        var data = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 15,
            Wrecks = null,
            NextWreckId = 0,
            WreckRecoveryPercent = 0,
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(data));

        Assert.Empty(loaded.Wrecks);
        Assert.Equal(0, loaded.WreckRecoveryPercent);
        Assert.Equal(1, loaded.NextWreckId);
    }

    private static (WorldState World, SimulationEngine Engine, DeviceBlueprint Blueprint)
        CreateWorldWithDeployedVictim(int percent)
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .SetWreckRecoveryPercent(percent)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "B",
                "r0",
                new Vec2(500, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                Sensor: new SensorModule(1_000),
                Weapon: new WeaponModule(1_000, Damage: 10, EnergyCost: 0, CooldownTicks: 0)))
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "device",
            0,
            "Device",
            MaxHealth: 10,
            EnergyCapacity: 10,
            MaterialCost: 100));
        var engine = new SimulationEngine(world);
        var deployed = engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            blueprint,
            "r0",
            new Vec2(100, 100)));
        Assert.True(deployed.Success);
        return (world, engine, blueprint);
    }
}
