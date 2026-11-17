using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunCombatRandomnessDemo()
    {
        var world = BuildCombatWorld(
            hitChance: 60,
            damageVariance: 40,
            lootChance: 0,
            lootMaterial: 0,
            targetHealth: 100);
        var engine = new SimulationEngine(world);
        var hits = 0;
        var misses = 0;
        for (var tick = 0; tick < 8; tick++)
        {
            foreach (var simulationEvent in engine.Step(new[] { Attack() }).Events)
            {
                if (simulationEvent is ShotFired shot)
                {
                    hits += 1;
                    Console.WriteLine(
                        $"COMBAT_RNG t={shot.Tick} hit target={shot.TargetId} " +
                        $"damage={shot.Damage} energy={shot.EnergySpent}");
                }
                else if (simulationEvent is ShotMissed missed)
                {
                    misses += 1;
                    Console.WriteLine(
                        $"COMBAT_RNG t={missed.Tick} miss target={missed.TargetId} " +
                        $"energy={missed.EnergySpent}");
                }
            }
        }

        var target = world.FindEntity(new EntityId(2));
        Console.WriteLine(
            $"COMBAT_RNG summary hits={hits} misses={misses} " +
            $"health={target?.Health ?? 0} state=0x{world.Random.State:x16}");

        var lootWorld = BuildCombatWorld(
            hitChance: 100,
            damageVariance: 0,
            lootChance: 100,
            lootMaterial: 5,
            targetHealth: 1);
        var lootResult = new SimulationEngine(lootWorld).Step(new[] { Attack() });
        var loot = lootResult.Events.OfType<CombatLootGranted>().Single();
        Console.WriteLine(
            $"COMBAT_LOOT material={loot.Material} " +
            $"stock={lootWorld.Regions["frontier"].GetMaterial("A")} " +
            $"looted={lootWorld.Statistics.MaterialLootedTotal}");

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        Console.WriteLine(
            $"COMBAT_RNG saveHash=0x{WorldStateHasher.Compute(world):x16} " +
            $"loadHash=0x{WorldStateHasher.Compute(loaded):x16}");
        return 0;
    }

    private static Intent Attack() =>
        new AttackIntent(new EntityId(1), new EntityId(2))
        {
            IssuerCivilizationId = "A",
        };

    private static WorldState BuildCombatWorld(
        int hitChance,
        int damageVariance,
        int lootChance,
        long lootMaterial,
        int targetHealth)
    {
        var builder = new WorldBuilder()
            .AddRegion("frontier", 20_000, 20_000)
            .SetRandomSeed(2026)
            .SetCombatRandomness(
                hitChancePercent: hitChance,
                damageVariancePercent: damageVariance,
                lootChancePercent: lootChance,
                lootMaterial: lootMaterial)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "frontier",
                new Vec2(1_000, 1_000),
                MaxHealth: 100,
                Energy: 1_000_000,
                EnergyCapacity: 1_000_000,
                Material: 0,
                Sensor: new SensorModule(6_000),
                Weapon: new WeaponModule(
                    RangeMm: 5_000,
                    Damage: 7,
                    EnergyCost: 1,
                    CooldownTicks: 1),
                Drive: new DriveModule(SpeedMmPerTick: 100, EnergyCostPerMove: 1),
                Comm: new CommModule(LatencyTicks: 1, EnergyCostPerMessage: 1)));
        builder.AddEntity(new EntityDefinition(
            new EntityId(2),
            "B",
            "frontier",
            new Vec2(2_000, 1_000),
            MaxHealth: targetHealth,
            Energy: 0,
            EnergyCapacity: 0,
            Material: 0));
        return builder.Build();
    }
}
