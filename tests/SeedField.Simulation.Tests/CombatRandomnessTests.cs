using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class CombatRandomnessTests
{
    [Fact]
    public void ZeroHitChanceMissesWithoutConsumingRandomness()
    {
        var world = World(hitChance: 0);
        var state = world.Random.State;

        var result = new SimulationEngine(world).Step(new[] { Attack() });

        Assert.Empty(result.Events.OfType<ShotFired>());
        Assert.Single(result.Events.OfType<ShotMissed>());
        Assert.Equal(100, world.FindEntity(new EntityId(2))!.Health);
        Assert.Equal(1, world.Statistics.ShotsFired);
        Assert.Equal(1, world.Statistics.ShotsMissed);
        Assert.Equal(state, world.Random.State);
    }

    [Fact]
    public void FullHitChanceDoesNotConsumeRandomness()
    {
        var world = World(hitChance: 100);
        var state = world.Random.State;

        var result = new SimulationEngine(world).Step(new[] { Attack() });

        var shot = Assert.Single(result.Events.OfType<ShotFired>());
        Assert.Equal(7, shot.Damage);
        Assert.Equal(93, world.FindEntity(new EntityId(2))!.Health);
        Assert.Equal(state, world.Random.State);
    }

    [Fact]
    public void DamageVarianceConsumesOneDrawAndRoundsDown()
    {
        var world = World(damageVariance: 50);
        var probe = new DeterministicRandom(123);
        var offset = probe.NextInt(101) - 50;
        var expectedDamage = Math.Max(1, 7 * (100 + offset) / 100);

        var result = new SimulationEngine(world).Step(new[] { Attack() });

        var shot = Assert.Single(result.Events.OfType<ShotFired>());
        Assert.Equal(expectedDamage, shot.Damage);
        Assert.Equal(probe.State, world.Random.State);
        Assert.InRange(shot.Damage, 4, 10);
    }

    [Fact]
    public void LootChanceGrantsConfiguredMaterialOnKill()
    {
        var world = World(lootChance: 100, lootMaterial: 5, targetDies: true);
        var state = world.Random.State;

        var result = new SimulationEngine(world).Step(new[] { Attack() });

        Assert.Null(world.FindEntity(new EntityId(2)));
        var loot = Assert.Single(result.Events.OfType<CombatLootGranted>());
        Assert.Equal(5, loot.Material);
        Assert.Equal(5, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(5, world.Statistics.MaterialLootedTotal);
        Assert.Equal(state, world.Random.State);
    }

    [Fact]
    public void ZeroLootChanceDoesNotConsumeOrGrantMaterial()
    {
        var world = World(lootChance: 0, lootMaterial: 5, targetDies: true);
        var state = world.Random.State;

        var result = new SimulationEngine(world).Step(new[] { Attack() });

        Assert.Empty(result.Events.OfType<CombatLootGranted>());
        Assert.Equal(0, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(0, world.Statistics.MaterialLootedTotal);
        Assert.Equal(state, world.Random.State);
    }

    [Fact]
    public void SameSeedAndConfigProduceTheSameCombatStream()
    {
        var first = World(hitChance: 50, damageVariance: 50, lootChance: 50, lootMaterial: 3);
        var second = World(hitChance: 50, damageVariance: 50, lootChance: 50, lootMaterial: 3);
        var firstEvents = Run(first, 12);
        var secondEvents = Run(second, 12);

        Assert.Equal(WorldStateHasher.Compute(first), WorldStateHasher.Compute(second));
        Assert.Equal(
            firstEvents.Select(item => item.ToString()),
            secondEvents.Select(item => item.ToString()));
    }

    [Fact]
    public void CombatRulesRoundTripThroughSaveAndLoad()
    {
        var world = World(
            hitChance: 90,
            damageVariance: 25,
            lootChance: 60,
            lootMaterial: 9);
        world.Random.NextUInt64();

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(90, loaded.CombatHitChancePercent);
        Assert.Equal(25, loaded.CombatDamageVariancePercent);
        Assert.Equal(60, loaded.CombatLootChancePercent);
        Assert.Equal(9, loaded.CombatLootMaterial);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    [Fact]
    public void V17SaveMigratesToDeterministicCombatDefaults()
    {
        var world = World();
        var data = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 17,
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(data));

        Assert.Equal(100, loaded.CombatHitChancePercent);
        Assert.Equal(0, loaded.CombatDamageVariancePercent);
        Assert.Equal(0, loaded.CombatLootChancePercent);
        Assert.Equal(0, loaded.CombatLootMaterial);
        Assert.Equal(0, loaded.Statistics.ShotsMissed);
        Assert.Equal(0, loaded.Statistics.MaterialLootedTotal);
    }

    [Fact]
    public void InvalidCombatRulesAreRejectedOnLoad()
    {
        var world = World();
        var data = WorldSaveSerializer.ToData(world) with
        {
            CombatHitChancePercent = 101,
        };

        Assert.Throws<SaveFormatException>(
            () => WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(data)));
    }

    private static List<SimulationEvent> Run(WorldState world, int attacks)
    {
        var engine = new SimulationEngine(world);
        var events = new List<SimulationEvent>();
        for (var tick = 0; tick < attacks; tick++)
        {
            events.AddRange(engine.Step(new[] { Attack() }).Events);
        }

        return events;
    }

    private static Intent Attack() =>
        new AttackIntent(new EntityId(1), new EntityId(2))
        {
            IssuerCivilizationId = "A",
        };

    private static WorldState World(
        int hitChance = 100,
        int damageVariance = 0,
        int lootChance = 0,
        long lootMaterial = 0,
        bool targetDies = false,
        ulong seed = 123)
    {
        var builder = new WorldBuilder()
            .AddRegion("r0", 20_000, 20_000)
            .SetRandomSeed(seed)
            .SetCombatRandomness(
                hitChancePercent: hitChance,
                damageVariancePercent: damageVariance,
                lootChancePercent: lootChance,
                lootMaterial: lootMaterial)
            .AddEntity(TestWorlds.Duelist(1, "A", new Vec2(1_000, 1_000)));
        builder.AddEntity(targetDies
            ? Target(2, "B", new Vec2(2_000, 1_000), 1)
            : TestWorlds.Duelist(2, "B", new Vec2(2_000, 1_000)));
        return builder.Build();
    }

    private static EntityDefinition Target(
        long id,
        string civilizationId,
        Vec2 position,
        int health) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            position,
            MaxHealth: health,
            Energy: 0,
            EnergyCapacity: 0,
            Material: 0);
}
