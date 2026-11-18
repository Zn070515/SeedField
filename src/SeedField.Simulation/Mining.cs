namespace SeedField.Simulation;

/// <summary>Visual/flavor ore kind. All kinds currently yield the same material M.</summary>
public enum OreKind
{
    Copper = 0,
    Lead = 1,
    Titanium = 2,
    Coal = 3,
}

/// <summary>
/// A finite material deposit inside one region. Position, radius and remaining
/// material are authoritative; the visual patch is derived presentation data.
/// </summary>
public sealed class ResourceDepositState
{
    internal ResourceDepositState(
        long id,
        string regionId,
        OreKind kind,
        Vec2 position,
        int radiusMm,
        long remaining,
        long yieldPerTick)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        if (string.IsNullOrWhiteSpace(regionId))
        {
            throw new ArgumentException("Region id is required.", nameof(regionId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (radiusMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusMm));
        }

        if (remaining < 0 || yieldPerTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remaining));
        }

        Id = id;
        RegionId = regionId;
        Kind = kind;
        Position = position;
        RadiusMm = radiusMm;
        Remaining = remaining;
        YieldPerTick = yieldPerTick;
    }

    public long Id { get; }

    public string RegionId { get; }

    public OreKind Kind { get; }

    public Vec2 Position { get; }

    public int RadiusMm { get; }

    /// <summary>Material left in the deposit. Only the simulation mutates it.</summary>
    public long Remaining { get; internal set; }

    /// <summary>Maximum material the deposit can yield per mining action.</summary>
    public long YieldPerTick { get; }

    public bool IsDepleted => Remaining <= 0;

    public bool Covers(Vec2 position) => position.IsWithinRange(Position, RadiusMm);
}

/// <summary>Material extraction module. Values are scenario data, not balance.</summary>
public sealed record DrillModule(
    long RatePerTick,
    int RangeMm,
    long EnergyCostPerTick);
