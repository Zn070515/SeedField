namespace SeedField.Simulation;

public sealed record ReplayResult(
    bool Success,
    long? DivergedTick,
    ulong FinalHash,
    int TickCount,
    long EventCount);

/// <summary>
/// Replays recorded intents against the initial world and verifies each tick's
/// state hash. The replay only contains data; it never executes controller code.
/// </summary>
public static class ReplayPlayer
{
    public static ReplayResult Play(ReplayData data)
    {
        ReplaySerializer.Validate(data);
        var world = WorldSaveSerializer.Load(data.InitialWorld);
        var engine = new SimulationEngine(world);
        long events = 0;

        for (var index = 0; index < data.Ticks.Length; index++)
        {
            var tick = data.Ticks[index];
            if (tick.Tick != world.Tick)
            {
                throw new ReplayFormatException(
                    "REPLAY_TICK_MISMATCH",
                    $"Replay tick {tick.Tick} does not match world tick {world.Tick}.");
            }

            var intents = tick.Intents
                .Select(ReplayIntentCodec.ToIntent)
                .ToArray();
            var result = engine.Step(intents);
            events += result.Events.Count;

            var actual = WorldStateHasher.Compute(world);
            var expected = ReplaySerializer.ParseHash(data.TickHashes[index]);
            if (actual != expected)
            {
                return new ReplayResult(
                    false,
                    tick.Tick,
                    actual,
                    index + 1,
                    events);
            }
        }

        return new ReplayResult(
            true,
            null,
            WorldStateHasher.Compute(world),
            data.Ticks.Length,
            events);
    }
}
