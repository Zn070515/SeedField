using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SeedField.Simulation;

namespace SeedField.Catalog;

/// <summary>
/// Extracts lightweight index metadata from replay/save JSON. Files stay
/// authoritative; parsing failures are reported and skipped, never repaired here.
/// </summary>
public static class CatalogMetadata
{
    public static bool TryReadReplay(
        string path,
        string text,
        DateTimeOffset indexedUtc,
        out ReplayCatalogEntry entry,
        out string? error)
    {
        entry = null!;
        error = null;
        try
        {
            var replay = ReplaySerializer.Load(text);
            var file = new FileInfo(path);
            entry = new ReplayCatalogEntry(
                Path.GetFullPath(path),
                Sha256(text),
                replay.SchemaVersion,
                replay.Ticks.Length,
                replay.TickHashes.Length > 0 ? replay.TickHashes[^1] : string.Empty,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                indexedUtc);
            return true;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    public static bool TryReadSave(
        string path,
        string text,
        DateTimeOffset indexedUtc,
        out SaveCatalogEntry entry,
        out string? error)
    {
        entry = null!;
        error = null;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Save root is not an object.";
                return false;
            }

            string kind;
            int fileSchema;
            int worldSchema;
            long tick;
            var controllers = 0;
            string? packageId = null;

            if (root.TryGetProperty("world", out var world) &&
                world.ValueKind == JsonValueKind.Object)
            {
                kind = "game";
                fileSchema = ReadInt(root, "schemaVersion");
                worldSchema = ReadInt(world, "schemaVersion");
                tick = ReadLong(world, "tick");
                if (root.TryGetProperty("controllers", out var list) &&
                    list.ValueKind == JsonValueKind.Array)
                {
                    controllers = list.GetArrayLength();
                }

                packageId = ReadString(root, "opponentPackageId");
            }
            else if (root.TryGetProperty("regions", out _))
            {
                kind = "world";
                fileSchema = ReadInt(root, "schemaVersion");
                worldSchema = fileSchema;
                tick = ReadLong(root, "tick");
            }
            else
            {
                error = "JSON is neither a world save nor a game save.";
                return false;
            }

            if (fileSchema <= 0 || worldSchema <= 0 || tick < 0)
            {
                error = "Save schema/tick metadata is invalid.";
                return false;
            }

            var file = new FileInfo(path);
            entry = new SaveCatalogEntry(
                Path.GetFullPath(path),
                kind,
                Sha256(text),
                fileSchema,
                worldSchema,
                tick,
                controllers,
                packageId,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                indexedUtc);
            return true;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    public static bool LooksLikeReplay(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("tickHashes", out _);

    public static bool LooksLikeSave(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        (root.TryGetProperty("world", out _) || root.TryGetProperty("regions", out _));

    private static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            .ToLowerInvariant();

    private static int ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            !value.TryGetInt32(out var parsed))
        {
            throw new CatalogException(
                "INVALID_SAVE_METADATA",
                $"Save field '{property}' is missing or not an integer.");
        }

        return parsed;
    }

    private static long ReadLong(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            !value.TryGetInt64(out var parsed))
        {
            throw new CatalogException(
                "INVALID_SAVE_METADATA",
                $"Save field '{property}' is missing or not an integer.");
        }

        return parsed;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
