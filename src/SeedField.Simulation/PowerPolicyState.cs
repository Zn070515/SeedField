namespace SeedField.Simulation;

/// <summary>
/// Scenario-configured regional grid policy for one civilization. ReserveEnergy is a
/// draw floor: devices may not reduce a region's grid stock below it.
/// </summary>
public sealed class PowerPolicyState
{
    internal PowerPolicyState(
        string civilizationId,
        long reserveEnergy,
        int highPriorityThreshold,
        long priorityReserveEnergy,
        bool autoReconnect)
    {
        CivilizationId = civilizationId;
        ReserveEnergy = reserveEnergy;
        HighPriorityThreshold = highPriorityThreshold;
        PriorityReserveEnergy = priorityReserveEnergy;
        AutoReconnect = autoReconnect;
    }

    public string CivilizationId { get; }

    public long ReserveEnergy { get; internal set; }

    /// <summary>Devices at or above this priority may draw down to ReserveEnergy.</summary>
    public int HighPriorityThreshold { get; internal set; }

    /// <summary>Extra draw floor reserved for high-priority devices.</summary>
    public long PriorityReserveEnergy { get; internal set; }

    public bool AutoReconnect { get; internal set; }
}
