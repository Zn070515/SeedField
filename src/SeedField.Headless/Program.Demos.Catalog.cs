using SeedField.Catalog;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunCatalogRebuild(string root, string? catalogDb)
    {
        var database = CatalogDatabase(catalogDb);
        try
        {
            using var store = new CatalogStore(database);
            var result = CatalogIndexer.Rebuild(store, root);
            Console.WriteLine(
                $"CATALOG_REBUILD db={database} replays={result.Replays} " +
                $"saves={result.Saves} skipped={result.Skipped} " +
                $"missing={result.Missing} errors={result.Errors.Count}");
            foreach (var error in result.Errors.Take(5))
            {
                Console.Error.WriteLine($"CATALOG_SKIPPED {error}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"CATALOG_REBUILD_FAILED {exception.Message}");
            return 2;
        }
    }

    private static int RunCatalogList(string? catalogDb)
    {
        var database = CatalogDatabase(catalogDb);
        try
        {
            using var store = new CatalogStore(database);
            var counts = store.Counts();
            Console.WriteLine(
                $"CATALOG db={database} replays={counts.Replays} saves={counts.Saves} " +
                $"missingReplays={counts.MissingReplays} missingSaves={counts.MissingSaves} " +
                $"benchmarks={counts.Benchmarks} packages={counts.Packages}");

            foreach (var replay in store.ListReplays(limit: 10))
            {
                Console.WriteLine(
                    $"REPLAY path={replay.Path} ticks={replay.TickCount} " +
                    $"hash={replay.FinalHash} size={replay.FileSize}");
            }

            foreach (var save in store.ListSaves(limit: 10))
            {
                Console.WriteLine(
                    $"SAVE kind={save.Kind} path={save.Path} tick={save.Tick} " +
                    $"worldSchema={save.WorldSchemaVersion} " +
                    $"controllers={save.ControllerCount}");
            }

            foreach (var benchmark in store.ListBenchmarks(limit: 10))
            {
                Console.WriteLine(
                    $"BENCH id={benchmark.Id} kind={benchmark.Kind} " +
                    $"devices={benchmark.Devices} avgMs={benchmark.AvgTickMs:0.00} " +
                    $"ticks/s={benchmark.TicksPerSecond:0.0} " +
                    $"dof={benchmark.Dof} gcServer={benchmark.GcServer}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"CATALOG_LIST_FAILED {exception.Message}");
            return 2;
        }
    }

    private static int RunCatalogCheck(string? catalogDb)
    {
        var database = CatalogDatabase(catalogDb);
        try
        {
            using var store = new CatalogStore(database);
            var integrity = store.IntegrityCheck();
            var counts = store.Counts();
            Console.WriteLine(
                $"CATALOG_CHECK db={database} integrity={integrity} " +
                $"replays={counts.Replays} saves={counts.Saves} " +
                $"missing={counts.MissingReplays + counts.MissingSaves} " +
                $"benchmarks={counts.Benchmarks}");
            return string.Equals(integrity, "ok", StringComparison.Ordinal) ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"CATALOG_CHECK_FAILED {exception.Message}");
            return 2;
        }
    }

    private static string CatalogDatabase(string? explicitPath) =>
        explicitPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SeedField",
            "catalog.db");
}
