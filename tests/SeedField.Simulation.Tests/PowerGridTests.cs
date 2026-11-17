using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed partial class PowerGridTests
{
    [Fact]
    public void FlowBalancesConnectedRegionsAcrossMultipleHops()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddRegion("r2", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: 100)
            .AddLink("l12", "r1", "r2", latencyTicks: 1, powerPerTick: 100)
            .Build();
        world.Regions["r0"].AddEnergy("A", 90);

        var result = new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(30, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(30, world.Regions["r1"].GetEnergy("A"));
        Assert.Equal(30, world.Regions["r2"].GetEnergy("A"));
        Assert.Equal(60, world.Statistics.EnergyFlowedTotal);
        Assert.Equal(2, result.Events.OfType<PowerFlowed>().Count());
    }

    [Fact]
    public void FlowRespectsLinkCapacityPerTick()
    {
        var world = TwoRegionWorld(powerPerTick: 40);
        world.Regions["r0"].AddEnergy("A", 90);
        var engine = new SimulationEngine(world);

        engine.Step(Array.Empty<Intent>());

        Assert.Equal(50, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(40, world.Regions["r1"].GetEnergy("A"));
        Assert.Equal(40, world.Statistics.EnergyFlowedTotal);

        engine.Step(Array.Empty<Intent>());

        Assert.Equal(45, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(45, world.Regions["r1"].GetEnergy("A"));
        Assert.Equal(45, world.Statistics.EnergyFlowedTotal);
    }

    [Fact]
    public void ZeroPowerLinkDoesNotFlow()
    {
        var world = TwoRegionWorld(powerPerTick: 0);
        world.Regions["r0"].AddEnergy("A", 90);

        var result = new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(90, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(0, world.Regions["r1"].GetEnergy("A"));
        Assert.Equal(0, world.Statistics.EnergyFlowedTotal);
        Assert.Empty(result.Events.OfType<PowerFlowed>());
    }

    [Fact]
    public void FlowIsPerCivilization()
    {
        var world = TwoRegionWorld(powerPerTick: 100);
        world.Regions["r0"].AddEnergy("A", 90);
        world.Regions["r0"].AddEnergy("B", 30);

        new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(45, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(45, world.Regions["r1"].GetEnergy("A"));
        Assert.Equal(15, world.Regions["r0"].GetEnergy("B"));
        Assert.Equal(15, world.Regions["r1"].GetEnergy("B"));
        Assert.Equal(60, world.Statistics.EnergyFlowedTotal);
    }

    [Fact]
    public void DisconnectedRegionsDoNotFlow()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddRegion("r2", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: 100)
            .Build();
        world.Regions["r0"].AddEnergy("A", 90);

        new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(45, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(45, world.Regions["r1"].GetEnergy("A"));
        Assert.Equal(0, world.Regions["r2"].GetEnergy("A"));
        Assert.Equal(45, world.Statistics.EnergyFlowedTotal);
    }

    [Fact]
    public void FlowConservesEnergyAndIsDeterministic()
    {
        var first = ThreeRegionChain();
        var second = ThreeRegionChain();
        first.Regions["r0"].AddEnergy("A", 90);
        second.Regions["r0"].AddEnergy("A", 90);

        var firstEngine = new SimulationEngine(first);
        var secondEngine = new SimulationEngine(second);
        for (var tick = 0; tick < 3; tick++)
        {
            firstEngine.Step(Array.Empty<Intent>());
            secondEngine.Step(Array.Empty<Intent>());
        }

        Assert.Equal(90, TotalEnergy(first));
        Assert.Equal(90, TotalEnergy(second));
        Assert.Equal(WorldStateHasher.Compute(first), WorldStateHasher.Compute(second));
    }

    [Fact]
    public void PowerPerTickSurvivesSaveLoad()
    {
        var world = ThreeRegionChain();
        world.Regions["r0"].AddEnergy("A", 90);
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.All(loaded.Links.Values, link => Assert.Equal(100, link.PowerPerTick));
        Assert.Equal(30, loaded.Regions["r2"].GetEnergy("A"));
    }

    [Fact]
    public void V8LinkMigratesToZeroPowerPerTick()
    {
        var world = TwoRegionWorld(powerPerTick: 100);
        var data = WorldSaveSerializer.ToData(world);
        var v8Data = data with
        {
            SchemaVersion = 8,
            Links = new[] { data.Links![0] with { PowerPerTick = 0 } },
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(v8Data));

        Assert.Equal(0, Assert.Single(loaded.Links.Values).PowerPerTick);
    }

    [Fact]
    public void GenerationOverflowsIntoRegionGridWhenLocalIsFull()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 9,
                EnergyCapacity: 10,
                Material: 0,
                Power: new PowerModule(2, 10)))
            .Build();

        new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(10, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(1, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(2, world.Statistics.EnergyGeneratedTotal);
        Assert.Equal(1, world.Statistics.EnergyOverflowedTotal);
    }

    [Fact]
    public void InactiveRegionCatchUpOverflowsIntoGrid()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 9,
                EnergyCapacity: 10,
                Material: 0,
                Power: new PowerModule(2, 10)))
            .Build();
        var engine = new SimulationEngine(world);
        engine.DeactivateRegion("r0");

        for (var tick = 0; tick < 3; tick++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        engine.ActivateRegion("r0");

        Assert.Equal(10, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(5, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(6, world.Statistics.EnergyGeneratedTotal);
        Assert.Equal(5, world.Statistics.EnergyOverflowedTotal);
    }

    [Fact]
    public void DiagnosticsMarksBlackoutWhenComponentHasNoGridEnergy()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: 100)
            .AddEntity(PowerEntity(1, "A", "r0", generation: 0, energy: 0))
            .Build();

        var diagnostics = PowerGridDiagnosticsBuilder.Build(world, "A");

        Assert.All(diagnostics.Regions, region => Assert.True(region.IsBlackedOut));
        Assert.Equal(0, diagnostics.TotalGridEnergy);
        Assert.Equal(2, diagnostics.Regions[0].ComponentRegionCount);
        Assert.Equal(new[] { "r1" }, diagnostics.Regions[0].ConnectedRegions);
    }

    [Fact]
    public void OverflowRespectsGridCapacityAndWastesExcess()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetGridCapacity("r0", "A", capacity: 2)
            .AddEntity(PowerNode(
                1,
                "r0",
                energy: 9,
                capacity: 10,
                generation: 5))
            .Build();

        new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(10, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(2, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(2, world.Statistics.EnergyOverflowedTotal);
        Assert.Equal(2, world.Statistics.EnergyWastedTotal);
    }

    [Fact]
    public void ExplicitStoreRejectsWhenGridIsFull()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetGridCapacity("r0", "A", capacity: 10)
            .AddEntity(PowerNode(1, "r0", energy: 20, capacity: 100, generation: 0))
            .Build();
        world.Regions["r0"].AddEnergy("A", 10);

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendEnergyIntent(new EntityId(1), 5),
        });

        Assert.Equal(SimulationErrorCode.GridFull, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Equal(20, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(10, world.Regions["r0"].GetEnergy("A"));
    }

    [Fact]
    public void FlowRespectsGridCapacity()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .SetGridCapacity("r1", "A", capacity: 10)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: 100)
            .Build();
        world.Regions["r0"].AddEnergy("A", 90);

        new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(80, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(10, world.Regions["r1"].GetEnergy("A"));
    }

    [Fact]
    public void PowerShipmentArrivalWastesWhenGridIsFull()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .SetGridCapacity("r1", "A", capacity: 5)
            .AddLink(
                "l01",
                "r0",
                "r1",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLatencyTicks: 2)
            .Build();
        world.Regions["r0"].AddEnergy("A", 90);
        var engine = new SimulationEngine(world);

        engine.Step(Array.Empty<Intent>());
        Assert.Equal(5, Assert.Single(world.InTransitPower).DeliveredAmount);

        world.Regions["r1"].AddEnergy("A", 5);
        var arrival = engine.Step(Array.Empty<Intent>());

        Assert.Empty(world.InTransitPower);
        Assert.Equal(5, world.Regions["r1"].GetEnergy("A"));
        Assert.Equal(5, world.Statistics.EnergyWastedTotal);
        Assert.Contains(arrival.Events, item =>
            item is EnergyWasted { Source: "arrival", Amount: 5 });
    }

    [Fact]
    public void PriorityLoadSheddingProtectsHighPriorityDraws()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy(
                "A",
                reserveEnergy: 10,
                highPriorityThreshold: 5,
                priorityReserveEnergy: 5)
            .AddEntity(PowerNode(
                1,
                "r0",
                energy: 0,
                capacity: 100,
                generation: 0,
                driveCost: 3,
                priority: 10))
            .AddEntity(PowerNode(
                2,
                "r0",
                energy: 0,
                capacity: 100,
                generation: 0,
                driveCost: 3,
                priority: 0))
            .Build();
        world.Regions["r0"].AddEnergy("A", 13);

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
            new MoveIntent(new EntityId(2), new Vec2(500, 500)),
        });

        Assert.True(result.IntentResults.Single(item =>
            item.Intent.EntityId == new EntityId(1)).Accepted);
        var low = result.IntentResults.Single(item =>
            item.Intent.EntityId == new EntityId(2));
        Assert.Equal(SimulationErrorCode.NoEnergy, low.ErrorCode);
        Assert.Equal(10, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(3, world.Statistics.EnergyAutoDrawnTotal);
        Assert.Contains(result.Events, item =>
            item is PowerShortfall { Source: "priority" });
    }

    private static EntityDefinition PowerEntity(
        long id,
        string civilizationId,
        string regionId,
        int generation,
        long energy) =>
        new(
            new EntityId(id),
            civilizationId,
            regionId,
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: energy,
            EnergyCapacity: 100,
            Material: 0,
            Power: new PowerModule(generation, 100));

    private static EntityDefinition PowerNode(
        long id,
        string regionId,
        long energy,
        long capacity,
        int generation,
        long driveCost = 0,
        int priority = 0) =>
        new(
            new EntityId(id),
            "A",
            regionId,
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: energy,
            EnergyCapacity: capacity,
            Material: 0,
            Drive: driveCost > 0 ? new DriveModule(100, driveCost) : null,
            Power: new PowerModule(generation, capacity, priority));

    private static WorldState ThreeRegionChain()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddRegion("r2", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: 100)
            .AddLink("l12", "r1", "r2", latencyTicks: 1, powerPerTick: 100)
            .Build();
        return world;
    }

    private static long TotalEnergy(WorldState world) =>
        world.Regions.Values.Sum(region => region.GetEnergy("A"));

    private static WorldState TwoRegionWorld(int powerPerTick)
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: powerPerTick)
            .Build();
        return world;
    }
}
