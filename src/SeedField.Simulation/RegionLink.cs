namespace SeedField.Simulation;

/// <summary>
/// Explicit bidirectional link between two real 2D regions. MessagesPerTick 0 means
/// unlimited in this prototype; a positive value caps cross-region messages per tick.
/// Link economics and transport formulas remain configurable prototype values.
/// </summary>
public sealed record RegionLink(
    string Id,
    string RegionA,
    string RegionB,
    int LatencyTicks,
    int MessagesPerTick = 0,
    int MaterialPerTick = 0)
{
    public bool Connects(string regionId) =>
        string.Equals(RegionA, regionId, StringComparison.Ordinal) ||
        string.Equals(RegionB, regionId, StringComparison.Ordinal);

    public string Other(string regionId) =>
        string.Equals(RegionA, regionId, StringComparison.Ordinal) ? RegionB : RegionA;
}
