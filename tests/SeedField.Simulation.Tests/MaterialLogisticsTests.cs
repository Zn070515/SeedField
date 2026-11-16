using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class MaterialLogisticsTests
{
    [Fact]
    public void MaterialArrivesOnlyAfterTheLinkLatency()
    {
        var world = NewWorld(withLink: true, sourceStock: 100);
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[]
        {
            new TransferMaterialIntent(new EntityId(1), "r1", 40),
        });

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        Assert.Equal(60, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(0, world.Regions["r1"].GetMaterial("A"));
        Assert.Equal(40, world.Statistics.MaterialShippedTotal);

        var shipment = Assert.Single(world.InTransitMaterial);
        Assert.Equal(3, shipment.ArriveTick);
        Assert.Equal("r1", shipment.ToRegionId);
        Assert.Equal("l01", shipment.Route);
        Assert.Single(result.Events.OfType<MaterialShipped>());

        var observation = engine.BuildObservation("A");
        var observed = Assert.Single(observation.Shipments);
        Assert.Equal(40, observed.Amount);
        Assert.Equal(3, observed.ArriveTick);

        Step(engine, 1);
        Assert.Equal(0, world.Regions["r1"].GetMaterial("A"));

        Step(engine, 1);
        Assert.Equal(40, world.Regions["r1"].GetMaterial("A"));
        Assert.Empty(world.InTransitMaterial);
        Assert.Equal(40, world.Statistics.MaterialDeliveredTotal);
    }

    [Fact]
    public void MultiHopMaterialUsesSummedLatencyAndFullRoute()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddRegion("r2", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 2)
            .AddLink("l12", "r1", "r2", latencyTicks: 3)
            .AddStock("r0", "A", 100)
            .AddEntity(Hauler())
            .Build();

        var engine = new SimulationEngine(world);
        engine.Step(new Intent[]
        {
            new TransferMaterialIntent(new EntityId(1), "r2", 40),
        });

        var shipment = Assert.Single(world.InTransitMaterial);
        Assert.Equal(5, shipment.ArriveTick);
        Assert.Equal("l01>l12", shipment.Route);
        Assert.Equal(60, world.Regions["r0"].GetMaterial("A"));

        Step(engine, 4);
        Assert.Equal(40, world.Regions["r2"].GetMaterial("A"));
    }

    [Fact]
    public void MaterialLinkCapacityRejectsTheSecondShipment()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 3, materialPerTick: 1)
            .AddStock("r0", "A", 100)
            .AddEntity(Hauler())
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new TransferMaterialIntent(new EntityId(1), "r1", 40),
            new TransferMaterialIntent(new EntityId(1), "r1", 40),
        });

        Assert.True(result.IntentResults[0].Accepted);
        Assert.Equal(SimulationErrorCode.NoBandwidth, result.IntentResults[1].ErrorCode);
        Assert.Equal(60, world.Regions["r0"].GetMaterial("A"));
        Assert.Single(world.InTransitMaterial);
    }

    [Fact]
    public void TransferWithoutALinkIsRejected()
    {
        var world = NewWorld(withLink: false, sourceStock: 100);
        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new TransferMaterialIntent(new EntityId(1), "r1", 40),
        });

        Assert.Equal(SimulationErrorCode.NoRoute, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Equal(100, world.Regions["r0"].GetMaterial("A"));
        Assert.Empty(world.InTransitMaterial);
    }

    [Fact]
    public void TransferWithoutStockIsRejected()
    {
        var world = NewWorld(withLink: true, sourceStock: 10);
        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new TransferMaterialIntent(new EntityId(1), "r1", 40),
        });

        Assert.Equal(
            SimulationErrorCode.InsufficientMaterial,
            Assert.Single(result.IntentResults).ErrorCode);
        Assert.Equal(10, world.Regions["r0"].GetMaterial("A"));
        Assert.Empty(world.InTransitMaterial);
    }

    [Fact]
    public void TransferRequiresAHaulerDrive()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 3)
            .AddStock("r0", "A", 100)
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
            new TransferMaterialIntent(new EntityId(1), "r1", 40),
        });

        Assert.Equal(SimulationErrorCode.NoDrive, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Equal(100, world.Regions["r0"].GetMaterial("A"));
    }

    [Fact]
    public void SavePreservesInTransitMaterial()
    {
        var world = NewWorld(withLink: true, sourceStock: 100);
        var engine = new SimulationEngine(world);
        engine.Step(new Intent[]
        {
            new TransferMaterialIntent(new EntityId(1), "r1", 40),
        });

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var shipment = Assert.Single(loaded.InTransitMaterial);
        Assert.Equal(3, shipment.ArriveTick);
        Assert.Equal("l01", shipment.Route);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));

        var loadedEngine = new SimulationEngine(loaded);
        Step(loadedEngine, 2);
        Assert.Equal(40, loaded.Regions["r1"].GetMaterial("A"));
        Assert.Empty(loaded.InTransitMaterial);
    }

    private static void Step(SimulationEngine engine, int count)
    {
        for (var i = 0; i < count; i++)
        {
            engine.Step(Array.Empty<Intent>());
        }
    }

    private static EntityDefinition Hauler() =>
        new(
            new EntityId(1),
            "A",
            "r0",
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Drive: new DriveModule(50, EnergyCostPerMove: 1));

    private static WorldState NewWorld(bool withLink, long sourceStock)
    {
        var builder = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddStock("r0", "A", sourceStock)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Drive: new DriveModule(50, EnergyCostPerMove: 1)));

        if (withLink)
        {
            builder.AddLink("l01", "r0", "r1", latencyTicks: 3);
        }

        return builder.Build();
    }
}
