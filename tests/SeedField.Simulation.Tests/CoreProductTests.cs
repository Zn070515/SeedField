using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class CoreProductTests
{
    [Fact]
    public void CoreStoresLatestProductPerBranchAndRestoresIt()
    {
        var world = NewCoreWorld();
        var products = PublishFiveBranches(world.Workshop);
        var core = world.Cores["A"];

        foreach (var product in products)
        {
            core.RecordLatestProduct(product.Branch, product.Blueprint);
        }

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var loadedCore = loaded.Cores["A"];

        Assert.Equal(5, loadedCore.LatestProducts.Count);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));

        var engine = new SimulationEngine(loaded);
        for (var tick = 0; tick < 100; tick++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var result = engine.TryRestoreLatestProduct(
            "A",
            DeviceBranch.Execution,
            new Vec2(500, 500),
            energyCost: 10,
            materialCost: 10);

        Assert.True(result.Success);
        var entity = Assert.Single(loaded.EnumerateEntities());
        Assert.Equal("execution", entity.BlueprintId);
        Assert.Equal(1, entity.BlueprintVersion);
        Assert.Equal(90, loadedCore.EnergyReserve);
        Assert.Equal(90, loadedCore.MaterialReserve);
    }

    [Fact]
    public void RestoreFailsWithoutARegisteredProduct()
    {
        var world = NewCoreWorld();
        var engine = new SimulationEngine(world);

        var result = engine.TryRestoreLatestProduct(
            "A",
            DeviceBranch.Sensing,
            new Vec2(500, 500),
            0,
            0);

        Assert.False(result.Success);
        Assert.Equal(SimulationErrorCode.DeploymentRejected, result.ErrorCode);
        Assert.Empty(world.EnumerateEntities());
    }

    [Fact]
    public void RecordingAProductForAnUncoveredBranchIsRejected()
    {
        var world = NewCoreWorld();
        var sensing = world.Workshop.Publish(
            new DeviceBlueprint("sensor", 0, "Sensor", 10, 0, Sensor: new SensorModule(1_000)));

        var exception = Assert.Throws<BlueprintValidationException>(() =>
            world.Cores["A"].RecordLatestProduct(DeviceBranch.Execution, sensing));

        Assert.Equal("BRANCH_NOT_COVERED", exception.Code);
    }

    [Fact]
    public void WorkshopVersionsSurviveSaveAndCanBeDeployed()
    {
        var world = NewCoreWorld();
        var version1 = world.Workshop.Publish(Scout("Mk1", 100));
        var version2 = world.Workshop.Publish(Scout("Mk2", 120));

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(2, loaded.Workshop.GetVersions("scout").Count);
        Assert.Equal(version2, loaded.Workshop.GetVersion("scout", 2));
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));

        var entity = DeploymentService.Deploy(
            loaded,
            loaded.Workshop.GetVersion("scout", 2)!,
            "A",
            "core",
            new Vec2(400, 400));

        Assert.Equal("scout", entity.BlueprintId);
        Assert.Equal(2, entity.BlueprintVersion);
    }

    private static List<(DeviceBranch Branch, DeviceBlueprint Blueprint)> PublishFiveBranches(
        WorkshopService workshop) =>
        new()
        {
            (DeviceBranch.Energy, workshop.Publish(
                new DeviceBlueprint("power", 0, "Power", 10, 100, Power: new PowerModule(1, 100)))),
            (DeviceBranch.Sensing, workshop.Publish(
                new DeviceBlueprint("sensing", 0, "Sensing", 10, 0, Sensor: new SensorModule(1_000)))),
            (DeviceBranch.Communication, workshop.Publish(
                new DeviceBlueprint("comm", 0, "Comm", 10, 0, Comm: new CommModule(1, 1)))),
            (DeviceBranch.State, workshop.Publish(
                new DeviceBlueprint("state", 0, "State", 10, 0, State: new StateModule(1, 10)))),
            (DeviceBranch.Execution, workshop.Publish(
                new DeviceBlueprint("execution", 0, "Execution", 100, 100, Drive: new DriveModule(50, 1)))),
        };

    private static DeviceBlueprint Scout(string name, int health) =>
        new(
            "scout",
            0,
            name,
            health,
            100,
            Sensor: new SensorModule(1_000),
            Drive: new DriveModule(50, 1));

    private static WorldState NewCoreWorld() =>
        new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("wilds", 10_000, 10_000)
            .AddCore("A", "core", new SeedCoreConfig(1, 1, 100, 100))
            .Build();
}
