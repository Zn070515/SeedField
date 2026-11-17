namespace SeedField.Simulation;

/// <summary>
/// Scenario-configured regional grid policy for one civilization. ReserveEnergy is a
/// draw floor: devices may not reduce a region's grid stock below it.
/// </summary>
public sealed class PowerPolicyState
{
    internal PowerPolicyState(string civilizationId, long reserveEnergy)
    {
        CivilizationId = civilizationId;
        ReserveEnergy = reserveEnergy;
    }

    public string CivilizationId { get; }

    public long ReserveEnergy { get; internal set; }
}
