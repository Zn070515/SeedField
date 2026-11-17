using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SeedField.Catalog;

/// <summary>
/// Local SQLite catalog. It stores only derived metadata and benchmark history;
/// JSON files remain authoritative and the catalog can be deleted and rebuilt.
/// Connections are opened per operation so readers/writers can overlap under WAL.
/// </summary>
public sealed class CatalogStore : IDisposable
{
    private readonly bool _readOnly;

    public CatalogStore(string databasePath, bool readOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);
        _readOnly = readOnly;

        if (!readOnly)
        {
            var directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        if (_readOnly)
        {
            using var readConnection = OpenConnection();
            var version = CatalogMigrations.GetVersion(readConnection);
            if (version != CatalogMigrations.CurrentVersion)
            {
                throw new CatalogException(
                    "CATALOG_READ_ONLY_MIGRATION_REQUIRED",
                    $"Read-only catalog is schema {version}; expected " +
                    $"{CatalogMigrations.CurrentVersion}.");
            }

            return;
        }

        if (TryRecoverCorruptDatabase())
        {
            // Recreated below.
        }

        using var connection = OpenConnection();
        if (!IsIntegrityOk(connection))
        {
            throw new CatalogException(
                "CATALOG_CORRUPT",
                $"Catalog '{DatabasePath}' failed integrity_check.");
        }

        CatalogMigrations.Apply(connection);
    }

    public string DatabasePath { get; }

    public string? RecoveredBackupPath { get; private set; }

    public void Dispose()
    {
        // Connections are per-operation; nothing persistent to release.
    }

