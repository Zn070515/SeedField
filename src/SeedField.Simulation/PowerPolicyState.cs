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
        bool autoReconnect,
        int breakerTripLimit = 0,
        long breakerMaintenanceMaterialCost = 0,
        int breakerFaultChancePercent = 0)
    {
        CivilizationId = civilizationId;
        ReserveEnergy = reserveEnergy;
        HighPriorityThreshold = highPriorityThreshold;
        PriorityReserveEnergy = priorityReserveEnergy;
        AutoReconnect = autoReconnect;
        BreakerTripLimit = breakerTripLimit;
        BreakerMaintenanceMaterialCost = breakerMaintenanceMaterialCost;
        BreakerFaultChancePercent = breakerFaultChancePercent;
    }

    public string CivilizationId { get; }

    public long ReserveEnergy { get; internal set; }

    /// <summary>Devices at or above this priority may draw down to ReserveEnergy.</summary>
    public int HighPriorityThreshold { get; internal set; }

    /// <summary>Extra draw floor reserved for high-priority devices.</summary>
    public long PriorityReserveEnergy { get; internal set; }

    public bool AutoReconnect { get; internal set; }

    /// <summary>
    /// Fault trips before the breaker locks out for maintenance. Zero disables lockout.
    /// </summary>
    public int BreakerTripLimit { get; internal set; }

    /// <summary>Material charged when the host performs breaker maintenance.</summary>
    public long BreakerMaintenanceMaterialCost { get; internal set; }

    /// <summary>
    /// Chance per fault trip (0..100) that the breaker requires maintenance at once.
    /// Zero consumes no random values.
    /// </summary>
    public int BreakerFaultChancePercent { get; internal set; }
}
