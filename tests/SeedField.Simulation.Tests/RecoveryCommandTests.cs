using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class RecoveryCommandTests
{
    [Fact]
    public void BootstrapCommandUsesTheCoreAndReturnsAnEvent()
    {
        var world = NewCoreWorld();
        var engine = new SimulationEngine(world);
        Step(engine, 100);
        var blueprint = new RecoveryBlueprint("minimum", 10, 10, 10, 0, 10, 0);

        var result = engine.ExecuteRecoveryCommand(new BootstrapCoreCommand(
            "A",
            blueprint,
            new Vec2(500, 500)));

        Assert.True(result.Success);
        Assert.NotNull(result.EntityId);
        Assert.IsType<EntityBootstrapped>(result.Event);
        Assert.Equal(90, world.Cores["A"].EnergyReserve);
        Assert.NotNull(world.FindEntity(result.EntityId!.Value));
    }

    [Fact]
    public void RestoreLatestProductCommandUsesTheRegisteredVersion()
    {
        var world = NewCoreWorld();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "execution",
            0,
            "Execution",
            10,
            100,
            Drive: new DriveModule(50, 1)));
        world.Cores["A"].RecordLatestProduct(DeviceBranch.Execution, blueprint);
        var engine = new SimulationEngine(world);
        Step(engine, 100);

        var result = engine.ExecuteRecoveryCommand(new RestoreCoreProductCommand(
            "A",
            DeviceBranch.Execution,
            new Vec2(500, 500),
            10,
            10));

        Assert.True(result.Success);
        var entity = Assert.IsType<EntityBootstrapped>(result.Event);
        Assert.Equal("Execution", entity.BlueprintName);
    }

    [Fact]
    public void RecoveryCommandFailsWhenTheCoreReserveIsInsufficient()
    {
        var world = NewCoreWorld();
        var engine = new SimulationEngine(world);
        var blueprint = new RecoveryBlueprint("expensive", 1_000, 1_000, 10, 0, 10, 0);

        var result = engine.ExecuteRecoveryCommand(new BootstrapCoreCommand(
            "A",
            blueprint,
            new Vec2(500, 500)));

        Assert.False(result.Success);
        Assert.Equal(SimulationErrorCode.NoEnergy, result.ErrorCode);
        Assert.Empty(world.EnumerateEntities());
    }

    private static void Step(SimulationEngine engine, int count)
    {
        for (var i = 0; i < count; i++)
        {
            engine.Step(Array.Empty<Intent>());
        }
    }

    private static WorldState NewCoreWorld() =>
        new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("wilds", 10_000, 10_000)
            .AddCore("A", "core", new SeedCoreConfig(1, 1, 100, 100))
            .Build();
}
