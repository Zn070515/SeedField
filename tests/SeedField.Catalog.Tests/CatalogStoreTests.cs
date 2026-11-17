using Microsoft.Data.Sqlite;
using SeedField.Catalog;
using SeedField.Simulation;

namespace SeedField.Catalog.Tests;

public sealed class CatalogStoreTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "seedfield-catalog-tests",
        Guid.NewGuid().ToString("N"));

    public CatalogStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    private string DatabasePath => Path.Combine(_root, "catalog.db");

    [Fact]
    public void UpsertsAndListsReplayMetadata()
    {
        using var store = new CatalogStore(DatabasePath);
        var entry = ReplayEntry("a.replay.json");

        store.UpsertReplay(entry);
        var replays = store.ListReplays();

        var actual = Assert.Single(replays);
        Assert.Equal(entry.Path, actual.Path);
        Assert.Equal(entry.FinalHash, actual.FinalHash);
        Assert.Equal(1, store.Counts().Replays);
    }

    [Fact]
    public void IndexerReadsReplayWorldAndGameSaves()
    {
        CreateReplayFile("a.replay.json");
        CreateWorldSaveFile("b.world.json");
        CreateGameSaveFile("c.game.json");
        using var store = new CatalogStore(DatabasePath);

        var result = CatalogIndexer.Rebuild(store, _root);

        Assert.Equal(1, result.Replays);
        Assert.Equal(2, result.Saves);
        Assert.Empty(result.Errors);
        Assert.Equal(3, store.Counts().Replays + store.Counts().Saves);
        Assert.Equal("game", Assert.Single(store.ListSaves(), save => save.Kind == "game").Kind);
        Assert.Equal("world", Assert.Single(store.ListSaves(), save => save.Kind == "world").Kind);
    }

    [Fact]
    public void MissingFilesAreFlaggedInsteadOfDeleted()
    {
        CreateReplayFile("a.replay.json");
        using var store = new CatalogStore(DatabasePath);
        CatalogIndexer.Rebuild(store, _root);
        Assert.Single(store.ListReplays());

        File.Delete(Path.Combine(_root, "a.replay.json"));
        var result = CatalogIndexer.Rebuild(store, _root);

        Assert.Empty(store.ListReplays());
        var missing = Assert.Single(store.ListReplays(includeMissing: true));
        Assert.True(missing.Missing);
        Assert.Equal(1, result.Missing);
    }

    [Fact]
    public void InvalidJsonIsSkippedWithoutAbortingTheScan()
    {
        File.WriteAllText(Path.Combine(_root, "bad.json"), "{ not json");
        File.WriteAllText(Path.Combine(_root, "other.json"), "{}");
        using var store = new CatalogStore(DatabasePath);

        var result = CatalogIndexer.Rebuild(store, _root);

        Assert.True(result.Skipped >= 2);
        Assert.Equal(0, store.Counts().Replays + store.Counts().Saves);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void BenchmarksRoundTripAndOrderNewestFirst()
    {
        using var store = new CatalogStore(DatabasePath);

        var first = store.RecordBenchmark(Benchmark(100, 7.5, createdAt: FixedNow));
        var second = store.RecordBenchmark(Benchmark(1000, 60.0, createdAt: FixedNow.AddSeconds(1)));

        Assert.True(second > first);
        var runs = store.ListBenchmarks();
        Assert.Equal(2, runs.Count);
        Assert.Equal(1000, runs[0].Devices);
        Assert.Equal(60.0, runs[0].AvgTickMs);
        Assert.Equal(2, store.Counts().Benchmarks);
    }

    [Fact]
    public async Task ConcurrentWritersRemainConsistent()
    {
        using var store = new CatalogStore(DatabasePath);
        var tasks = Enumerable.Range(0, 8)
            .Select(index => Task.Run(() =>
                store.RecordBenchmark(Benchmark(100 + index, 1.0 + index))))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(8, store.Counts().Benchmarks);
    }

    [Fact]
    public void CorruptDatabaseIsBackedUpAndRecreated()
    {
        using (var store = new CatalogStore(DatabasePath))
        {
            store.UpsertReplay(ReplayEntry("a.replay.json"));
        }

        File.WriteAllText(DatabasePath, "this is not a sqlite database");

        using var recovered = new CatalogStore(DatabasePath);

        Assert.NotNull(recovered.RecoveredBackupPath);
        Assert.True(File.Exists(recovered.RecoveredBackupPath));
        Assert.Equal("ok", recovered.IntegrityCheck());
        Assert.Equal(0, recovered.Counts().Replays);
    }

    [Fact]
    public void FutureCatalogSchemaIsRejected()
    {
        using (var store = new CatalogStore(DatabasePath))
        {
            store.UpsertReplay(ReplayEntry("a.replay.json"));
        }

        using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99";
            command.ExecuteNonQuery();
        }

        var exception = Assert.Throws<CatalogException>(
            () => new CatalogStore(DatabasePath));
        Assert.Equal("UNSUPPORTED_CATALOG_VERSION", exception.Code);
    }

    private static ReplayCatalogEntry ReplayEntry(string path) =>
        new(
            Path.GetFullPath(path),
            "abc123",
            1,
            20,
            "0xdeadbeef",
            1024,
            FixedNow.AddMinutes(-1),
            FixedNow);

    private static BenchmarkCatalogEntry Benchmark(
        int devices,
        double avgTickMs,
        DateTimeOffset? createdAt = null) =>
        new(
            0,
            "jint-parallel",
            devices,
            10,
            avgTickMs,
            1.0,
            2.0,
            3.0,
            0.5,
            1000.0 / avgTickMs,
            128.0,
            true,
            16,
            "test",
            createdAt ?? FixedNow);

    private void CreateReplayFile(string name)
    {
        var world = SimpleWorld();
        var data = new ReplayData(
            1,
            WorldSaveSerializer.Save(world),
            Array.Empty<ReplayTickData>(),
            Array.Empty<string>());
        File.WriteAllText(
            Path.Combine(_root, name),
            ReplaySerializer.Save(data));
    }

    private void CreateWorldSaveFile(string name) =>
        File.WriteAllText(
            Path.Combine(_root, name),
            WorldSaveSerializer.Save(SimpleWorld()));

    private void CreateGameSaveFile(string name) =>
        File.WriteAllText(
            Path.Combine(_root, name),
            SeedField.Runtime.Js.GameSaveSerializer.Save(
                SimpleWorld(),
                Array.Empty<SeedField.Runtime.Js.ControllerSlot>()));

    private static WorldState SimpleWorld() =>
        new WorldBuilder().AddRegion("r0", 1_000, 1_000).Build();

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; SQLite may still be releasing a handle.
        }
    }
}
