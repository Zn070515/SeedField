namespace SeedField.Simulation;

/// <summary>
/// Persistent breaker state for one region/civilization pair. ManualHold prevents
/// automatic reconnect after a deliberate open.
/// </summary>
public sealed class PowerGridState
{
    internal PowerGridState(string regionId, string civilizationId)
    {
        RegionId = regionId;
        CivilizationId = civilizationId;
    }

    public string RegionId { get; }

    public string CivilizationId { get; }

    public bool BreakerOpen { get; internal set; }

    public bool ManualHold { get; internal set; }
}
