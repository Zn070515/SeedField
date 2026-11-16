using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void SameWorldAndSameController_ProducesIdenticalHashesForFiveThousandTicks()
    {
        var first = RunScripted(TestWorlds.CombatPair(), 5_000);
        var second = RunScripted(TestWorlds.CombatPair(), 5_000);

        Assert.Equal(first.Hashes, second.Hashes);
        Assert.Equal(first.FinalHash, second.FinalHash);
        Assert.True(first.EventsSeen > 0);
    }

    [Fact]
    public void EnergyIsConservedWhenEntitiesAreNotDestroyed()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 10_000,
                EnergyCapacity: 10_000,
                Material: 0,
                Drive: new DriveModule(50, EnergyCostPerMove: 3)))
            .Build();

        var engine = new SimulationEngine(world);
        var initialEnergy = world.EnumerateEntities().Sum(entity => entity.Energy);

        for (var i = 0; i < 500; i++)
        {
            engine.Step(new Intent[]
            {
                new MoveIntent(new EntityId(1), new Vec2(9_000, 9_000)),
            });
        }

        var finalEnergy = world.EnumerateEntities().Sum(entity => entity.Energy);
        Assert.Equal(initialEnergy - world.Statistics.EnergySpentTotal, finalEnergy);
        Assert.Equal(0, world.Statistics.EnergyRegeneratedTotal);
        Assert.True(world.Statistics.EnergySpentTotal > 0);
    }

    private static RunResult RunScripted(WorldState world, int ticks)
    {
        var engine = new SimulationEngine(world);
        var hashes = new List<ulong>();
        var eventsSeen = 0;

        for (var i = 0; i < ticks; i++)
        {
            var result = engine.Step(Policy(world));
            eventsSeen += result.Events.Count;

            if ((i + 1) % 500 == 0)
            {
                hashes.Add(WorldStateHasher.Compute(world));
            }
        }

        return new RunResult(hashes, WorldStateHasher.Compute(world), eventsSeen);
    }

    private static List<Intent> Policy(WorldState world)
    {
        var intents = new List<Intent>();
        var entities = world.EnumerateEntities().ToList();

        foreach (var entity in entities)
        {
            var enemy = entities.FirstOrDefault(candidate =>
                !string.Equals(candidate.CivilizationId, entity.CivilizationId, StringComparison.Ordinal));

            if (enemy is null)
            {
                intents.Add(new WaitIntent(entity.Id));
                continue;
            }

            var weaponRange = entity.Weapon?.RangeMm ?? 0;
            if (entity.Position.IsWithinRange(enemy.Position, weaponRange))
            {
                intents.Add(new AttackIntent(entity.Id, enemy.Id));
            }
            else
            {
                intents.Add(new MoveIntent(entity.Id, enemy.Position));
            }
        }

        return intents;
    }

    private sealed record RunResult(List<ulong> Hashes, ulong FinalHash, int EventsSeen);
}
