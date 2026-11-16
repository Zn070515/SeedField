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

        var point = world.ControlPoint!;
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

        var point = world.ControlPoint!;
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
        Assert.Equal(1, world.ControlPoint!.HoldTicks);

        engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(5_000, 5_000)),
        });

        Assert.Null(world.ControlPoint!.HoldingCivilizationId);
        Assert.Equal(0, world.ControlPoint.HoldTicks);
    }

    [Fact]
    public void DeviceObservationExposesTheControlPointInItsRegion()
    {
        var world = NewWorld(holdTicks: 3)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), drive: false))
            .Build();
        var engine = new SimulationEngine(world);

        var observation = engine.BuildDeviceObservation(new EntityId(1));

        Assert.NotNull(observation.ControlPoint);
        Assert.Equal("relay", observation.ControlPoint.Id);
        Assert.Equal(1_000, observation.ControlPoint.RadiusMm);
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
        Assert.Equal(2, loaded.ControlPoint!.HoldTicks);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));

        var loadedEngine = new SimulationEngine(loaded);
        Step(loadedEngine, 1);

        Assert.Equal("A", loaded.ControlPoint!.OwnerCivilizationId);
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
