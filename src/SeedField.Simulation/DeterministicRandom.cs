namespace SeedField.Simulation;

/// <summary>
/// SplitMix64 stream stored in the world state. Every consumer must take values in
/// the same deterministic order and the seed/state must be saved and hashed, otherwise
/// replays and save round-trips diverge.
/// </summary>
public sealed class DeterministicRandom
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;
    private const ulong Mix1 = 0xBF58476D1CE4E5B9UL;
    private const ulong Mix2 = 0x94D049BB133111EBUL;

    public DeterministicRandom(ulong seed = 0)
    {
        Seed = seed;
        State = seed;
    }

    /// <summary>Scenario seed; kept for diagnostics and save identity.</summary>
    public ulong Seed { get; private set; }

    /// <summary>Next SplitMix64 counter value. Hashed and saved with the world.</summary>
    public ulong State { get; internal set; }

    internal void Reset(ulong seed)
    {
        Seed = seed;
        State = seed;
    }

    internal void Restore(ulong seed, ulong state)
    {
        Seed = seed;
        State = state;
    }

    public ulong NextUInt64()
    {
        State += Gamma;
        var value = State;
        value = (value ^ (value >> 30)) * Mix1;
        value = (value ^ (value >> 27)) * Mix2;
        return value ^ (value >> 31);
    }

    /// <summary>Uniform value in [0, maxExclusive).</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                "maxExclusive must be positive.");
        }

        var bound = ulong.MaxValue - (ulong.MaxValue % (ulong)maxExclusive);
        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value >= bound);

        return (int)(value % (ulong)maxExclusive);
    }

    /// <summary>True with the given integer percentage chance (0..100).</summary>
    public bool Percent(int percent)
    {
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent),
                "percent must be within 0..100.");
        }

        return percent > 0 && NextInt(100) < percent;
    }
}
