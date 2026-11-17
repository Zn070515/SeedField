namespace SeedField.Simulation;

/// <summary>
/// Persistent breaker state for one region/civilization pair. ManualHold prevents
/// automatic reconnect after a deliberate open; MaintenanceRequired blocks every
/// reconnect path until the host performs maintenance.
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

    /// <summary>Fault trips since the last maintenance. Manual opens do not count.</summary>
    public int TripCount { get; internal set; }

    /// <summary>Set when trips reach the configured limit; cleared by maintenance.</summary>
    public bool MaintenanceRequired { get; internal set; }
}
