using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class RegionActivationTests
{
    [Fact]
    public void IntentInInactiveRegionIsRejected()
    {
        var world = NewWorld();
        var engine = new SimulationEngine(world);
        engine.DeactivateRegion("r0");

        var result = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });

        Assert.Equal(SimulationErrorCode.RegionInactive, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Equal(new Vec2(100, 100), world.FindEntity(new EntityId(1))!.Position);
    }

    [Fact]
    public void ReactivatedRegionAcceptsIntentsAgain()
    {
        var world = NewWorld();
        var engine = new SimulationEngine(world);
        engine.DeactivateRegion("r0");
        engine.Step(new Intent[] { new MoveIntent(new EntityId(1), new Vec2(500, 500)) });

        engine.ActivateRegion("r0");
        var result = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        Assert.Equal(new Vec2(500, 500), world.FindEntity(new EntityId(1))!.Position);
    }

    [Fact]
    public void InactivePowerIsDeferredAndCaughtUpOnActivation()
    {
        var activeWorld = NewWorld();
        var inactiveWorld = NewWorld();
        var activeEngine = new SimulationEngine(activeWorld);
        var inactiveEngine = new SimulationEngine(inactiveWorld);
        inactiveEngine.DeactivateRegion("r0");

        for (var tick = 0; tick < 60; tick++)
        {
            activeEngine.Step(Array.Empty<Intent>());
            inactiveEngine.Step(Array.Empty<Intent>());
        }

        Assert.Equal(100, activeWorld.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(50, inactiveWorld.FindEntity(new EntityId(1))!.Energy);
        Assert.NotEqual(
            WorldStateHasher.Compute(activeWorld),
            WorldStateHasher.Compute(inactiveWorld));

        inactiveEngine.ActivateRegion("r0");

        Assert.Equal(100, inactiveWorld.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(
            WorldStateHasher.Compute(activeWorld),
            WorldStateHasher.Compute(inactiveWorld));
    }

    [Fact]
    public void UnknownRegionCannotBeDeactivated()
    {
        var engine = new SimulationEngine(NewWorld());

        Assert.Throws<ArgumentException>(() => engine.DeactivateRegion("missing"));
    }

    private static WorldState NewWorld() =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 50,
                EnergyCapacity: 100,
                Material: 0,
                Drive: new DriveModule(1_000, EnergyCostPerMove: 1),
                Power: new PowerModule(EnergyPerTick: 1, StorageCapacity: 100)))
            .Build();
}
