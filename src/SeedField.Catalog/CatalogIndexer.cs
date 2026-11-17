using System.Text.Json;

namespace SeedField.Catalog;

/// <summary>
/// Scans JSON files under a root and replaces the catalog's file-derived rows.
/// Missing files are flagged, not deleted; invalid files are skipped with an error.
/// </summary>
public static class CatalogIndexer
{
    public static CatalogRebuildResult Rebuild(CatalogStore store, string root)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Catalog root '{root}' does not exist.");
        }

        var replays = new List<ReplayCatalogEntry>();
        var saves = new List<SaveCatalogEntry>();
        var errors = new List<string>();
        var skipped = 0;
        var indexedUtc = DateTimeOffset.UtcNow;

        foreach (var path in Directory.EnumerateFiles(
                     root,
                     "*.json",
                     SearchOption.AllDirectories))
        {
            try
            {
                var text = File.ReadAllText(path);
                using var document = JsonDocument.Parse(text);
                var rootElement = document.RootElement;

                if (CatalogMetadata.LooksLikeReplay(rootElement))
                {
                    if (CatalogMetadata.TryReadReplay(
                            path,
                            text,
                            indexedUtc,
                            out var replay,
                            out var replayError))
                    {
                        replays.Add(replay);
                    }
                    else
                    {
                        skipped++;
                        errors.Add($"{path}: {replayError}");
                    }
                }
                else if (CatalogMetadata.LooksLikeSave(rootElement))
                {
                    if (CatalogMetadata.TryReadSave(
                            path,
                            text,
                            indexedUtc,
                            out var save,
                            out var saveError))
                    {
                        saves.Add(save);
                    }
                    else
                    {
                        skipped++;
                        errors.Add($"{path}: {saveError}");
                    }
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception exception)
            {
                skipped++;
                errors.Add($"{path}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        store.ReplaceFileIndex(replays, saves);
        var counts = store.Counts();
        return new CatalogRebuildResult(
            replays.Count,
            saves.Count,
            skipped,
            counts.MissingReplays + counts.MissingSaves,
            errors);
    }
}
