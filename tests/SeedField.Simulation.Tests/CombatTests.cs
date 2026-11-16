using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class CombatTests
{
    [Fact]
    public void AttackOutsideWeaponRangeIsRejectedWithoutDamage()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 20_000, 20_000)
            .AddEntity(Attacker(new Vec2(100, 100), range: 1_000, sensorRange: 20_000))
            .AddEntity(Target(new Vec2(9_000, 100)))
            .Build();

        var engine = new SimulationEngine(world);
        var result = engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });

        Assert.Equal(SimulationErrorCode.OutOfRange, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Empty(result.Events.OfType<ShotFired>());
        Assert.Equal(100, world.FindEntity(new EntityId(2))!.Health);
    }

    [Fact]
    public void AttackAgainstAnUnsensedTargetIsRejected()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 20_000, 20_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 1_000,
                EnergyCapacity: 1_000,
                Material: 0,
                Sensor: new SensorModule(100),
                Weapon: new WeaponModule(10_000, Damage: 10, EnergyCost: 1, CooldownTicks: 0)))
            .AddEntity(Target(new Vec2(5_000, 100)))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });

        Assert.Equal(SimulationErrorCode.TargetUnknown, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Equal(100, world.FindEntity(new EntityId(2))!.Health);
        Assert.Empty(result.Events.OfType<ShotFired>());
    }

    [Fact]
    public void HitDealsDamageAndEmitsOrderedEvents()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 20_000, 20_000)
            .AddEntity(Attacker(new Vec2(100, 100), range: 5_000))
            .AddEntity(Target(new Vec2(1_000, 100)))
            .Build();

        var engine = new SimulationEngine(world);
        var result = engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        var shot = Assert.Single(result.Events.OfType<ShotFired>());
        var damage = Assert.Single(result.Events.OfType<EntityDamaged>());

        Assert.Equal(10, shot.Damage);
        Assert.Equal(90, damage.HealthRemaining);
        Assert.Equal(1, world.Statistics.ShotsFired);
        Assert.Equal(10, world.Statistics.DamageDealt);

        var eventList = result.Events.ToList();
        Assert.True(eventList.IndexOf(shot) < eventList.IndexOf(damage));
    }

    [Fact]
    public void WeaponCooldownRejectsAnImmediateSecondShot()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 20_000, 20_000)
            .AddEntity(Attacker(new Vec2(100, 100), range: 5_000, cooldownTicks: 3))
            .AddEntity(Target(new Vec2(1_000, 100)))
            .Build();

        var engine = new SimulationEngine(world);

        var first = engine.Step(new Intent[] { new AttackIntent(new EntityId(1), new EntityId(2)) });
        Assert.True(Assert.Single(first.IntentResults).Accepted);
        Assert.Equal(3, world.FindEntity(new EntityId(1))!.NextWeaponReadyTick);

        var second = engine.Step(new Intent[] { new AttackIntent(new EntityId(1), new EntityId(2)) });
        Assert.Equal(SimulationErrorCode.WeaponCoolingDown, Assert.Single(second.IntentResults).ErrorCode);
    }

    [Fact]
    public void DestroyedEntityInventoryIsAccountedAsLost()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 20_000, 20_000)
            .AddEntity(Attacker(new Vec2(100, 100), range: 5_000, damage: 100))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(1_000, 100),
                MaxHealth: 100,
                Energy: 30,
                EnergyCapacity: 100,
                Material: 5))
            .Build();

        var initialEnergy = world.EnumerateEntities().Sum(entity => entity.Energy);
        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });

        Assert.Equal(30, world.Statistics.EnergyLostTotal);
        Assert.Equal(5, world.Statistics.MaterialLostTotal);
        var lost = Assert.Single(result.Events.OfType<EntityInventoryLost>());
        Assert.Equal(30, lost.Energy);
        Assert.Equal(5, lost.Material);

        var finalEnergy = world.EnumerateEntities().Sum(entity => entity.Energy);
        Assert.Equal(
            initialEnergy,
            finalEnergy + world.Statistics.EnergySpentTotal + world.Statistics.EnergyLostTotal);
    }

    [Fact]
    public void LethalHitRemovesTargetAndLaterIntentsCannotSeeIt()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 20_000, 20_000)
            .AddEntity(Attacker(new Vec2(100, 100), range: 5_000, damage: 100))
            .AddEntity(Target(new Vec2(1_000, 100)))
            .Build();

        var engine = new SimulationEngine(world);
        var result = engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
            new AttackIntent(new EntityId(2), new EntityId(1)),
        });

        Assert.Contains(result.Events, item => item is EntityDestroyed { EntityId.Value: 2 });
        Assert.Null(world.FindEntity(new EntityId(2)));
        Assert.Equal(SimulationErrorCode.UnknownEntity, result.IntentResults[1].ErrorCode);
        Assert.Equal(1, world.Statistics.EntitiesDestroyed);
    }

    private static EntityDefinition Attacker(
        Vec2 position,
        int range,
        int damage = 10,
        int cooldownTicks = 0,
        int? sensorRange = null) =>
        new(
            new EntityId(1),
            "A",
            "r0",
            position,
            MaxHealth: 100,
            Energy: 1_000,
            EnergyCapacity: 1_000,
            Material: 0,
            Sensor: new SensorModule(sensorRange ?? range),
            Weapon: new WeaponModule(range, damage, EnergyCost: 5, cooldownTicks));

    private static EntityDefinition Target(Vec2 position) =>
        new(
            new EntityId(2),
            "B",
            "r0",
            position,
            MaxHealth: 100,
            Energy: 0,
            EnergyCapacity: 0,
            Material: 0);
}
