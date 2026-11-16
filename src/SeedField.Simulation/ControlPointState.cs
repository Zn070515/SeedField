namespace SeedField.Simulation;

/// <summary>
/// A single capture objective. A civilization must be the only occupant inside the
/// radius for HoldTicksRequired consecutive ticks; contest or absence resets progress.
/// The real hold duration is scenario-configurable and not a frozen balance value.
/// </summary>
public sealed class ControlPointState
{
    internal ControlPointState(
        string id,
        string regionId,
        Vec2 position,
        int radiusMm,
        int holdTicksRequired)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Control point id is required.", nameof(id));
        }

        if (radiusMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusMm), "Radius must be positive.");
        }

        if (holdTicksRequired <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(holdTicksRequired),
                "Hold duration must be positive.");
        }

        Id = id;
        RegionId = regionId;
        Position = position;
        RadiusMm = radiusMm;
        HoldTicksRequired = holdTicksRequired;
    }

    public string Id { get; }

    public string RegionId { get; }

    public Vec2 Position { get; }

    public int RadiusMm { get; }

    public int HoldTicksRequired { get; }

    public string? HoldingCivilizationId { get; internal set; }

    public int HoldTicks { get; internal set; }

    public string? OwnerCivilizationId { get; internal set; }

    public bool IsCaptured => OwnerCivilizationId is not null;
}
