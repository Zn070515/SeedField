namespace SeedField.Catalog;

/// <summary>
/// Derived catalog metadata. The JSON files remain authoritative; every entry can be
/// rebuilt by scanning the filesystem, and nothing here feeds the simulation.
/// </summary>
public sealed record ReplayCatalogEntry(
    string Path,
    string ContentSha256,
    int SchemaVersion,
    int TickCount,
    string FinalHash,
    long FileSize,
    DateTimeOffset LastWriteUtc,
    DateTimeOffset IndexedUtc,
    bool Missing = false);

public sealed record SaveCatalogEntry(
    string Path,
    string Kind,
    string ContentSha256,
    int FileSchemaVersion,
    int WorldSchemaVersion,
    long Tick,
    int ControllerCount,
    string? OpponentPackageId,
    long FileSize,
    DateTimeOffset LastWriteUtc,
    DateTimeOffset IndexedUtc,
    bool Missing = false);

public sealed record PackageCatalogEntry(
    string PackageId,
    string Version,
    string ContentSha256,
    string? SignerKeyId,
    string TrustLevel,
    string Path,
    DateTimeOffset IndexedUtc);

public sealed record BenchmarkCatalogEntry(
    long Id,
    string Kind,
    int Devices,
    int Ticks,
    double AvgTickMs,
    double ObsMs,
    double SerMs,
    double RunMs,
    double StepMs,
    double TicksPerSecond,
    double PeakWorkingSetMb,
    bool GcServer,
    int Dof,
    string? GitSha,
    DateTimeOffset CreatedUtc);

public sealed record CatalogCounts(
    int Replays,
    int Saves,
    int MissingReplays,
    int MissingSaves,
    int Benchmarks,
    int Packages);

public sealed record CatalogRebuildResult(
    int Replays,
    int Saves,
    int Skipped,
    int Missing,
    IReadOnlyList<string> Errors);
