using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class DeterministicRandomTests
{
    [Fact]
    public void SplitMix64MatchesKnownVectors()
    {
        var random = new DeterministicRandom(0);

        Assert.Equal(0xe220a8397b1dcdafUL, random.NextUInt64());
        Assert.Equal(0x6e789e6aa1b965f4UL, random.NextUInt64());
        Assert.Equal(0x06c45d188009454fUL, random.NextUInt64());
    }

    [Fact]
    public void SameSeedProducesTheSameStream()
    {
        var first = new DeterministicRandom(42);
        var second = new DeterministicRandom(42);

        for (var index = 0; index < 16; index++)
        {
            Assert.Equal(first.NextUInt64(), second.NextUInt64());
        }

        Assert.NotEqual(new DeterministicRandom(7).NextUInt64(), first.NextUInt64());
    }

    [Fact]
    public void ZeroPercentDoesNotConsumeTheStream()
    {
        var random = new DeterministicRandom(5);
        var state = random.State;

        Assert.False(random.Percent(0));
        Assert.Equal(state, random.State);

        Assert.True(random.Percent(100));
        Assert.NotEqual(state, random.State);
    }

    [Fact]
    public void RandomStateSurvivesSaveLoadAndContinuesTheStream()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetRandomSeed(1234)
            .Build();
        world.Random.NextUInt64();

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(1234UL, loaded.Random.Seed);
        Assert.Equal(world.Random.State, loaded.Random.State);
        Assert.Equal(world.Random.NextUInt64(), loaded.Random.NextUInt64());
    }

    [Fact]
    public void RandomFaultChanceCanLockOutOnTheFirstTrip()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetRandomSeed(9)
            .SetPowerPolicy(
                "A",
                reserveEnergy: 0,
                breakerFaultChancePercent: 100)
            .AddEntity(PowerNode())
            .Build();

        var result = new SimulationEngine(world).Step(Array.Empty<Intent>());

        var state = world.PowerGridStates[("r0", "A")];
        Assert.True(state.MaintenanceRequired);
        Assert.Equal(1, state.TripCount);
        var lockedOut = Assert.Single(result.Events.OfType<BreakerLockedOut>());
        Assert.Equal("fault", lockedOut.Reason);
    }

    [Fact]
    public void ZeroFaultChanceKeepsTheBreakerStreamUntouched()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetRandomSeed(9)
            .SetPowerPolicy("A", reserveEnergy: 0)
            .AddEntity(PowerNode())
            .Build();
        var seed = world.Random.State;

        new SimulationEngine(world).Step(Array.Empty<Intent>());

        Assert.False(world.PowerGridStates[("r0", "A")].MaintenanceRequired);
        Assert.Equal(seed, world.Random.State);
    }

    [Fact]
    public void V12SaveMigratesToZeroRandomAndFaultChance()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 5)
            .Build();
        var data = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 12,
            PowerPolicies = new[] { new PowerPolicySaveData("A", 5) },
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(data));

        Assert.Equal(0UL, loaded.Random.Seed);
        Assert.Equal(0UL, loaded.Random.State);
        Assert.Equal(0, loaded.PowerPolicies["A"].BreakerFaultChancePercent);
    }

    private static EntityDefinition PowerNode() =>
        new(
            new EntityId(1),
            "A",
            "r0",
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 0,
            EnergyCapacity: 100,
            Material: 0,
            Power: new PowerModule(0, 100));
}