    public void UpsertReplay(ReplayCatalogEntry entry)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        UpsertReplay(connection, transaction, entry);
        transaction.Commit();
    }

    public void UpsertReplays(IEnumerable<ReplayCatalogEntry> entries)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var entry in entries)
        {
            UpsertReplay(connection, transaction, entry);
        }

        transaction.Commit();
    }

    public void UpsertSave(SaveCatalogEntry entry)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        UpsertSave(connection, transaction, entry);
        transaction.Commit();
    }

    public void UpsertSaves(IEnumerable<SaveCatalogEntry> entries)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var entry in entries)
        {
            UpsertSave(connection, transaction, entry);
        }

        transaction.Commit();
    }

    public void UpsertPackage(PackageCatalogEntry entry)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO packages
                (package_id, version, content_sha256, signer_key_id, trust_level, path, indexed_utc)
            VALUES
                ($packageId, $version, $hash, $signer, $trust, $path, $indexed)
            ON CONFLICT(package_id, version) DO UPDATE SET
                content_sha256 = excluded.content_sha256,
                signer_key_id = excluded.signer_key_id,
                trust_level = excluded.trust_level,
                path = excluded.path,
                indexed_utc = excluded.indexed_utc;
            """;
        command.Parameters.AddWithValue("$packageId", entry.PackageId);
        command.Parameters.AddWithValue("$version", entry.Version);
        command.Parameters.AddWithValue("$hash", entry.ContentSha256);
        command.Parameters.AddWithValue("$signer", (object?)entry.SignerKeyId ?? DBNull.Value);
        command.Parameters.AddWithValue("$trust", entry.TrustLevel);
        command.Parameters.AddWithValue("$path", entry.Path);
        command.Parameters.AddWithValue("$indexed", Format(entry.IndexedUtc));
        command.ExecuteNonQuery();
    }

    public long RecordBenchmark(BenchmarkCatalogEntry entry)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO benchmark_runs
                (kind, devices, ticks, avg_tick_ms, obs_ms, ser_ms, run_ms, step_ms,
                 ticks_per_second, peak_working_set_mb, gc_server, dof, git_sha, created_utc)
            VALUES
                ($kind, $devices, $ticks, $avg, $obs, $ser, $run, $step,
                 $tps, $memory, $gc, $dof, $git, $created);
            """;
        command.Parameters.AddWithValue("$kind", entry.Kind);
        command.Parameters.AddWithValue("$devices", entry.Devices);
        command.Parameters.AddWithValue("$ticks", entry.Ticks);
        command.Parameters.AddWithValue("$avg", entry.AvgTickMs);
        command.Parameters.AddWithValue("$obs", entry.ObsMs);
        command.Parameters.AddWithValue("$ser", entry.SerMs);
        command.Parameters.AddWithValue("$run", entry.RunMs);
        command.Parameters.AddWithValue("$step", entry.StepMs);
        command.Parameters.AddWithValue("$tps", entry.TicksPerSecond);
        command.Parameters.AddWithValue("$memory", entry.PeakWorkingSetMb);
        command.Parameters.AddWithValue("$gc", entry.GcServer ? 1 : 0);
        command.Parameters.AddWithValue("$dof", entry.Dof);
        command.Parameters.AddWithValue("$git", (object?)entry.GitSha ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", Format(entry.CreatedUtc));
        command.ExecuteNonQuery();

        using var id = connection.CreateCommand();
        id.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(id.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Replaces the filesystem-derived replay/save index in one transaction. Entries
    /// not seen in the scan remain as <c>missing = 1</c> instead of being deleted.
    /// </summary>
    public void ReplaceFileIndex(
        IReadOnlyList<ReplayCatalogEntry> replays,
        IReadOnlyList<SaveCatalogEntry> saves)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var mark = connection.CreateCommand())
        {
            mark.Transaction = transaction;
            mark.CommandText = "UPDATE replays SET missing = 1;";
            mark.ExecuteNonQuery();
            mark.CommandText = "UPDATE saves SET missing = 1;";
            mark.ExecuteNonQuery();
        }

        foreach (var replay in replays)
        {
            UpsertReplay(connection, transaction, replay with { Missing = false });
        }

        foreach (var save in saves)
        {
            UpsertSave(connection, transaction, save with { Missing = false });
        }

        transaction.Commit();
    }

    public IReadOnlyList<ReplayCatalogEntry> ListReplays(
        int limit = 100,
        bool includeMissing = false)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT path, content_sha256, schema_version, tick_count, final_hash,
                   file_size, last_write_utc, indexed_utc, missing
            FROM replays
            {(includeMissing ? string.Empty : "WHERE missing = 0")}
            ORDER BY last_write_utc DESC, path
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        using var reader = command.ExecuteReader();
        var results = new List<ReplayCatalogEntry>();
        while (reader.Read())
        {
            results.Add(new ReplayCatalogEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetInt64(5),
                Parse(reader.GetString(6)),
                Parse(reader.GetString(7)),
                reader.GetInt32(8) != 0));
        }

        return results;
    }

    public IReadOnlyList<SaveCatalogEntry> ListSaves(
        int limit = 100,
        bool includeMissing = false)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT path, kind, content_sha256, file_schema_version,
                   world_schema_version, tick, controller_count, opponent_package_id,
                   file_size, last_write_utc, indexed_utc, missing
            FROM saves
            {(includeMissing ? string.Empty : "WHERE missing = 0")}
            ORDER BY last_write_utc DESC, path
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        using var reader = command.ExecuteReader();
        var results = new List<SaveCatalogEntry>();
        while (reader.Read())
        {
            results.Add(new SaveCatalogEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt64(5),
                reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetInt64(8),
                Parse(reader.GetString(9)),
                Parse(reader.GetString(10)),
                reader.GetInt32(11) != 0));
        }

        return results;
    }

    public IReadOnlyList<PackageCatalogEntry> ListPackages(int limit = 100)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT package_id, version, content_sha256, signer_key_id, trust_level, path, indexed_utc
            FROM packages
            ORDER BY indexed_utc DESC, package_id, version
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        using var reader = command.ExecuteReader();
        var results = new List<PackageCatalogEntry>();
        while (reader.Read())
        {
            results.Add(new PackageCatalogEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                Parse(reader.GetString(6))));
        }

        return results;
    }

    public IReadOnlyList<BenchmarkCatalogEntry> ListBenchmarks(int limit = 50)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, kind, devices, ticks, avg_tick_ms, obs_ms, ser_ms, run_ms,
                   step_ms, ticks_per_second, peak_working_set_mb, gc_server, dof,
                   git_sha, created_utc
            FROM benchmark_runs
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        using var reader = command.ExecuteReader();
        var results = new List<BenchmarkCatalogEntry>();
        while (reader.Read())
        {
            results.Add(new BenchmarkCatalogEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetDouble(6),
                reader.GetDouble(7),
                reader.GetDouble(8),
                reader.GetDouble(9),
                reader.GetDouble(10),
                reader.GetInt32(11) != 0,
                reader.GetInt32(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                Parse(reader.GetString(14))));
        }

        return results;
    }

    public CatalogCounts Counts()
    {
        using var connection = OpenConnection();
        return new CatalogCounts(
            Scalar(connection, "SELECT COUNT(*) FROM replays WHERE missing = 0"),
            Scalar(connection, "SELECT COUNT(*) FROM saves WHERE missing = 0"),
            Scalar(connection, "SELECT COUNT(*) FROM replays WHERE missing = 1"),
            Scalar(connection, "SELECT COUNT(*) FROM saves WHERE missing = 1"),
            Scalar(connection, "SELECT COUNT(*) FROM benchmark_runs"),
            Scalar(connection, "SELECT COUNT(*) FROM packages"));
    }

    public string IntegrityCheck()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)
               ?? string.Empty;
    }

    internal SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = _readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            // Catalog operations are not hot. Disabling pooling lets corruption
            // recovery move/delete the database without lingering Windows handles.
            Pooling = false,
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA busy_timeout = 5000;";
            command.ExecuteNonQuery();
        }

        if (!_readOnly)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
            command.ExecuteNonQuery();
        }

        return connection;
    }

    internal static void UpsertReplay(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ReplayCatalogEntry entry)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO replays
                (path, content_sha256, schema_version, tick_count, final_hash,
                 file_size, last_write_utc, indexed_utc, missing)
            VALUES
                ($path, $hash, $schema, $ticks, $final, $size, $write, $indexed, $missing)
            ON CONFLICT(path) DO UPDATE SET
                content_sha256 = excluded.content_sha256,
                schema_version = excluded.schema_version,
                tick_count = excluded.tick_count,
                final_hash = excluded.final_hash,
                file_size = excluded.file_size,
                last_write_utc = excluded.last_write_utc,
                indexed_utc = excluded.indexed_utc,
                missing = excluded.missing;
            """;
        Add(command, "$path", entry.Path);
        Add(command, "$hash", entry.ContentSha256);
        Add(command, "$schema", entry.SchemaVersion);
        Add(command, "$ticks", entry.TickCount);
        Add(command, "$final", entry.FinalHash);
        Add(command, "$size", entry.FileSize);
        Add(command, "$write", Format(entry.LastWriteUtc));
        Add(command, "$indexed", Format(entry.IndexedUtc));
        Add(command, "$missing", entry.Missing ? 1 : 0);
        command.ExecuteNonQuery();
    }

    internal static void UpsertSave(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SaveCatalogEntry entry)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO saves
                (path, kind, content_sha256, file_schema_version, world_schema_version,
                 tick, controller_count, opponent_package_id, file_size,
                 last_write_utc, indexed_utc, missing)
            VALUES
                ($path, $kind, $hash, $fileSchema, $worldSchema, $tick, $controllers,
                 $package, $size, $write, $indexed, $missing)
            ON CONFLICT(path) DO UPDATE SET
                kind = excluded.kind,
                content_sha256 = excluded.content_sha256,
                file_schema_version = excluded.file_schema_version,
                world_schema_version = excluded.world_schema_version,
                tick = excluded.tick,
                controller_count = excluded.controller_count,
                opponent_package_id = excluded.opponent_package_id,
                file_size = excluded.file_size,
                last_write_utc = excluded.last_write_utc,
                indexed_utc = excluded.indexed_utc,
                missing = excluded.missing;
            """;
        Add(command, "$path", entry.Path);
        Add(command, "$kind", entry.Kind);
        Add(command, "$hash", entry.ContentSha256);
        Add(command, "$fileSchema", entry.FileSchemaVersion);
        Add(command, "$worldSchema", entry.WorldSchemaVersion);
        Add(command, "$tick", entry.Tick);
        Add(command, "$controllers", entry.ControllerCount);
        Add(command, "$package", (object?)entry.OpponentPackageId ?? DBNull.Value);
        Add(command, "$size", entry.FileSize);
        Add(command, "$write", Format(entry.LastWriteUtc));
        Add(command, "$indexed", Format(entry.IndexedUtc));
        Add(command, "$missing", entry.Missing ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private static void Add(SqliteCommand command, string name, object value) =>
        command.Parameters.AddWithValue(name, value);

    private static int Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private bool TryRecoverCorruptDatabase()
    {
        if (_readOnly || !File.Exists(DatabasePath))
        {
            return false;
        }

        // Avoid opening a non-SQLite file: the failed open can leave a transient
        // Windows handle that blocks moving the file during recovery.
        if (!HasSqliteHeader(DatabasePath))
        {
            return MoveCorruptDatabase();
        }

        try
        {
            using (var connection = OpenConnection())
            {
                if (IsIntegrityOk(connection))
                {
                    return false;
                }
            }

            return MoveCorruptDatabase();
        }
        catch (SqliteException)
        {
            // The connection is out of scope here; the file can be moved.
            return MoveCorruptDatabase();
        }
    }

    private bool MoveCorruptDatabase()
    {
        SqliteConnection.ClearAllPools();
        var backup = $"{DatabasePath}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        MoveWithRetry(DatabasePath, backup, overwrite: false);
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecar = DatabasePath + suffix;
            if (File.Exists(sidecar))
            {
                MoveWithRetry(sidecar, backup + suffix, overwrite: true);
            }
        }

        RecoveredBackupPath = backup;
        return true;
    }

    private static void MoveWithRetry(string source, string destination, bool overwrite)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                // Windows may briefly hold the SQLite file after a failed open.
                Thread.Sleep(25);
            }
        }
    }

    private static bool HasSqliteHeader(string path)
    {
        try
        {
            Span<byte> header = stackalloc byte[16];
            using var stream = File.OpenRead(path);
            if (stream.Read(header) != header.Length)
            {
                return false;
            }

            return header.SequenceEqual("SQLite format 3\0"u8);
        }
        catch (IOException)
        {
            // Another connection/process may hold a read lock; let SQLite open it
            // and report the real problem instead of treating it as corruption.
            return true;
        }
    }

    private static bool IsIntegrityOk(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        return string.Equals(
            Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture),
            "ok",
            StringComparison.Ordinal);
    }

    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
