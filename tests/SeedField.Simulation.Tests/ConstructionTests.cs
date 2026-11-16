using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class ConstructionTests
{
    [Fact]
    public void BuildTimeQueuesMaterialThenMaterialisesAnEntity()
    {
        var world = NewWorld();
        var blueprint = world.Workshop.Publish(Blueprint(buildTicks: 3));
        var engine = new SimulationEngine(world);

        var result = engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            blueprint,
            "r0",
            new Vec2(100, 100)));

        Assert.True(result.Success);
        Assert.Null(result.EntityId);
        Assert.Empty(world.EnumerateEntities());
        Assert.Equal(80, world.Regions["r0"].GetMaterial("A"));
        var queued = Assert.Single(result.Events.OfType<ConstructionQueued>());
        Assert.Equal(3, queued.CompleteTick);
        Assert.Single(world.ConstructionOrders);

        Step(engine, 3);

        var entity = Assert.Single(world.EnumerateEntities());
        Assert.Equal(blueprint.BlueprintId, entity.BlueprintId);
        Assert.Equal(80, world.Regions["r0"].GetMaterial("A"));
        Assert.Empty(world.ConstructionOrders);
        Assert.Equal(1, world.Statistics.ConstructionsCompletedTotal);
    }

    [Fact]
    public void CancelRefundsMaterialAndPreventsDeployment()
    {
        var world = NewWorld();
        var blueprint = world.Workshop.Publish(Blueprint(buildTicks: 3));
        var engine = new SimulationEngine(world);
        engine.ExecuteDeploymentCommand(new DeployCommand("A", blueprint, "r0", new Vec2(100, 100)));
        var orderId = Assert.Single(world.ConstructionOrders).Id;

        var result = engine.ExecuteDeploymentCommand(new CancelConstructionCommand(
            "A",
            orderId));

        Assert.True(result.Success);
        Assert.Empty(world.ConstructionOrders);
        Assert.Equal(100, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(20, world.Statistics.MaterialRefundedTotal);
        Assert.Single(result.Events.OfType<ConstructionCancelled>());

        Step(engine, 3);
        Assert.Empty(world.EnumerateEntities());
    }

    [Fact]
    public void AnotherCivilizationCannotCancelTheOrder()
    {
        var world = NewWorld();
        var blueprint = world.Workshop.Publish(Blueprint(buildTicks: 3));
        var engine = new SimulationEngine(world);
        engine.ExecuteDeploymentCommand(new DeployCommand("A", blueprint, "r0", new Vec2(100, 100)));
        var orderId = Assert.Single(world.ConstructionOrders).Id;

        var result = engine.ExecuteDeploymentCommand(new CancelConstructionCommand("B", orderId));

        Assert.False(result.Success);
        Assert.Single(world.ConstructionOrders);
        Assert.Equal(80, world.Regions["r0"].GetMaterial("A"));
    }

    [Fact]
    public void FailedConstructionRefundsMaterial()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 80)
            .Build();
        world.ConstructionOrders.Add(new ConstructionOrder(
            1,
            "A",
            "missing-blueprint",
            1,
            "r0",
            new Vec2(100, 100),
            0,
            1,
            20));
        var engine = new SimulationEngine(world);

        var result = engine.Step(Array.Empty<Intent>());

        var failed = Assert.Single(result.Events.OfType<ConstructionFailed>());
        Assert.Equal(20, failed.MaterialRefunded);
        Assert.Equal(100, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(20, world.Statistics.MaterialRefundedTotal);
        Assert.Empty(world.ConstructionOrders);
    }

    [Fact]
    public void SavePreservesConstructionOrder()
    {
        var world = NewWorld();
        var blueprint = world.Workshop.Publish(Blueprint(buildTicks: 3));
        var engine = new SimulationEngine(world);
        engine.ExecuteDeploymentCommand(new DeployCommand("A", blueprint, "r0", new Vec2(100, 100)));

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        Assert.Single(loaded.ConstructionOrders);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));

        var loadedEngine = new SimulationEngine(loaded);
        Step(loadedEngine, 3);

        Assert.Single(loaded.EnumerateEntities());
        Assert.Empty(loaded.ConstructionOrders);
    }

    private static void Step(SimulationEngine engine, int count)
    {
        for (var i = 0; i < count; i++)
        {
            engine.Step(Array.Empty<Intent>());
        }
    }

    private static WorldState NewWorld() =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .Build();

    private static DeviceBlueprint Blueprint(int buildTicks) =>
        new(
            "built",
            0,
            "Built",
            20,
            10,
            Sensor: new SensorModule(1_000),
            Power: new PowerModule(1, 10),
            MaterialCost: 20,
            BuildTicks: buildTicks);
}
