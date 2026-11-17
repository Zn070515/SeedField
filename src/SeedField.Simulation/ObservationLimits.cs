namespace SeedField.Simulation;

/// <summary>
/// Bounds the per-observation message history. The inbox itself keeps its
/// storage/expiry semantics; scripts see only the most recent delivered messages
/// so a large fleet cannot make every observation carry the whole shared inbox.
/// </summary>
public static class ObservationLimits
{
    public const int MaxMessages = 32;
}
