using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed partial class PowerGridTests
{
    [Fact]
    public void BreakerTripsAndAutoReconnectsAtReserve()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 10)
            .AddEntity(PowerNode(1, "r0", energy: 0, capacity: 100, generation: 0))
            .Build();
        var engine = new SimulationEngine(world);

        var trip = engine.Step(Array.Empty<Intent>());
        Assert.Contains(trip.Events, item => item is PowerBlackout);
        Assert.True(world.PowerGridStates[("r0", "A")].BreakerOpen);

        var blocked = engine.Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 5),
        });
        Assert.Equal(SimulationErrorCode.NoEnergy, Assert.Single(blocked.IntentResults).ErrorCode);
        Assert.Contains(blocked.Events, item =>
            item is PowerShortfall { Source: "blackout" });

        world.Regions["r0"].AddEnergy("A", 5);
        engine.Step(Array.Empty<Intent>());
        Assert.True(world.PowerGridStates[("r0", "A")].BreakerOpen);

        world.Regions["r0"].AddEnergy("A", 10);
        var restored = engine.Step(Array.Empty<Intent>());
        Assert.Contains(restored.Events, item => item is PowerRestored);
        Assert.False(world.PowerGridStates[("r0", "A")].BreakerOpen);

        var draw = engine.Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 5),
        });
        Assert.True(Assert.Single(draw.IntentResults).Accepted);
        Assert.Equal(10, world.Regions["r0"].GetEnergy("A"));
    }

    [Fact]
    public void ManualBreakerOpenHoldsUntilManualClose()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(PowerNode(1, "r0", energy: 0, capacity: 100, generation: 0))
            .Build();
        world.Regions["r0"].AddEnergy("A", 20);
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());

        var opened = engine.ExecutePowerCommand(
            new OpenBreakerCommand("A", "r0"));
        Assert.True(opened.Success);

        engine.Step(Array.Empty<Intent>());
        var state = world.PowerGridStates[("r0", "A")];
        Assert.True(state.BreakerOpen);
        Assert.True(state.ManualHold);

        var closed = engine.ExecutePowerCommand(
            new CloseBreakerCommand("A", "r0"));
        Assert.True(closed.Success);
        Assert.False(state.BreakerOpen);
        Assert.False(state.ManualHold);
    }

    [Fact]
    public void BreakerAndCapacitySurviveSaveLoad()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetGridCapacity("r0", "A", capacity: 50)
            .SetPowerPolicy(
                "A",
                reserveEnergy: 10,
                highPriorityThreshold: 5,
                priorityReserveEnergy: 5,
                autoReconnect: false)
            .AddEntity(PowerNode(1, "r0", energy: 0, capacity: 100, generation: 0))
            .Build();
        new SimulationEngine(world).Step(Array.Empty<Intent>());

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(50, loaded.Regions["r0"].GetGridCapacity("A"));
        Assert.True(loaded.PowerGridStates[("r0", "A")].BreakerOpen);
        Assert.False(loaded.PowerPolicies["A"].AutoReconnect);
        Assert.Equal(5, loaded.PowerPolicies["A"].HighPriorityThreshold);
    }

    [Fact]
    public void LossCompensationDeliversTheNetDeficitInOneShipment()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddRegion("r2", 10_000, 10_000)
            .AddLink(
                "l01",
                "r0",
                "r1",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLossPercent: 10)
            .AddLink(
                "l12",
                "r1",
                "r2",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLossPercent: 10)
            .Build();
        world.Regions["r0"].AddEnergy("A", 100);

        new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(34, world.Regions["r1"].GetEnergy("A"));
        Assert.Equal(57, world.Statistics.EnergyFlowedTotal);
        Assert.Equal(10, world.Statistics.EnergyFlowLossTotal);
    }

    [Fact]
    public void PowerLatencyDelaysArrival()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
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

        var first = engine.Step(Array.Empty<Intent>());

        Assert.Equal(45, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(0, world.Regions["r1"].GetEnergy("A"));
        var shipment = Assert.Single(world.InTransitPower);
        Assert.Equal(45, shipment.Amount);
        Assert.Equal(45, shipment.DeliveredAmount);
        Assert.Equal(2, shipment.ArriveTick);
        Assert.Equal(0, world.Statistics.EnergyFlowedTotal);
        Assert.Single(first.Events.OfType<PowerShipped>());

        var second = engine.Step(Array.Empty<Intent>());

        Assert.Equal(45, world.Regions["r1"].GetEnergy("A"));
        Assert.Empty(world.InTransitPower);
        Assert.Equal(45, world.Statistics.EnergyFlowedTotal);
        Assert.Single(second.Events.OfType<PowerArrived>());
    }

    [Fact]
    public void PowerLossReducesDeliveredAmount()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink(
                "l01",
                "r0",
                "r1",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLossPercent: 10)
            .Build();
        world.Regions["r0"].AddEnergy("A", 90);

        var result = new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.Equal(45, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(40, world.Regions["r1"].GetEnergy("A"));
        Assert.Equal(40, world.Statistics.EnergyFlowedTotal);
        Assert.Equal(5, world.Statistics.EnergyFlowLossTotal);
        var flowed = Assert.Single(result.Events.OfType<PowerFlowed>());
        Assert.Equal(40, flowed.Amount);
    }

    [Fact]
    public void InTransitPowerSurvivesSaveLoad()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
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

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Single(loaded.InTransitPower);

        new SimulationEngine(loaded).Step(Array.Empty<Intent>());
        Assert.Equal(45, loaded.Regions["r1"].GetEnergy("A"));
    }

    [Fact]
    public void V9LinkMigratesToZeroPowerLatencyAndLoss()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink(
                "l01",
                "r0",
                "r1",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLatencyTicks: 2,
                powerLossPercent: 10)
            .Build();
        var data = WorldSaveSerializer.ToData(world);
        var v9Data = data with
        {
            SchemaVersion = 9,
            PowerShipments = null,
            NextPowerShipmentId = 0,
            Links = new[]
            {
                data.Links![0] with
                {
                    PowerLatencyTicks = 0,
                    PowerLossPercent = 0,
                },
            },
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(v9Data));
        var link = Assert.Single(loaded.Links.Values);

        Assert.Equal(0, link.PowerLatencyTicks);
        Assert.Equal(0, link.PowerLossPercent);
        Assert.Empty(loaded.InTransitPower);
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
    public void ActionAutoDrawsFromRegionalGrid()
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
                Drive: new DriveModule(100, EnergyCostPerMove: 5),
                Power: new PowerModule(0, 100)))
            .Build();
        world.Regions["r0"].AddEnergy("A", 20);

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        Assert.Equal(0, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(15, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(5, world.Statistics.EnergyAutoDrawnTotal);
        Assert.Single(result.Events.OfType<EnergyAutoDrawn>());
    }

    [Fact]
    public void AutoDrawStopsAtTheRegionalReserve()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 10)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 100,
                Material: 0,
                Drive: new DriveModule(100, EnergyCostPerMove: 5),
                Power: new PowerModule(0, 100)))
            .Build();
        world.Regions["r0"].AddEnergy("A", 12);

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });

        Assert.Equal(SimulationErrorCode.NoEnergy, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Equal(0, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(12, world.Regions["r0"].GetEnergy("A"));
        var shortfall = Assert.Single(result.Events.OfType<PowerShortfall>());
        Assert.Equal("reserve", shortfall.Source);
        Assert.Equal(2, shortfall.Available);
    }

    [Fact]
    public void ExplicitDrawRespectsTheRegionalReserve()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 10)
            .AddEntity(PowerEntity(1, "A", "r0", generation: 0, energy: 0))
            .Build();
        world.Regions["r0"].AddEnergy("A", 12);
        var engine = new SimulationEngine(world);

        var rejected = engine.Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 5),
        });
        Assert.Equal(SimulationErrorCode.NoEnergy, Assert.Single(rejected.IntentResults).ErrorCode);
        Assert.Equal(12, world.Regions["r0"].GetEnergy("A"));

        var accepted = engine.Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 2),
        });
        Assert.True(Assert.Single(accepted.IntentResults).Accepted);
        Assert.Equal(10, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(2, world.FindEntity(new EntityId(1))!.Energy);
    }

    [Fact]
    public void PowerPolicySurvivesSaveLoad()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 10)
            .Build();

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(10, loaded.PowerPolicies["A"].ReserveEnergy);
    }

    [Fact]
    public void V9SaveMigratesToZeroReserve()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 10)
            .Build();
        var v9Data = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 9,
            PowerPolicies = null,
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(v9Data));

        Assert.Empty(loaded.PowerPolicies);
        Assert.Equal(0, PowerGridDiagnosticsBuilder.Build(loaded, "A").ReserveEnergy);
    }

    [Fact]
    public void DiagnosticsReportsLoadShedBelowReserve()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 10)
            .AddEntity(PowerEntity(1, "A", "r0", generation: 0, energy: 0))
            .Build();
        world.Regions["r0"].AddEnergy("A", 5);

        var diagnostics = PowerGridDiagnosticsBuilder.Build(world, "A");
        var region = Assert.Single(diagnostics.Regions);

        Assert.Equal(10, diagnostics.ReserveEnergy);
        Assert.Equal(10, region.ReserveEnergy);
        Assert.True(region.IsLoadShed);
        Assert.False(region.IsBlackedOut);
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
        Assert.Equal("blackout", shortfall.Source);
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

}
