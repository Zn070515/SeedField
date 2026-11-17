namespace SeedField.Simulation;

/// <summary>Presentation state derived from a read-only core snapshot.</summary>
public enum CoreVisualState
{
    Absent,
    Idle,
    Regenerating,
    Bootstrap,
    Besieged,
}

/// <summary>
/// Deterministic classification for the four core art states. This does not
/// mutate the world and does not change recovery rules; it only names what the
/// presentation layer should draw.
/// </summary>
public static class CoreVisualStateRules
{
    public static CoreVisualState Classify(
        CoreRecoverySnapshot snapshot,
        long tick,
        long bootstrapUntilTick)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.HasCore)
        {
            return CoreVisualState.Absent;
        }

        if (snapshot.IsBesieged)
        {
            return CoreVisualState.Besieged;
        }

        if (tick < bootstrapUntilTick)
        {
            return CoreVisualState.Bootstrap;
        }

        if (snapshot.EnergyReserve < snapshot.MaxEnergyReserve ||
            snapshot.MaterialReserve < snapshot.MaxMaterialReserve)
        {
            return CoreVisualState.Regenerating;
        }

        return CoreVisualState.Idle;
    }
}
