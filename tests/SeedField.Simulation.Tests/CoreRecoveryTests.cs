using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class CoreRecoveryTests
{
    private static readonly SeedCoreConfig CoreConfig =
        new(EnergyPerTick: 1, MaterialPerTick: 1, MaxEnergyReserve: 100, MaxMaterialReserve: 100);

    private static readonly RecoveryBlueprint MinimumEntity = new(
        "minimum-entity",
        RequiredEnergy: 40,
        RequiredMaterial: 30,
        MaxHealth: 20,
        Energy: 20,
        EnergyCapacity: 20,
        Material: 0,
        Sensor: new SensorModule(1_000),
        Drive: new DriveModule(50, EnergyCostPerMove: 1),
        Comm: new CommModule(1, EnergyCostPerMessage: 1));

    [Fact]
    public void CoreRegeneratesUpToItsCapAndNeverBeyond()
    {
        var world = CreateCoreWorld();
        var engine = new SimulationEngine(world);

        for (var i = 0; i < 250; i++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var core = world.Cores["A"];
        Assert.Equal(100, core.EnergyReserve);
        Assert.Equal(100, core.MaterialReserve);
        Assert.Equal(100, world.Statistics.EnergyRegeneratedTotal);
        Assert.Equal(100, world.Statistics.MaterialRegeneratedTotal);
    }

    [Fact]
    public void AfterTotalExternalLossCoreCanBootstrapAMinimumEntity()
    {
        var world = CreateCoreWorld();
        var engine = new SimulationEngine(world);

        // No external entity or industry exists at all: the worst-case wipe.
        for (var i = 0; i < 100; i++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var result = engine.TryBootstrapFromCore("A", MinimumEntity, new Vec2(500, 500));

        Assert.True(result.Success);
        Assert.NotNull(result.EntityId);
        Assert.IsType<EntityBootstrapped>(result.Event);

        var entity = world.FindEntity(result.EntityId!.Value);
        Assert.NotNull(entity);
        Assert.Equal("A", entity.CivilizationId);
        Assert.Equal("core", entity.RegionId);
        Assert.Equal(60, world.Cores["A"].EnergyReserve);
        Assert.Equal(70, world.Cores["A"].MaterialReserve);
        Assert.Equal(40, world.Statistics.EnergySpentTotal);
        Assert.Equal(30, world.Statistics.MaterialSpentTotal);
    }

    [Fact]
    public void BootstrapIsRejectedWhenTheCoreQuotaIsInsufficient()
    {
        var world = CreateCoreWorld();
        var engine = new SimulationEngine(world);

        var result = engine.TryBootstrapFromCore("A", MinimumEntity, new Vec2(500, 500));

        Assert.False(result.Success);
        Assert.Equal(SimulationErrorCode.NoEnergy, result.ErrorCode);
        Assert.Empty(world.EnumerateEntities());
    }

    [Fact]
    public void BootstrapOutsideTheCoreRegionIsRejected()
    {
        var world = CreateCoreWorld();
        var engine = new SimulationEngine(world);

        for (var i = 0; i < 100; i++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var result = engine.TryBootstrapFromCore("A", MinimumEntity, new Vec2(50_000, 50_000));

        Assert.False(result.Success);
        Assert.Equal(SimulationErrorCode.OutOfBounds, result.ErrorCode);
        Assert.Empty(world.EnumerateEntities());
    }

    private static WorldState CreateCoreWorld() =>
        new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("wilds", 50_000, 50_000)
            .AddCore("A", "core", CoreConfig)
            .Build();
}
