using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class PowerGridTests
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
    public void DiagnosticsListsConnectedRegionsWithoutNonPowerLinks()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddRegion("r2", 10_000, 10_000)
            .AddRegion("r3", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: 100)
            .AddLink("l12", "r1", "r2", latencyTicks: 1, powerPerTick: 100)
            .AddLink("l23", "r2", "r3", latencyTicks: 1, powerPerTick: 0)
            .AddEntity(PowerEntity(1, "A", "r0", generation: 3, energy: 10))
            .Build();

        var diagnostics = PowerGridDiagnosticsBuilder.Build(world, "A");
        var r0 = diagnostics.Regions.Single(region => region.RegionId == "r0");
        var r3 = diagnostics.Regions.Single(region => region.RegionId == "r3");

        Assert.Equal(new[] { "r1", "r2" }, r0.ConnectedRegions);
        Assert.Equal(3, r0.ComponentRegionCount);
        Assert.Empty(r3.ConnectedRegions);
        Assert.Equal(1, r3.ComponentRegionCount);
        Assert.Equal(3, diagnostics.TotalGenerationPerTick);
    }

    [Fact]
    public void DiagnosticsDoNotCountAnotherCivilizationsDevices()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(PowerEntity(1, "A", "r0", generation: 2, energy: 10))
            .AddEntity(PowerEntity(2, "B", "r0", generation: 5, energy: 50))
            .Build();

        var diagnostics = PowerGridDiagnosticsBuilder.Build(world, "A");

        Assert.Equal(2, diagnostics.TotalGenerationPerTick);
        Assert.Equal(10, diagnostics.TotalLocalEnergy);
        Assert.Equal(100, diagnostics.TotalCapacity);
    }

    [Fact]
    public void DrawFailureEmitsGridPowerShortfall()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(PowerEntity(1, "A", "r0", generation: 0, energy: 0))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 10),
        });

        Assert.Equal(SimulationErrorCode.NoEnergy, Assert.Single(result.IntentResults).ErrorCode);
        var shortfall = Assert.Single(result.Events.OfType<PowerShortfall>());
        Assert.Equal("grid", shortfall.Source);
        Assert.Equal(10, shortfall.Requested);
        Assert.Equal(0, shortfall.Available);
    }

    [Fact]
    public void MoveFailureEmitsLocalPowerShortfall()
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
                Drive: new DriveModule(100, EnergyCostPerMove: 5)))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });

        var shortfall = Assert.Single(result.Events.OfType<PowerShortfall>());
        Assert.Equal("local", shortfall.Source);
        Assert.Equal(5, shortfall.Requested);
        Assert.Equal(0, shortfall.Available);
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
