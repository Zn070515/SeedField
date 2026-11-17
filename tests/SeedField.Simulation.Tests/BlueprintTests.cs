using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class BlueprintTests
{
    [Fact]
    public void PublishingANewVersionDoesNotMutateDeployedInstances()
    {
        var workshop = new WorkshopService();
        var version1 = workshop.Publish(ScoutBlueprint("Scout Mk1", health: 100, sensor: 1_000));
        var world = NewWorld();
        var entity = DeploymentService.Deploy(world, version1, "A", "r0", new Vec2(100, 100));

        var version2 = workshop.Publish(ScoutBlueprint("Scout Mk2", health: 150, sensor: 2_000));

        Assert.Equal(2, version2.Version);
        Assert.Equal(1, entity.BlueprintVersion);
        Assert.Equal(100, entity.MaxHealth);
        Assert.Equal(1_000, entity.Sensor!.RangeMm);
        Assert.Equal(version1.Version, entity.BlueprintVersion);
    }

    [Fact]
    public void ExplicitUpgradeMovesTheInstanceToTheNewVersion()
    {
        var workshop = new WorkshopService();
        var version1 = workshop.Publish(ScoutBlueprint("Scout Mk1", health: 100, sensor: 1_000));
        var version2 = workshop.Publish(
            ScoutBlueprint("Scout Mk2", health: 80, sensor: 2_000, energyCapacity: 300));
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(400, 400),
                MaxHealth: 100,
                Energy: 400,
                EnergyCapacity: 500,
                Material: 0,
                Sensor: version1.Sensor,
                Drive: version1.Drive,
                Comm: version1.Comm,
                BlueprintId: version1.BlueprintId,
                BlueprintVersion: version1.Version,
                ControllerProgramId: version1.ControllerProgramId))
            .Build();
        var entity = world.FindEntity(new EntityId(1))!;

        DeploymentService.Upgrade(world, entity, version2);

        Assert.Equal(2, entity.BlueprintVersion);
        Assert.Equal(80, entity.MaxHealth);
        Assert.Equal(80, entity.Health);
        Assert.Equal(300, entity.Energy);
        Assert.Equal(300, entity.EnergyCapacity);
        Assert.Equal(2_000, entity.Sensor!.RangeMm);
        Assert.Equal(new Vec2(400, 400), entity.Position);
    }

    [Fact]
    public void MinimumUsableRequiresAllFiveBranches()
    {
        var workshop = new WorkshopService();

        workshop.Publish(new DeviceBlueprint("power", 0, "Power", 10, 100, Power: new PowerModule(1, 100)));
        Assert.False(workshop.IsMinimumUsable);

        workshop.Publish(new DeviceBlueprint("sensor", 0, "Sensor", 10, 0, Sensor: new SensorModule(100)));
        workshop.Publish(new DeviceBlueprint("comm", 0, "Comm", 10, 0, Comm: new CommModule(1, 1)));
        workshop.Publish(new DeviceBlueprint("state", 0, "State", 10, 0, State: new StateModule(1, 10)));
        Assert.False(workshop.IsMinimumUsable);

        workshop.Publish(new DeviceBlueprint("drive", 0, "Drive", 10, 0, Drive: new DriveModule(10, 1)));
        Assert.True(workshop.IsMinimumUsable);
    }

    [Fact]
    public void PublishingABlueprintWithNoBranchesIsRejected()
    {
        var workshop = new WorkshopService();

        var exception = Assert.Throws<BlueprintValidationException>(() =>
            workshop.Publish(new DeviceBlueprint("empty", 0, "Empty", 10, 0)));

        Assert.Equal("NO_BRANCH", exception.Code);
    }

    [Fact]
    public void UnpublishedBlueprintCannotBeDeployed()
    {
        var world = NewWorld();
        var draft = ScoutBlueprint("Draft", 100, 1_000);

        var exception = Assert.Throws<BlueprintValidationException>(() =>
            DeploymentService.Deploy(world, draft, "A", "r0", new Vec2(100, 100)));

        Assert.Equal("UNPUBLISHED_BLUEPRINT", exception.Code);
    }

    [Fact]
    public void SaveLoadPreservesBlueprintReferences()
    {
        var workshop = new WorkshopService();
        var blueprint = workshop.Publish(ScoutBlueprint("Scout", 100, 1_000));
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: blueprint.MaxHealth,
                Energy: 250,
                EnergyCapacity: blueprint.EnergyCapacity,
                Material: 0,
                Sensor: blueprint.Sensor,
                Drive: blueprint.Drive,
                Comm: blueprint.Comm,
                BlueprintId: blueprint.BlueprintId,
                BlueprintVersion: blueprint.Version,
                ControllerProgramId: blueprint.ControllerProgramId))
            .Build();

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var loadedEntity = Assert.Single(loaded.EnumerateEntities());

        Assert.Equal(blueprint.BlueprintId, loadedEntity.BlueprintId);
        Assert.Equal(blueprint.Version, loadedEntity.BlueprintVersion);
        Assert.Equal(blueprint.ControllerProgramId, loadedEntity.ControllerProgramId);
        Assert.Equal(250, loadedEntity.Energy);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    private static DeviceBlueprint ScoutBlueprint(
        string name,
        int health,
        int sensor,
        long energyCapacity = 500) =>
        new(
            BlueprintId: "scout",
            Version: 0,
            Name: name,
            MaxHealth: health,
            EnergyCapacity: energyCapacity,
            Sensor: new SensorModule(sensor),
            Drive: new DriveModule(50, EnergyCostPerMove: 1),
            Comm: new CommModule(1, EnergyCostPerMessage: 1),
            ControllerProgramId: "program.scout");

    private static WorldState NewWorld() =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .Build();
}
