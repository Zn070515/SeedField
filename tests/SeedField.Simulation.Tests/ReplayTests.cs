using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class ReplayTests
{
    [Fact]
    public void RecordAndReplayCombatRandomness()
    {
        var world = BuildWorld();
        var engine = new SimulationEngine(world);
        var recorder = new ReplayRecorder(world);
        long events = 0;
        for (var tick = 0; tick < 20; tick++)
        {
            var intents = new[] { Attack() };
            events += engine.Step(intents).Events.Count;
            recorder.Record(world, tick, intents);
        }

        var json = ReplaySerializer.Save(recorder.Build());
        var replayed = ReplayPlayer.Play(ReplaySerializer.Load(json));

        Assert.True(replayed.Success, $"diverged at {replayed.DivergedTick}");
        Assert.Equal(20, replayed.TickCount);
        Assert.Equal(events, replayed.EventCount);
        Assert.Equal(WorldStateHasher.Compute(world), replayed.FinalHash);
    }

    [Fact]
    public void TamperedTickHashReportsTheDivergentTick()
    {
        var world = BuildWorld();
        var engine = new SimulationEngine(world);
        var recorder = new ReplayRecorder(world);
        for (var tick = 0; tick < 3; tick++)
        {
            var intents = new[] { Attack() };
            engine.Step(intents);
            recorder.Record(world, tick, intents);
        }

        var data = recorder.Build();
        var hashes = data.TickHashes.ToArray();
        hashes[1] = "0x0000000000000000";
        var replayed = ReplayPlayer.Play(data with { TickHashes = hashes });

        Assert.False(replayed.Success);
        Assert.Equal(1, replayed.DivergedTick);
    }

    [Fact]
    public void RejectsUnsupportedReplaySchema()
    {
        var recorder = new ReplayRecorder(BuildWorld());
        var data = recorder.Build() with { SchemaVersion = 99 };

        Assert.Throws<ReplayFormatException>(
            () => ReplaySerializer.Load(ReplaySerializer.Save(data)));
    }

    [Fact]
    public void RejectsMismatchedTickHashesAndTicks()
    {
        var world = BuildWorld();
        var engine = new SimulationEngine(world);
        var recorder = new ReplayRecorder(world);
        var intents = new[] { Attack() };
        engine.Step(intents);
        recorder.Record(world, 0, intents);
        var data = recorder.Build();

        Assert.Throws<ReplayFormatException>(
            () => ReplayPlayer.Play(data with { TickHashes = Array.Empty<string>() }));
    }

    [Fact]
    public void EmptyReplayReturnsTheInitialHash()
    {
        var world = BuildWorld();
        var data = new ReplayRecorder(world).Build();

        var replayed = ReplayPlayer.Play(data);

        Assert.True(replayed.Success);
        Assert.Equal(0, replayed.TickCount);
        Assert.Equal(WorldStateHasher.Compute(world), replayed.FinalHash);
    }

    [Fact]
    public void AllIntentKindsRoundTripThroughTheReplayCodec()
    {
        var intents = new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(10, 20))
            {
                IssuerCivilizationId = "A",
            },
            new AttackIntent(new EntityId(1), new EntityId(2))
            {
                IssuerCivilizationId = "A",
            },
            new SendMessageIntent(new EntityId(1), "B", "topic", "payload", "r1")
            {
                IssuerCivilizationId = "A",
            },
            new WaitIntent(new EntityId(1))
            {
                IssuerCivilizationId = "A",
            },
            new TransferMaterialIntent(new EntityId(1), "r1", 5)
            {
                IssuerCivilizationId = "A",
            },
            new SendEnergyIntent(new EntityId(1), 7, "r1")
            {
                IssuerCivilizationId = "A",
            },
            new DrawEnergyIntent(new EntityId(1), 3)
            {
                IssuerCivilizationId = "A",
            },
        };

        foreach (var intent in intents)
        {
            var data = ReplayIntentCodec.ToData(intent);
            Assert.Equal(intent, ReplayIntentCodec.ToIntent(data));
        }
    }

    private static Intent Attack() =>
        new AttackIntent(new EntityId(1), new EntityId(2))
        {
            IssuerCivilizationId = "A",
        };

    private static WorldState BuildWorld()
    {
        var builder = new WorldBuilder()
            .AddRegion("r0", 20_000, 20_000)
            .SetRandomSeed(2026)
            .SetCombatRandomness(
                hitChancePercent: 60,
                damageVariancePercent: 40,
                lootChancePercent: 100,
                lootMaterial: 5)
            .AddEntity(TestWorlds.Duelist(1, "A", new Vec2(1_000, 1_000)));
        builder.AddEntity(TestWorlds.Duelist(2, "B", new Vec2(2_000, 1_000)));
        return builder.Build();
    }
}
