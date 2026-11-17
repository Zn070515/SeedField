namespace SeedField.Simulation;

/// <summary>
/// Records the initial world and the intents submitted each tick. Call
/// <see cref="Record"/> after <see cref="SimulationEngine.Step"/> so the stored
/// hash is the post-step state.
/// </summary>
public sealed class ReplayRecorder
{
    private readonly string _initialWorld;
    private readonly List<ReplayTickData> _ticks = new();
    private readonly List<string> _hashes = new();

    public ReplayRecorder(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _initialWorld = WorldSaveSerializer.Save(world);
    }

    public int Count => _ticks.Count;

    public void Record(WorldState world, long tick, IReadOnlyList<Intent> intents)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(intents);
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        if (_ticks.Count > 0 && tick <= _ticks[^1].Tick)
        {
            throw new ArgumentException(
                "Replay ticks must be strictly increasing.",
                nameof(tick));
        }

        _ticks.Add(new ReplayTickData(
            tick,
            intents.Select(ReplayIntentCodec.ToData).ToArray()));
        _hashes.Add(ReplaySerializer.FormatHash(WorldStateHasher.Compute(world)));
    }

    public ReplayData Build() =>
        new(
            ReplaySerializer.CurrentSchemaVersion,
            _initialWorld,
            _ticks.ToArray(),
            _hashes.ToArray());

    public string Save() => ReplaySerializer.Save(Build());
}
