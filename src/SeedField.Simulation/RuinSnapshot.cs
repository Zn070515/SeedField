using System.Text.Json;

namespace SeedField.Simulation;

/// <summary>
/// Data-only snapshot of one civilization's structures. It never contains code,
/// controllers or module definitions; importing it can only create ruins/wrecks.
/// </summary>
public sealed record RuinSnapshotData(
    int SchemaVersion,
    string SourceCivilizationId,
    int WidthMm,
    int HeightMm,
    RuinSnapshotEntry[] Entries);

public sealed record RuinSnapshotEntry(
    string BlueprintId,
    int BlueprintVersion,
    int X,
    int Y,
    long MaterialValue);

public sealed class RuinSnapshotException : Exception
{
    public RuinSnapshotException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// JSON round-trip and validation for offline ruin snapshots. Files are untrusted
/// data: size, count, coordinate and value limits are enforced before import.
/// </summary>
public static class RuinSnapshotSerializer
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxJsonCharacters = 1_000_000;
    public const int MaxEntries = 2_000;
    public const int MaxIdentifierLength = 64;
    public const long MaxMaterialValue = 1_000_000_000;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string Save(RuinSnapshotData snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Validate(snapshot);
        return JsonSerializer.Serialize(snapshot, Options);
    }

    public static RuinSnapshotData Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new RuinSnapshotException("EMPTY_SNAPSHOT", "Ruin snapshot is empty.");
        }

        if (json.Length > MaxJsonCharacters)
        {
            throw new RuinSnapshotException(
                "SNAPSHOT_TOO_LARGE",
                $"Ruin snapshot exceeds {MaxJsonCharacters} characters.");
        }

        RuinSnapshotData? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<RuinSnapshotData>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new RuinSnapshotException("INVALID_SNAPSHOT", exception.Message);
        }

        if (snapshot is null)
        {
            throw new RuinSnapshotException("INVALID_SNAPSHOT", "Ruin snapshot payload is null.");
        }

        Validate(snapshot);
        return snapshot;
    }

    /// <summary>Captures live structures of one civilization in one region.</summary>
    public static RuinSnapshotData Capture(
        WorldState world,
        string civilizationId,
        string regionId)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!world.Regions.TryGetValue(regionId, out var region))
        {
            throw new RuinSnapshotException(
                "UNKNOWN_REGION",
                $"Region '{regionId}' does not exist.");
        }

        var entries = new List<RuinSnapshotEntry>();
        foreach (var entity in region.Entities.Values)
        {
            if (!string.Equals(
                    entity.CivilizationId,
                    civilizationId,
                    StringComparison.Ordinal) ||
                entity.BlueprintId is null)
            {
                continue;
            }

            var blueprint = world.Workshop.GetVersion(
                entity.BlueprintId,
                entity.BlueprintVersion);
            if (blueprint is null || blueprint.MaterialCost <= 0)
            {
                continue;
            }

            entries.Add(new RuinSnapshotEntry(
                entity.BlueprintId,
                entity.BlueprintVersion,
                entity.Position.X,
                entity.Position.Y,
                blueprint.MaterialCost));
        }

        var snapshot = new RuinSnapshotData(
            CurrentSchemaVersion,
            civilizationId,
            region.WidthMm,
            region.HeightMm,
            entries.ToArray());
        Validate(snapshot);
        return snapshot;
    }

    private static void Validate(RuinSnapshotData snapshot)
    {
        if (snapshot.SchemaVersion != CurrentSchemaVersion)
        {
            throw new RuinSnapshotException(
                "UNSUPPORTED_SNAPSHOT_VERSION",
                $"Unsupported ruin snapshot schema {snapshot.SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.SourceCivilizationId) ||
            snapshot.SourceCivilizationId.Length > MaxIdentifierLength)
        {
            throw new RuinSnapshotException(
                "INVALID_SOURCE",
                "Source civilization id must be 1..64 characters.");
        }

        if (snapshot.WidthMm <= 0 || snapshot.HeightMm <= 0)
        {
            throw new RuinSnapshotException(
                "INVALID_DIMENSIONS",
                "Snapshot dimensions must be positive.");
        }

        if (snapshot.Entries is null || snapshot.Entries.Length > MaxEntries)
        {
            throw new RuinSnapshotException(
                "TOO_MANY_ENTRIES",
                $"Ruin snapshot entries must be <= {MaxEntries}.");
        }

        foreach (var entry in snapshot.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.BlueprintId) ||
                entry.BlueprintId.Length > MaxIdentifierLength ||
                entry.BlueprintVersion < 0 ||
                entry.X < 0 ||
                entry.Y < 0 ||
                entry.X > snapshot.WidthMm ||
                entry.Y > snapshot.HeightMm ||
                entry.MaterialValue < 0 ||
                entry.MaterialValue > MaxMaterialValue)
            {
                throw new RuinSnapshotException(
                    "INVALID_ENTRY",
                    $"Ruin snapshot entry '{entry.BlueprintId}' is invalid.");
            }
        }
    }
}
