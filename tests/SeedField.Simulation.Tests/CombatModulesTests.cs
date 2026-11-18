using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// M4 combat depth: shields absorb before health, armor reduces direct damage,
/// beam weapons ignore armor, shields regenerate after a delay, and critical
/// damage disables modules.
/// </summary>
public sealed class CombatModulesTests
{
    [Fact]
    public void ShieldAbsorbsDamageBeforeHealthAndArmorReducesIt()
    {
        var world = World(
            target: Target(
                armor: new ArmorModule(4),
                shield: new ShieldModule(Capacity: 30, RegenPerTick: 0, RegenDelayTicks: 0, EnergyCostPerRegen: 0)));
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[] { new AttackIntent(new EntityId(1), new EntityId(2)) });

        var damaged = result.Events.OfType<EntityDamaged>().Single();
        Assert.Equal(0, damaged.Damage);
        Assert.Equal(6, damaged.ShieldAbsorbed);
        Assert.Equal(4, damaged.ArmorAbsorbed);
        Assert.Equal(24, world.FindEntity(new EntityId(2))!.ShieldEnergy);
        Assert.Equal(100, world.FindEntity(new EntityId(2))!.Health);
    }

    [Fact]
    public void BeamWeaponIgnoresArmor()
    {
        var world = World(
            weaponKind: WeaponKind.Beam,
            target: Target(
                armor: new ArmorModule(4),
                shield: new ShieldModule(Capacity: 30, RegenPerTick: 0, RegenDelayTicks: 0, EnergyCostPerRegen: 0)));
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[] { new AttackIntent(new EntityId(1), new EntityId(2)) });

        var damaged = result.Events.OfType<EntityDamaged>().Single();
        Assert.Equal(10, damaged.ShieldAbsorbed);
        Assert.Equal(0, damaged.ArmorAbsorbed);
        Assert.Equal(20, world.FindEntity(new EntityId(2))!.ShieldEnergy);
    }

    [Fact]
    public void ShieldRegeneratesAfterTheDelayAndSpendsEnergy()
    {
        var world = World(
            target: Target(
                energy: 100,
                shield: new ShieldModule(30, 5, 2, 1)));
        var engine = new SimulationEngine(world);
        var entity = world.FindEntity(new EntityId(2))!;

        engine.Step(new Intent[] { new AttackIntent(new EntityId(1), new EntityId(2)) });
        engine.Step();
        var beforeDelay = entity.ShieldEnergy;
        engine.Step();
        engine.Step();
        engine.Step();

        Assert.Equal(20, beforeDelay);
        Assert.Equal(30, entity.ShieldEnergy);
        Assert.Equal(98, entity.Energy);
    }

    [Fact]
    public void CriticalDamageDisablesWeaponDriveAndSensor()
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
                Sensor: new SensorModule(1_000),
                Weapon: new WeaponModule(1_000, 10, 1, 1),
                Drive: new DriveModule(100, 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(200, 100),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Sensor: new SensorModule(1_000),
                Weapon: new WeaponModule(1_000, Damage: 80, EnergyCost: 1, CooldownTicks: 1)))
            .Build();
        var engine = new SimulationEngine(world);
        var attacker = world.FindEntity(new EntityId(1))!;
        engine.Step(new Intent[] { new AttackIntent(new EntityId(2), new EntityId(1)) });
        Assert.Equal(20, attacker.Health);

        var move = engine.Step(new Intent[] { new MoveIntent(new EntityId(1), new Vec2(900, 900)) });
        var attack = engine.Step(new Intent[] { new AttackIntent(new EntityId(1), new EntityId(2)) });

        Assert.Equal(SimulationErrorCode.ModuleDisabled, move.IntentResults[0].ErrorCode);
        Assert.Equal(SimulationErrorCode.ModuleDisabled, attack.IntentResults[0].ErrorCode);
        Assert.True(attacker.IsCritical);
        Assert.False(VisibilityRules.IsSensedBy(
            attacker,
            world.FindEntity(new EntityId(2))!));
    }

    [Fact]
    public void DamageStateThresholdsAreStable()
    {
        var atHalf = World(attackerDamage: 50);
        var engine = new SimulationEngine(atHalf);
        engine.Step(new Intent[] { new AttackIntent(new EntityId(1), new EntityId(2)) });
        var damaged = atHalf.FindEntity(new EntityId(2))!;
        Assert.True(damaged.IsDamaged);
        Assert.False(damaged.IsCritical);

        var aboveHalf = World(attackerDamage: 49);
        engine = new SimulationEngine(aboveHalf);
        engine.Step(new Intent[] { new AttackIntent(new EntityId(1), new EntityId(2)) });
        var nominal = aboveHalf.FindEntity(new EntityId(2))!;
        Assert.False(nominal.IsDamaged);
        Assert.False(nominal.IsCritical);
    }

    [Fact]
    public void ObservationExposesArmorShieldAndProductionModules()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 11, ore: 7, components: 3)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 50,
                EnergyCapacity: 100,
                Material: 0,
                Sensor: new SensorModule(1_000),
                Weapon: new WeaponModule(1_000, 5, 1, 1, WeaponKind.Beam),
                Armor: new ArmorModule(4),
                Shield: new ShieldModule(30, 2, 3, 1),
                Smelter: new SmelterModule(3, 1),
                Fabricator: new FabricatorModule(1, 2, 1, 10)))
            .Build();
        var observation = new SimulationEngine(world)
            .BuildControllerObservation(new EntityId(1));
        var entity = Assert.Single(observation.Entities);

        Assert.Equal(4, entity.Armor);
        Assert.Equal(30, entity.Shield);
        Assert.Equal(30, entity.MaxShield);
        Assert.Equal("Beam", entity.WeaponKind);
        Assert.True(entity.HasSmelter);
        Assert.True(entity.HasFabricator);
        Assert.Equal(11, observation.Material);
        Assert.Equal(7, observation.Ore);
        Assert.Equal(3, observation.Components);
    }

    [Fact]
    public void SaveRoundTripKeepsArmorShieldAndCurrentShield()
    {
        var world = World(
            target: Target(
                armor: new ArmorModule(4),
                shield: new ShieldModule(30, 5, 2, 1)));
        var engine = new SimulationEngine(world);
        engine.Step(new Intent[] { new AttackIntent(new EntityId(1), new EntityId(2)) });

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var target = loaded.FindEntity(new EntityId(2))!;

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(4, target.Armor!.FlatReduction);
        Assert.Equal(24, target.ShieldEnergy);
        Assert.NotNull(target.Shield);
    }

    private static WorldState World(
        WeaponKind weaponKind = WeaponKind.Direct,
        int attackerDamage = 10,
        EntityDefinition? target = null)
    {
        var builder = new WorldBuilder().AddRegion("r0", 10_000, 10_000);
        builder.AddEntity(new EntityDefinition(
            new EntityId(1),
            "A",
            "r0",
            new Vec2(100, 100),
            MaxHealth: 100,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Sensor: new SensorModule(1_000),
            Weapon: new WeaponModule(
                1_000,
                Damage: attackerDamage,
                EnergyCost: 1,
                CooldownTicks: 1,
                Kind: weaponKind),
            Drive: new DriveModule(100, 1)));
        builder.AddEntity(target ?? Target());
        return builder.Build();
    }

    private static EntityDefinition Target(
        long energy = 100,
        ArmorModule? armor = null,
        ShieldModule? shield = null) =>
        new(
            new EntityId(2),
            "B",
            "r0",
            new Vec2(200, 100),
            MaxHealth: 100,
            Energy: energy,
            EnergyCapacity: 100,
            Material: 0,
            Armor: armor,
            Shield: shield);
}
