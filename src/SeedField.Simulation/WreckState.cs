namespace SeedField.Simulation;

/// <summary>
/// Salvageable remains of a destroyed blueprint instance. Wrecks are world state:
/// they persist across saves, can be salvaged by the owner anywhere, or by another
/// civilization that has a living entity in the wreck's region.
/// </summary>
public sealed class WreckState
{
    internal WreckState(
        long id,
        string regionId,
        Vec2 position,
        string ownerCivilizationId,
        string blueprintId,
        int blueprintVersion,
        long materialRecoverable,
        long createdTick)
    {
        Id = id;
        RegionId = regionId;
        Position = position;
        OwnerCivilizationId = ownerCivilizationId;
        BlueprintId = blueprintId;
        BlueprintVersion = blueprintVersion;
        MaterialRecoverable = materialRecoverable;
        CreatedTick = createdTick;
    }

    public long Id { get; }

    public string RegionId { get; }

    public Vec2 Position { get; }

    public string OwnerCivilizationId { get; }

    public string BlueprintId { get; }

    public int BlueprintVersion { get; }

    public long MaterialRecoverable { get; }

    public long CreatedTick { get; }

    /// <summary>Civilization currently occupying this wreck (null = unoccupied).</summary>
    public string? OccupiedByCivilizationId { get; internal set; }

    /// <summary>Tick the occupation started, or -1 when unoccupied.</summary>
    public long OccupiedTick { get; internal set; } = -1;

    internal void Occupy(string civilizationId, long tick)
    {
        OccupiedByCivilizationId = civilizationId;
        OccupiedTick = tick;
    }
}
