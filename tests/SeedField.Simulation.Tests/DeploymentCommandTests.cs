using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class DeploymentCommandTests
{
    [Fact]
    public void DeployCommandCreatesAnInstanceAndEmitsAnEvent()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .Build();
        var blueprint = world.Workshop.Publish(Blueprint("frame", materialCost: 30));
        var engine = new SimulationEngine(world);

        var result = engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            blueprint,
            "r0",
            new Vec2(400, 400)));

        Assert.True(result.Success);
        Assert.NotNull(result.EntityId);
        Assert.Equal(70, world.Regions["r0"].GetMaterial("A"));
        var deployed = Assert.Single(result.Events.OfType<EntityDeployed>());
        Assert.Equal(blueprint.BlueprintId, deployed.BlueprintId);
        Assert.Equal(blueprint.Version, deployed.BlueprintVersion);
    }

    [Fact]
    public void DeployCommandRespectsCoreProtection()
    {
        var world = new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("wilds", 10_000, 10_000)
            .AddCore("A", "core", new SeedCoreConfig(1, 1, 100, 100))
            .Build();
        var blueprint = world.Workshop.Publish(Blueprint("probe", materialCost: 0));
        var engine = new SimulationEngine(world);

        var result = engine.ExecuteDeploymentCommand(new DeployCommand(
            "B",
            blueprint,
            "core",
            new Vec2(100, 100)));

        Assert.False(result.Success);
        Assert.Equal(SimulationErrorCode.CoreProtected, result.ErrorCode);
        Assert.Empty(world.EnumerateEntities());
    }

    [Fact]
    public void UpgradeCommandReportsOldAndNewVersions()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .Build();
        var version1 = world.Workshop.Publish(Blueprint("frame", materialCost: 10, sensor: 1_000));
        var version2 = world.Workshop.Publish(Blueprint("frame", materialCost: 10, sensor: 2_000));
        var engine = new SimulationEngine(world);

        var deployed = engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            version1,
            "r0",
            new Vec2(100, 100)));
        var upgraded = engine.ExecuteDeploymentCommand(new UpgradeCommand(
            "A",
            deployed.EntityId!.Value,
            version2));

        Assert.True(upgraded.Success);
        var upgradeEvent = Assert.Single(upgraded.Events.OfType<EntityUpgraded>());
        Assert.Equal(1, upgradeEvent.OldVersion);
        Assert.Equal(2, upgradeEvent.NewVersion);
        Assert.Equal(2, world.FindEntity(deployed.EntityId.Value)!.BlueprintVersion);
    }

    [Fact]
    public void UpgradeCommandRejectsAnotherCivilizationsEntity()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "B",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 10,
                EnergyCapacity: 10,
                Material: 0))
            .Build();
        var blueprint = world.Workshop.Publish(Blueprint("frame", materialCost: 0));
        var engine = new SimulationEngine(world);

        var result = engine.ExecuteDeploymentCommand(new UpgradeCommand(
            "A",
            new EntityId(1),
            blueprint));

        Assert.False(result.Success);
        Assert.Equal(SimulationErrorCode.DeploymentRejected, result.ErrorCode);
    }

    private static DeviceBlueprint Blueprint(
        string id,
        long materialCost,
        int sensor = 1_000) =>
        new(
            id,
            0,
            id,
            20,
            10,
            Sensor: new SensorModule(sensor),
            Power: new PowerModule(1, 10),
            MaterialCost: materialCost);
}
