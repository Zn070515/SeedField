using System.Security.Cryptography;
using System.Text;

namespace SeedField.Runtime.Js;

public sealed record OpponentPackageManifest(
    int SchemaVersion,
    string PackageId,
    string Version,
    string Kind,
    string EngineCompat,
    string[] EntryPoints,
    string[] RequiredCapabilities,
    string ContentHash);

public sealed class OpponentPackageValidationException : Exception
{
    public OpponentPackageValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// Trusted built-in opponent package. This validates metadata and entry points, but it
/// is not a file importer: arbitrary third-party programs still require OS isolation.
/// </summary>
public sealed class OpponentPackage
{
    public const int CurrentSchemaVersion = 1;
    public const string CurrentEngineCompat = "0.1";
    public const int MaxProgramBytes = 256 * 1024;
    public const int MaxTotalBytes = 1024 * 1024;
    public const int MaxProgramFiles = 32;

    /// <summary>
    /// Hard gate: arbitrary third-party executable packages stay closed until the
    /// process-isolation, IPC and resource-limit gates in ADR-0020 are complete.
    /// </summary>
    public const bool ThirdPartyImportEnabled = false;

    private OpponentPackage(
        OpponentPackageManifest manifest,
        IReadOnlyDictionary<string, string> programs)
    {
        Manifest = manifest;
        Programs = programs;
    }

    public OpponentPackageManifest Manifest { get; }

    public IReadOnlyDictionary<string, string> Programs { get; }

    public string ProgramSource => Programs[Manifest.EntryPoints[0]];

    public static OpponentPackage Create(
        OpponentPackageManifest manifest,
        IReadOnlyDictionary<string, string> programs)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(programs);

        if (manifest.SchemaVersion != CurrentSchemaVersion)
        {
            throw new OpponentPackageValidationException(
                "UNSUPPORTED_PACKAGE_SCHEMA",
                $"Opponent package schema {manifest.SchemaVersion} is not supported.");
        }

        if (string.IsNullOrWhiteSpace(manifest.PackageId) ||
            string.IsNullOrWhiteSpace(manifest.Version) ||
            string.IsNullOrWhiteSpace(manifest.Kind) ||
            string.IsNullOrWhiteSpace(manifest.EngineCompat))
        {
            throw new OpponentPackageValidationException(
                "INVALID_MANIFEST",
                "Package id, version, kind and engine compatibility are required.");
        }

        if (!string.Equals(manifest.Kind, "offline-opponent", StringComparison.Ordinal))
        {
            throw new OpponentPackageValidationException(
                "INVALID_KIND",
                $"Package kind '{manifest.Kind}' is not an offline opponent.");
        }

        if (!string.Equals(manifest.EngineCompat, CurrentEngineCompat, StringComparison.Ordinal))
        {
            throw new OpponentPackageValidationException(
                "UNSUPPORTED_API",
                $"Package requires engine API '{manifest.EngineCompat}', expected '{CurrentEngineCompat}'.");
        }

        if (manifest.EntryPoints.Length is < 1 or > 8)
        {
            throw new OpponentPackageValidationException(
                "INVALID_ENTRYPOINTS",
                "An opponent package needs 1-8 entry points.");
        }

        if (programs.Count > MaxProgramFiles)
        {
            throw new OpponentPackageValidationException(
                "TOO_MANY_FILES",
                $"Package contains {programs.Count} programs; limit is {MaxProgramFiles}.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        long totalBytes = 0;

        foreach (var entryPoint in manifest.EntryPoints)
        {
            if (!IsSafeRelativePath(entryPoint))
            {
                throw new OpponentPackageValidationException(
                    "UNSAFE_PATH",
                    $"Entry point '{entryPoint}' is not a safe relative path.");
            }

            if (!seen.Add(entryPoint))
            {
                throw new OpponentPackageValidationException(
                    "DUPLICATE_ENTRYPOINT",
                    $"Entry point '{entryPoint}' appears twice.");
            }

            if (!programs.TryGetValue(entryPoint, out var source))
            {
                throw new OpponentPackageValidationException(
                    "MISSING_ENTRY",
                    $"Entry point '{entryPoint}' has no program source.");
            }

            var bytes = Encoding.UTF8.GetByteCount(source);
            if (bytes > MaxProgramBytes)
            {
                throw new OpponentPackageValidationException(
                    "PACKAGE_TOO_LARGE",
                    $"Program '{entryPoint}' exceeds {MaxProgramBytes} bytes.");
            }

            totalBytes += bytes;
        }

        if (totalBytes > MaxTotalBytes)
        {
            throw new OpponentPackageValidationException(
                "PACKAGE_TOO_LARGE",
                $"Package programs exceed {MaxTotalBytes} bytes.");
        }

        var contentHash = ComputeContentHash(programs);
        var validated = manifest with { ContentHash = contentHash };
        return new OpponentPackage(validated, programs);
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains('\\') ||
            path.Contains(':'))
        {
            return false;
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                return false;
            }
        }

        return true;
    }

    private static string ComputeContentHash(IReadOnlyDictionary<string, string> programs)
    {
        var builder = new StringBuilder();
        foreach (var pair in programs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key);
            builder.Append('\0');
            builder.Append(pair.Value);
            builder.Append('\0');
        }

        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }
}
