using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class EnergyGridTests
{
    [Fact]
    public void EntityCanStoreAndDrawLocalGridEnergy()
    {
        var world = NewWorld(withLink: false);
        var engine = new SimulationEngine(world);

        engine.Step(new Intent[] { new SendEnergyIntent(new EntityId(1), 40) });

        var entity = world.FindEntity(new EntityId(1))!;
        Assert.Equal(60, entity.Energy);
        Assert.Equal(40, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(40, world.Statistics.EnergyStoredTotal);

        engine.Step(new Intent[] { new DrawEnergyIntent(new EntityId(1), 30) });

        Assert.Equal(90, entity.Energy);
        Assert.Equal(10, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(30, world.Statistics.EnergyDrawnTotal);
    }

    [Fact]
    public void RemoteEnergyArrivesAfterLinkLatency()
    {
        var world = NewWorld(withLink: true);
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[]
        {
            new SendEnergyIntent(new EntityId(1), 40, "r1"),
        });

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        Assert.Equal(60, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(0, world.Regions["r1"].GetEnergy("A"));

        var shipment = Assert.Single(world.InTransitEnergy);
        Assert.Equal(3, shipment.ArriveTick);
        Assert.Equal("l01", shipment.Route);
        Assert.Single(result.Events.OfType<EnergyShipped>());

        var observation = engine.BuildObservation("A");
        Assert.Equal(40, Assert.Single(observation.EnergyShipments).Amount);

        Step(engine, 2);
        Assert.Equal(40, world.Regions["r1"].GetEnergy("A"));
        Assert.Empty(world.InTransitEnergy);
        Assert.Equal(40, world.Statistics.EnergyDeliveredTotal);
    }

    [Fact]
    public void DrawRejectsInsufficientGridEnergy()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 100,
                Material: 0,
                Power: new PowerModule(EnergyPerTick: 0, StorageCapacity: 100)))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 10),
        });

        Assert.Equal(SimulationErrorCode.NoEnergy, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Equal(0, world.FindEntity(new EntityId(1))!.Energy);
    }

    [Fact]
    public void EnergyOperationsRequireAPowerModule()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendEnergyIntent(new EntityId(1), 40),
            new DrawEnergyIntent(new EntityId(1), 10),
        });

        Assert.All(result.IntentResults, item =>
            Assert.Equal(SimulationErrorCode.NoPower, item.ErrorCode));
    }

    [Fact]
    public void EnergyLinkCapacityRejectsTheSecondShipment()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 3, energyPerTick: 1)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 200,
                EnergyCapacity: 200,
                Material: 0,
                Power: new PowerModule(EnergyPerTick: 0, StorageCapacity: 200)))
            .Build();
        var entity = world.FindEntity(new EntityId(1))!;

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendEnergyIntent(new EntityId(1), 40, "r1"),
            new SendEnergyIntent(new EntityId(1), 40, "r1"),
        });

        Assert.True(result.IntentResults[0].Accepted);
        Assert.Equal(SimulationErrorCode.NoBandwidth, result.IntentResults[1].ErrorCode);
        Assert.Equal(160, entity.Energy);
        Assert.Single(world.InTransitEnergy);
    }

    [Fact]
    public void SavePreservesLocalGridAndEnergyShipment()
    {
        var world = NewWorld(withLink: true);
        var engine = new SimulationEngine(world);

        engine.Step(new Intent[] { new SendEnergyIntent(new EntityId(1), 40) });
        engine.Step(new Intent[] { new SendEnergyIntent(new EntityId(1), 30, "r1") });

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var shipment = Assert.Single(loaded.InTransitEnergy);

        Assert.Equal(40, loaded.Regions["r0"].GetEnergy("A"));
        Assert.Equal("l01", shipment.Route);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));

        var loadedEngine = new SimulationEngine(loaded);
        Step(loadedEngine, 2);
        Assert.Equal(30, loaded.Regions["r1"].GetEnergy("A"));
        Assert.Empty(loaded.InTransitEnergy);
    }

    private static void Step(SimulationEngine engine, int count)
    {
        for (var i = 0; i < count; i++)
        {
            engine.Step(Array.Empty<Intent>());
        }
    }

    private static WorldState NewWorld(bool withLink, int energyPerTick = 0)
    {
        var builder = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Power: new PowerModule(EnergyPerTick: 0, StorageCapacity: 100)));

        if (withLink)
        {
            builder.AddLink("l01", "r0", "r1", latencyTicks: 3, energyPerTick: energyPerTick);
        }

        return builder.Build();
    }
}
