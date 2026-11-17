using Microsoft.Data.Sqlite;

namespace SeedField.Catalog;

/// <summary>
/// Explicit schema migrations using SQLite's <c>user_version</c>. A future version
/// is rejected instead of being silently recreated: the database is a derived cache,
/// but silently discarding a newer catalog would hide a downgrade mistake.
/// </summary>
internal static class CatalogMigrations
{
    public const int CurrentVersion = 1;

    private static readonly string[][] Steps =
    {
        new[]
        {
            """
            CREATE TABLE IF NOT EXISTS replays (
                path TEXT PRIMARY KEY COLLATE NOCASE,
                content_sha256 TEXT NOT NULL,
                schema_version INTEGER NOT NULL,
                tick_count INTEGER NOT NULL,
                final_hash TEXT NOT NULL,
                file_size INTEGER NOT NULL,
                last_write_utc TEXT NOT NULL,
                indexed_utc TEXT NOT NULL,
                missing INTEGER NOT NULL DEFAULT 0
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS saves (
                path TEXT PRIMARY KEY COLLATE NOCASE,
                kind TEXT NOT NULL,
                content_sha256 TEXT NOT NULL,
                file_schema_version INTEGER NOT NULL,
                world_schema_version INTEGER NOT NULL,
                tick INTEGER NOT NULL,
                controller_count INTEGER NOT NULL,
                opponent_package_id TEXT NULL,
                file_size INTEGER NOT NULL,
                last_write_utc TEXT NOT NULL,
                indexed_utc TEXT NOT NULL,
                missing INTEGER NOT NULL DEFAULT 0
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS packages (
                package_id TEXT NOT NULL,
                version TEXT NOT NULL,
                content_sha256 TEXT NOT NULL,
                signer_key_id TEXT NULL,
                trust_level TEXT NOT NULL,
                path TEXT NOT NULL,
                indexed_utc TEXT NOT NULL,
                PRIMARY KEY (package_id, version)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS benchmark_runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                kind TEXT NOT NULL,
                devices INTEGER NOT NULL,
                ticks INTEGER NOT NULL,
                avg_tick_ms REAL NOT NULL,
                obs_ms REAL NOT NULL,
                ser_ms REAL NOT NULL,
                run_ms REAL NOT NULL,
                step_ms REAL NOT NULL,
                ticks_per_second REAL NOT NULL,
                peak_working_set_mb REAL NOT NULL,
                gc_server INTEGER NOT NULL,
                dof INTEGER NOT NULL,
                git_sha TEXT NULL,
                created_utc TEXT NOT NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS ix_replays_hash ON replays(final_hash);",
            "CREATE INDEX IF NOT EXISTS ix_replays_missing ON replays(missing);",
            "CREATE INDEX IF NOT EXISTS ix_saves_tick ON saves(tick);",
            "CREATE INDEX IF NOT EXISTS ix_saves_missing ON saves(missing);",
            "CREATE INDEX IF NOT EXISTS ix_benchmarks_created ON benchmark_runs(created_utc);",
        },
    };

    public static void Apply(SqliteConnection connection)
    {
        var version = GetVersion(connection);
        if (version > CurrentVersion)
        {
            throw new CatalogException(
                "UNSUPPORTED_CATALOG_VERSION",
                $"Catalog schema {version} is newer than supported {CurrentVersion}.");
        }

        for (var index = version; index < CurrentVersion; index++)
        {
            using var transaction = connection.BeginTransaction();
            foreach (var statement in Steps[index])
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }

            using (var setVersion = connection.CreateCommand())
            {
                setVersion.Transaction = transaction;
                setVersion.CommandText = $"PRAGMA user_version = {index + 1}";
                setVersion.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    public static int GetVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
