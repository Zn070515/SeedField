using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// Repair Bay support module: the nearest damaged friendly entity in the same
/// region is repaired once per tick, paying local energy and regional material.
/// These are scenario values, not balance decisions.
/// </summary>
public sealed class RepairModuleTests
{
    private static (
        WorldState World,
        SimulationEngine Engine,
        EntityState Bay,
        EntityState Target) Build(
        RepairModule repair,
        long bayEnergy = 100,
        long material = 0,
        int targetHealth = 50,
        int targetDistance = 500)
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", material)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: bayEnergy,
                EnergyCapacity: 100,
                Material: 0,
                Repair: repair))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "r0",
                new Vec2(100 + targetDistance, 100),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0))
            .Build();

        world.FindEntity(new EntityId(2))!.Health = targetHealth;
        return (
            world,
            new SimulationEngine(world),
            world.FindEntity(new EntityId(1))!,
            world.FindEntity(new EntityId(2))!);
    }

    [Fact]
    public void RepairsNearestDamagedFriendlyInRange()
    {
        var (world, engine, bay, target) = Build(new RepairModule(1000, 5, 2));

        var result = engine.Step();

        Assert.Equal(55, target.Health);
        Assert.Equal(98, bay.Energy);
        var repaired = Assert.Single(result.Events.OfType<EntityRepaired>());
        Assert.Equal(new EntityId(1), repaired.RepairerId);
        Assert.Equal(new EntityId(2), repaired.TargetId);
        Assert.Equal(5, repaired.Amount);
        Assert.Equal(55, repaired.TargetHealth);
        Assert.Equal(5, world.Statistics.HealthRepairedTotal);
    }

    [Fact]
    public void DoesNotRepairOutOfRange()
    {
        var (_, engine, bay, target) = Build(
            new RepairModule(1000, 5, 2),
            targetDistance: 1500);

        var result = engine.Step();

        Assert.Equal(50, target.Health);
        Assert.Equal(100, bay.Energy);
        Assert.Empty(result.Events.OfType<EntityRepaired>());
    }

    [Fact]
    public void DoesNotRepairFullHealth()
    {
        var (_, engine, bay, target) = Build(
            new RepairModule(1000, 5, 2),
            targetHealth: 100);

        var result = engine.Step();

        Assert.Equal(100, target.Health);
        Assert.Equal(100, bay.Energy);
        Assert.Empty(result.Events.OfType<EntityRepaired>());
    }

    [Fact]
    public void DoesNotRepairEnemy()
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
                Repair: new RepairModule(1000, 5, 2)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(500, 100),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0))
            .Build();
        var enemy = world.FindEntity(new EntityId(2))!;
        enemy.Health = 50;

        new SimulationEngine(world).Step();

        Assert.Equal(50, enemy.Health);
    }

    [Fact]
    public void ConsumesRegionalMaterial()
    {
        var (world, engine, _, target) = Build(
            new RepairModule(1000, 5, 2, MaterialPerTick: 2),
            material: 10);

        var result = engine.Step();

        Assert.Equal(55, target.Health);
        Assert.Equal(8, world.Regions["r0"].GetMaterial("A"));
        var repaired = Assert.Single(result.Events.OfType<EntityRepaired>());
        Assert.Equal(2, repaired.MaterialSpent);
    }

    [Fact]
    public void SkipsWhenMaterialMissing()
    {
        var (world, engine, bay, target) = Build(
            new RepairModule(1000, 5, 2, MaterialPerTick: 2),
            material: 0);

        var result = engine.Step();

        Assert.Equal(50, target.Health);
        Assert.Equal(100, bay.Energy);
        Assert.Empty(result.Events.OfType<EntityRepaired>());
        Assert.Equal(0, world.Regions["r0"].GetMaterial("A"));
    }

    [Fact]
    public void SkipsWhenEnergyMissing()
    {
        var (_, engine, _, target) = Build(
            new RepairModule(1000, 5, 2),
            bayEnergy: 1);

        var result = engine.Step();

        Assert.Equal(50, target.Health);
        Assert.Empty(result.Events.OfType<EntityRepaired>());
    }

    [Fact]
    public void CriticalRepairerStops()
    {
        var (_, engine, bay, target) = Build(new RepairModule(1000, 5, 2));
        bay.Health = 20;

        var result = engine.Step();

        Assert.Equal(50, target.Health);
        Assert.Empty(result.Events.OfType<EntityRepaired>());
    }

    [Fact]
    public void TieBreaksByEntityId()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(5_000, 5_000),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Repair: new RepairModule(1000, 5, 2)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "r0",
                new Vec2(5_300, 5_000),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0))
            .AddEntity(new EntityDefinition(
                new EntityId(3),
                "A",
                "r0",
                new Vec2(4_700, 5_000),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0))
            .Build();
        world.FindEntity(new EntityId(2))!.Health = 50;
        world.FindEntity(new EntityId(3))!.Health = 50;

        new SimulationEngine(world).Step();

        Assert.Equal(55, world.FindEntity(new EntityId(2))!.Health);
        Assert.Equal(50, world.FindEntity(new EntityId(3))!.Health);
    }

    [Fact]
    public void CapsAtMaxHealth()
    {
        var (_, engine, bay, target) = Build(
            new RepairModule(1000, 5, 2),
            targetHealth: 98);

        var result = engine.Step();

        Assert.Equal(100, target.Health);
        Assert.Equal(98, bay.Energy);
        var repaired = Assert.Single(result.Events.OfType<EntityRepaired>());
        Assert.Equal(2, repaired.Amount);
    }

    [Fact]
    public void ObservationExposesRepair()
    {
        var (_, engine, _, _) = Build(new RepairModule(1000, 5, 2));

        var observed = engine.BuildObservation("A").Entities
            .Single(entity => entity.EntityId == 1);

        Assert.True(observed.HasRepair);
        Assert.Equal(1000, observed.RepairRangeMm);
        Assert.Equal(5, observed.RepairHealthPerTick);
    }

    [Fact]
    public void SaveRoundTripPreservesRepair()
    {
        var (world, _, _, _) = Build(
            new RepairModule(1000, 5, 2, MaterialPerTick: 1),
            material: 10);
        world.Workshop.Publish(new DeviceBlueprint(
            "repair_bay",
            0,
            "Repair Bay",
            MaxHealth: 100,
            EnergyCapacity: 100,
            Repair: new RepairModule(1000, 5, 2, MaterialPerTick: 1)));

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var repair = loaded.FindEntity(new EntityId(1))!.Repair;

        Assert.NotNull(repair);
        Assert.Equal(1000, repair!.RangeMm);
        Assert.Equal(5, repair.HealthPerTick);
        Assert.Equal(2, repair.EnergyCostPerTick);
        Assert.Equal(1, repair.MaterialPerTick);
        var blueprint = loaded.Workshop.GetVersion("repair_bay", 1);
        Assert.NotNull(blueprint);
        Assert.NotNull(blueprint!.Repair);
        Assert.Equal(1000, blueprint.Repair!.RangeMm);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    [Fact]
    public void HashIncludesRepairModule()
    {
        var (first, _, _, _) = Build(new RepairModule(1000, 5, 2));
        var (second, _, _, _) = Build(new RepairModule(1001, 5, 2));

        Assert.NotEqual(
            WorldStateHasher.Compute(first),
            WorldStateHasher.Compute(second));
    }

    [Fact]
    public void BlueprintCreatesRepairModule()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "repair_bay",
            0,
            "Repair Bay",
            MaxHealth: 100,
            EnergyCapacity: 100,
            Repair: new RepairModule(1000, 5, 2)));

        var entity = DeploymentService.CreateInstance(
            world,
            blueprint,
            "A",
            "r0",
            new Vec2(100, 100));

        Assert.NotNull(entity.Repair);
        Assert.Equal(1000, entity.Repair!.RangeMm);
    }
}
