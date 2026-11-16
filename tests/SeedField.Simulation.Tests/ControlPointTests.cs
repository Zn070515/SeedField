using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class ControlPointTests
{
    [Fact]
    public void SoleOccupantCapturesAfterTheHoldDuration()
    {
        var world = NewWorld(holdTicks: 3)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), drive: false))
            .Build();
        var engine = new SimulationEngine(world);

        Step(engine, 3);

        var point = world.ControlPoints["relay"];
        Assert.Equal("A", point.OwnerCivilizationId);
        Assert.Equal(3, point.HoldTicks);
        Assert.True(point.IsCaptured);
    }

    [Fact]
    public void ContestResetsProgress()
    {
        var world = NewWorld(holdTicks: 3)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), drive: false))
            .AddEntity(Unit(2, "B", new Vec2(100, 100), drive: false))
            .Build();
        var engine = new SimulationEngine(world);

        Step(engine, 4);

        var point = world.ControlPoints["relay"];
        Assert.Null(point.HoldingCivilizationId);
        Assert.Equal(0, point.HoldTicks);
        Assert.Null(point.OwnerCivilizationId);
    }

    [Fact]
    public void LeavingTheRadiusResetsProgress()
    {
        var world = NewWorld(holdTicks: 3)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), drive: true))
            .Build();
        var engine = new SimulationEngine(world);

        engine.Step(Array.Empty<Intent>());
        Assert.Equal(1, world.ControlPoints["relay"].HoldTicks);

        engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(5_000, 5_000)),
        });

        Assert.Null(world.ControlPoints["relay"].HoldingCivilizationId);
        Assert.Equal(0, world.ControlPoints["relay"].HoldTicks);
    }

    [Fact]
    public void DeviceObservationExposesTheControlPointInItsRegion()
    {
        var world = NewWorld(holdTicks: 3)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), drive: false))
            .Build();
        var engine = new SimulationEngine(world);

        var observation = engine.BuildDeviceObservation(new EntityId(1));

        var observed = Assert.Single(observation.ControlPoints!);
        Assert.Equal("relay", observed.Id);
        Assert.Equal(1_000, observed.RadiusMm);
    }

    [Fact]
    public void SavePreservesProgressAndOwner()
    {
        var world = NewWorld(holdTicks: 3)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), drive: false))
            .Build();
        var engine = new SimulationEngine(world);

        Step(engine, 2);
        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        Assert.Equal(2, loaded.ControlPoints["relay"].HoldTicks);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));

        var loadedEngine = new SimulationEngine(loaded);
        Step(loadedEngine, 1);

        Assert.Equal("A", loaded.ControlPoints["relay"].OwnerCivilizationId);
    }

    [Fact]
    public void CapturingEnoughPointsWinsTheMatch()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddControlPoint("p1", "r0", new Vec2(100, 100), radiusMm: 1_000, holdTicksRequired: 2)
            .AddControlPoint("p2", "r0", new Vec2(5_000, 5_000), radiusMm: 1_000, holdTicksRequired: 2)
            .SetVictoryCondition(capturedPointsToWin: 2)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), drive: false))
            .AddEntity(Unit(2, "A", new Vec2(5_000, 5_000), drive: false))
            .Build();
        var engine = new SimulationEngine(world);

        Step(engine, 2);

        Assert.Equal("A", world.Match!.WinnerCivilizationId);
        Assert.True(world.Match.IsFinished);
        Assert.Equal("A", world.ControlPoints["p1"].OwnerCivilizationId);
        Assert.Equal("A", world.ControlPoints["p2"].OwnerCivilizationId);
    }

    [Fact]
    public void V2SingleControlPointIsMigratedToThePointCollection()
    {
        var world = NewWorld(holdTicks: 2)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), drive: false))
            .Build();
        var v2 = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 2,
            ControlPoints = null,
            ControlPoint = new ControlPointSaveData(
                "relay",
                "r0",
                100,
                100,
                1_000,
                2,
                null,
                0,
                null),
            Match = null,
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(v2));

        Assert.True(loaded.ControlPoints.ContainsKey("relay"));
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    private static void Step(SimulationEngine engine, int count)
    {
        for (var i = 0; i < count; i++)
        {
            engine.Step(Array.Empty<Intent>());
        }
    }

    private static WorldBuilder NewWorld(int holdTicks) =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddControlPoint("relay", "r0", new Vec2(100, 100), radiusMm: 1_000, holdTicks);

    private static EntityDefinition Unit(long id, string civilizationId, Vec2 position, bool drive) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            position,
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Drive: drive ? new DriveModule(10_000, EnergyCostPerMove: 1) : null);
}
