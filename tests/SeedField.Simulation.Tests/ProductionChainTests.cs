using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// M2 production chain: drilling yields ore, smelters refine ore into material,
/// fabricators turn material into components, and advanced construction consumes
/// components. Build time remains handled by the construction queue.
/// </summary>
public sealed class ProductionChainTests
{
    [Fact]
    public void SmelterConvertsRegionalOreIntoTerritoryMaterial()
    {
        var world = World(
            new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 10,
                EnergyCapacity: 100,
                Material: 0,
                Smelter: new SmelterModule(OrePerTick: 5, EnergyCostPerTick: 1)),
            ore: 20);
        var engine = new SimulationEngine(world);

        var result = engine.Step();

        var smelted = Assert.IsType<OreSmelted>(Assert.Single(result.Events));
        Assert.Equal(5, smelted.Amount);
        Assert.Equal(15, world.Regions["r0"].GetOre("A"));
        Assert.Equal(5, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(9, world.FindEntity(new EntityId(1))!.Energy);
    }

    [Fact]
    public void FabricatorConvertsMaterialIntoComponentsUpToItsCap()
    {
        var world = World(
            new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 10,
                EnergyCapacity: 100,
                Material: 0,
                Fabricator: new FabricatorModule(
                    ComponentsPerTick: 2,
                    MaterialPerComponent: 3,
                    EnergyCostPerTick: 1,
                    MaxComponents: 3)),
            material: 20);
        var engine = new SimulationEngine(world);
        engine.Step();
        engine.Step();

        Assert.Equal(3, world.Regions["r0"].GetComponents("A"));
        Assert.Equal(11, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(8, world.FindEntity(new EntityId(1))!.Energy);
    }

    [Fact]
    public void AdvancedConstructionRequiresAndRefundsComponents()
    {
        var world = World(
            new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 10,
                EnergyCapacity: 100,
                Material: 0,
                Sensor: new SensorModule(100)),
            material: 20,
            components: 0);
        var engine = new SimulationEngine(world);
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "advanced",
            0,
            "Advanced",
            MaxHealth: 100,
            EnergyCapacity: 100,
            Sensor: new SensorModule(100),
            MaterialCost: 5,
            BuildTicks: 1,
            ComponentCost: 2));
        var position = new Vec2(200, 200);

        var rejected = engine.ExecuteDeploymentCommand(
            new DeployCommand("A", blueprint, "r0", position));
        Assert.False(rejected.Success);
        Assert.Equal(SimulationErrorCode.InsufficientComponents, rejected.ErrorCode);

        world.Regions["r0"].AddComponents("A", 2);
        var queued = engine.ExecuteDeploymentCommand(
            new DeployCommand("A", blueprint, "r0", position));
        Assert.True(queued.Success);
        var order = Assert.Single(world.ConstructionOrders);
        Assert.Equal(2, order.ComponentsPaid);
        Assert.Equal(15, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(0, world.Regions["r0"].GetComponents("A"));

        var queuedEvent = Assert.IsType<ConstructionQueued>(Assert.Single(queued.Events));
        var cancelled = engine.ExecuteDeploymentCommand(
            new CancelConstructionCommand("A", queuedEvent.OrderId));
        Assert.True(cancelled.Success);
        Assert.Equal(20, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(2, world.Regions["r0"].GetComponents("A"));
    }

    [Fact]
    public void SaveRoundTripKeepsOreComponentsAndProductionModules()
    {
        var world = World(
            new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 10,
                EnergyCapacity: 100,
                Material: 0,
                Smelter: new SmelterModule(5, 1),
                Fabricator: new FabricatorModule(2, 3, 1, 10)),
            material: 7,
            ore: 11,
            components: 4);

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var entity = loaded.FindEntity(new EntityId(1))!;

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(11, loaded.Regions["r0"].GetOre("A"));
        Assert.Equal(4, loaded.Regions["r0"].GetComponents("A"));
        Assert.NotNull(entity.Smelter);
        Assert.NotNull(entity.Fabricator);
    }

    private static WorldState World(
        EntityDefinition entity,
        long material = 0,
        long ore = 0,
        long components = 0) =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", material, ore, components)
            .AddEntity(entity)
            .Build();
}
