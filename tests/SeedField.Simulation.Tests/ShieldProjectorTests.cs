using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// Shield projector support module: friendly shielded entities in range are
/// recharged in stable EntityId order, paying local energy per target.
/// </summary>
public sealed class ShieldProjectorTests
{
    private static (
        WorldState World,
        SimulationEngine Engine,
        EntityState Projector,
        EntityState Target) Build(
        ShieldProjectorModule projector,
        long projectorEnergy = 100,
        long targetShield = 10,
        int targetDistance = 500,
        int targetCapacity = 50)
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: projectorEnergy,
                EnergyCapacity: 100,
                Material: 0,
                ShieldProjector: projector))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "r0",
                new Vec2(100 + targetDistance, 100),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                Shield: new ShieldModule(targetCapacity, 0, 100_000, 0)))
            .Build();
        var target = world.FindEntity(new EntityId(2))!;
        target.ShieldEnergy = targetShield;
        return (
            world,
            new SimulationEngine(world),
            world.FindEntity(new EntityId(1))!,
            target);
    }

    [Fact]
    public void ProjectsToShieldedFriendlyInRange()
    {
        var (world, engine, projector, target) = Build(
            new ShieldProjectorModule(1000, 5, 1));

        var result = engine.Step();

        Assert.Equal(15, target.ShieldEnergy);
        Assert.Equal(99, projector.Energy);
        var projected = Assert.Single(result.Events.OfType<ShieldProjected>());
        Assert.Equal(new EntityId(1), projected.ProjectorId);
        Assert.Equal(new EntityId(2), projected.TargetId);
        Assert.Equal(5, projected.Amount);
        Assert.Equal(15, projected.TargetShield);
        Assert.Equal(5, world.Statistics.ShieldProjectedTotal);
    }

    [Fact]
    public void IgnoresUnshieldedFriendly()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                ShieldProjector: new ShieldProjectorModule(1000, 5, 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "r0",
                new Vec2(500, 100),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0))
            .Build();

        var result = new SimulationEngine(world).Step();

        Assert.Empty(result.Events.OfType<ShieldProjected>());
        Assert.Equal(100, world.FindEntity(new EntityId(1))!.Energy);
    }

    [Fact]
    public void IgnoresEnemy()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                ShieldProjector: new ShieldProjectorModule(1000, 5, 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(500, 100),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                Shield: new ShieldModule(50, 0, 100_000, 0)))
            .Build();
        var enemy = world.FindEntity(new EntityId(2))!;
        enemy.ShieldEnergy = 10;

        new SimulationEngine(world).Step();

        Assert.Equal(10, enemy.ShieldEnergy);
    }

    [Fact]
    public void IgnoresOutOfRange()
    {
        var (_, engine, projector, target) = Build(
            new ShieldProjectorModule(1000, 5, 1),
            targetDistance: 1500);

        var result = engine.Step();

        Assert.Equal(10, target.ShieldEnergy);
        Assert.Equal(100, projector.Energy);
        Assert.Empty(result.Events.OfType<ShieldProjected>());
    }

    [Fact]
    public void StopsAtCapacity()
    {
        var (_, engine, projector, target) = Build(
            new ShieldProjectorModule(1000, 5, 1),
            targetShield: 48);

        var result = engine.Step();

        Assert.Equal(50, target.ShieldEnergy);
        Assert.Equal(99, projector.Energy);
        var projected = Assert.Single(result.Events.OfType<ShieldProjected>());
        Assert.Equal(2, projected.Amount);
    }

    [Fact]
    public void ChargesEnergyPerTarget()
    {
        var world = TwoTargets(projectorEnergy: 100);
        var first = world.FindEntity(new EntityId(2))!;
        var second = world.FindEntity(new EntityId(3))!;

        var result = new SimulationEngine(world).Step();

        Assert.Equal(15, first.ShieldEnergy);
        Assert.Equal(15, second.ShieldEnergy);
        Assert.Equal(98, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(2, result.Events.OfType<ShieldProjected>().Count());
    }

    [Fact]
    public void StopsWhenEnergyRunsOut()
    {
        var world = TwoTargets(projectorEnergy: 1);
        var first = world.FindEntity(new EntityId(2))!;
        var second = world.FindEntity(new EntityId(3))!;

        var result = new SimulationEngine(world).Step();

        Assert.Equal(15, first.ShieldEnergy);
        Assert.Equal(10, second.ShieldEnergy);
        Assert.Equal(0, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Single(result.Events.OfType<ShieldProjected>());
    }

    [Fact]
    public void CriticalProjectorStops()
    {
        var (_, engine, projector, target) = Build(
            new ShieldProjectorModule(1000, 5, 1));
        projector.Health = 20;

        var result = engine.Step();

        Assert.Equal(10, target.ShieldEnergy);
        Assert.Empty(result.Events.OfType<ShieldProjected>());
    }

    [Fact]
    public void ObservationExposesShieldProjector()
    {
        var (_, engine, _, _) = Build(new ShieldProjectorModule(1000, 5, 1));

        var observed = engine.BuildObservation("A").Entities
            .Single(entity => entity.EntityId == 1);

        Assert.True(observed.HasShieldProjector);
        Assert.Equal(1000, observed.ShieldProjectorRangeMm);
        Assert.Equal(5, observed.ShieldProjectorRechargePerTick);
    }

    [Fact]
    public void SaveRoundTripPreservesShieldProjector()
    {
        var (world, _, _, _) = Build(new ShieldProjectorModule(1000, 5, 1));
        world.Workshop.Publish(new DeviceBlueprint(
            "shield_generator",
            0,
            "Shield Generator",
            MaxHealth: 100,
            EnergyCapacity: 100,
            ShieldProjector: new ShieldProjectorModule(1000, 5, 1)));

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var projector = loaded.FindEntity(new EntityId(1))!.ShieldProjector;

        Assert.NotNull(projector);
        Assert.Equal(1000, projector!.RangeMm);
        Assert.Equal(5, projector.RechargePerTick);
        Assert.Equal(1, projector.EnergyCostPerTarget);
        var blueprint = loaded.Workshop.GetVersion("shield_generator", 1);
        Assert.NotNull(blueprint);
        Assert.NotNull(blueprint!.ShieldProjector);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    [Fact]
    public void HashIncludesShieldProjector()
    {
        var (first, _, _, _) = Build(new ShieldProjectorModule(1000, 5, 1));
        var (second, _, _, _) = Build(new ShieldProjectorModule(1001, 5, 1));

        Assert.NotEqual(
            WorldStateHasher.Compute(first),
            WorldStateHasher.Compute(second));
    }

    [Fact]
    public void BlueprintCreatesShieldProjector()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "shield_generator",
            0,
            "Shield Generator",
            MaxHealth: 100,
            EnergyCapacity: 100,
            ShieldProjector: new ShieldProjectorModule(1000, 5, 1)));

        var entity = DeploymentService.CreateInstance(
            world,
            blueprint,
            "A",
            "r0",
            new Vec2(100, 100));

        Assert.NotNull(entity.ShieldProjector);
        Assert.Equal(1000, entity.ShieldProjector!.RangeMm);
    }

    private static WorldState TwoTargets(long projectorEnergy)
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: projectorEnergy,
                EnergyCapacity: 100,
                Material: 0,
                ShieldProjector: new ShieldProjectorModule(1000, 5, 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "r0",
                new Vec2(500, 100),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                Shield: new ShieldModule(50, 0, 100_000, 0)))
            .AddEntity(new EntityDefinition(
                new EntityId(3),
                "A",
                "r0",
                new Vec2(600, 100),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                Shield: new ShieldModule(50, 0, 100_000, 0)))
            .Build();
        world.FindEntity(new EntityId(2))!.ShieldEnergy = 10;
        world.FindEntity(new EntityId(3))!.ShieldEnergy = 10;
        return world;
    }
}
