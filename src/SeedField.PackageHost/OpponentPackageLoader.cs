using System.Text;
using System.Text.Json;
using SeedField.Runtime.Js;

namespace SeedField.PackageHost;

/// <summary>
/// Host-only loader for a local package directory. It validates paths, sizes, content
/// hash, signature and explicit consent; it never executes the package. The global
/// ThirdPartyImportEnabled gate remains false.
/// </summary>
public static class OpponentPackageLoader
{
    public const string ManifestFileName = "manifest.json";
    public const int MaxManifestBytes = 64 * 1024;

    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        MaxDepth = 16,
    };

    public static LoadedOpponentPackage Load(
        string directory,
        PackageTrustStore trustStore,
        PackageImportConsent consent)
    {
        ArgumentNullException.ThrowIfNull(trustStore);
        ArgumentNullException.ThrowIfNull(consent);

        if (!consent.UserConfirmed)
        {
            throw new PackageHostException(
                "CONSENT_REQUIRED",
                "Package import requires explicit user confirmation.");
        }

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new PackageHostException(
                "DIRECTORY_MISSING",
                $"Package directory '{directory}' does not exist.");
        }

        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new PackageHostException(
                "MANIFEST_MISSING",
                $"Package is missing {ManifestFileName}.");
        }

        if (new FileInfo(manifestPath).Length > MaxManifestBytes)
        {
            throw new PackageHostException(
                "MANIFEST_TOO_LARGE",
                $"Manifest exceeds {MaxManifestBytes} bytes.");
        }

        var manifest = ParseManifest(ReadUtf8(manifestPath));
        ValidateEntryPoints(manifest);

        var programs = ReadPrograms(directory, manifest);
        var actualHash = OpponentPackage.ComputeContentHash(programs);
        if (!string.Equals(
                manifest.ContentHash,
                actualHash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new PackageHostException(
                "CONTENT_HASH_MISMATCH",
                "Package content does not match its declared content hash.");
        }

        PackageTrustLevel trust;
        if (!string.IsNullOrWhiteSpace(manifest.Signature))
        {
            if (string.IsNullOrWhiteSpace(manifest.SignerKeyId))
            {
                throw new PackageHostException(
                    "INVALID_SIGNATURE",
                    "Signed package is missing signerKeyId.");
            }

            if (!trustStore.TryGet(manifest.SignerKeyId, out var publicKey))
            {
                throw new PackageHostException(
                    "UNTRUSTED_SIGNER",
                    $"Signer '{manifest.SignerKeyId}' is not in the trust store.");
            }

            if (!PackageSignature.Verify(manifest, publicKey))
            {
                throw new PackageHostException(
                    "INVALID_SIGNATURE",
                    "Package signature is invalid.");
            }

            trust = PackageTrustLevel.Signed;
        }
        else
        {
            if (!consent.AllowUnsigned)
            {
                throw new PackageHostException(
                    "UNSIGNED_PACKAGE",
                    "Unsigned packages require explicit AllowUnsigned consent.");
            }

            trust = PackageTrustLevel.Unsigned;
        }

        OpponentPackage package;
        try
        {
            package = OpponentPackage.Create(manifest, programs);
        }
        catch (OpponentPackageValidationException exception)
        {
            throw new PackageHostException(exception.Code, exception.Message);
        }

        return new LoadedOpponentPackage(package, trust, manifest.SignerKeyId);
    }

    private static OpponentPackageManifest ParseManifest(string json)
    {
        OpponentPackageManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<OpponentPackageManifest>(
                json,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new PackageHostException("INVALID_MANIFEST", exception.Message);
        }

        if (manifest is null ||
            manifest.EntryPoints is null ||
            manifest.RequiredCapabilities is null)
        {
            throw new PackageHostException(
                "INVALID_MANIFEST",
                "Manifest payload is incomplete.");
        }

        return manifest;
    }

    private static void ValidateEntryPoints(OpponentPackageManifest manifest)
    {
        if (manifest.EntryPoints.Length is < 1 or > OpponentPackage.MaxProgramFiles)
        {
            throw new PackageHostException(
                "INVALID_ENTRYPOINTS",
                $"Package needs 1..{OpponentPackage.MaxProgramFiles} entry points.");
        }

        foreach (var entryPoint in manifest.EntryPoints)
        {
            if (!OpponentPackage.IsSafeRelativePath(entryPoint))
            {
                throw new PackageHostException(
                    "UNSAFE_PATH",
                    $"Entry point '{entryPoint}' is not a safe relative path.");
            }

            if (!entryPoint.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            {
                throw new PackageHostException(
                    "UNSUPPORTED_ENTRY_TYPE",
                    $"Entry point '{entryPoint}' must be a JavaScript file.");
            }
        }
    }

    private static Dictionary<string, string> ReadPrograms(
        string directory,
        OpponentPackageManifest manifest)
    {
        var entryPoints = new HashSet<string>(
            manifest.EntryPoints,
            StringComparer.Ordinal);
        var programs = new Dictionary<string, string>(StringComparer.Ordinal);
        var fileCount = 0;

        foreach (var directoryPath in Directory.EnumerateDirectories(
                     directory,
                     "*",
                     SearchOption.AllDirectories))
        {
            if (IsReparsePoint(directoryPath))
            {
                throw new PackageHostException(
                    "REPARSE_POINT",
                    $"Package directory '{directoryPath}' is a reparse point.");
            }
        }

        foreach (var path in Directory.EnumerateFiles(
                     directory,
                     "*",
                     SearchOption.AllDirectories))
        {
            if (IsReparsePoint(path))
            {
                throw new PackageHostException(
                    "REPARSE_POINT",
                    $"Package file '{path}' is a reparse point.");
            }

            var relative = Path.GetRelativePath(directory, path)
                .Replace('\\', '/');
            if (string.Equals(relative, ManifestFileName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!entryPoints.Contains(relative))
            {
                throw new PackageHostException(
                    "UNEXPECTED_FILE",
                    $"Package contains unexpected file '{relative}'.");
            }

            fileCount++;
            if (fileCount > OpponentPackage.MaxProgramFiles)
            {
                throw new PackageHostException(
                    "TOO_MANY_FILES",
                    $"Package exceeds {OpponentPackage.MaxProgramFiles} program files.");
            }

            var info = new FileInfo(path);
            if (info.Length > OpponentPackage.MaxProgramBytes)
            {
                throw new PackageHostException(
                    "PACKAGE_TOO_LARGE",
                    $"Program '{relative}' exceeds {OpponentPackage.MaxProgramBytes} bytes.");
            }

            programs.Add(relative, ReadUtf8(path));
        }

        foreach (var entryPoint in manifest.EntryPoints)
        {
            if (!programs.ContainsKey(entryPoint))
            {
                throw new PackageHostException(
                    "MISSING_ENTRY",
                    $"Entry point '{entryPoint}' has no program file.");
            }
        }

        return programs;
    }

    private static string ReadUtf8(string path)
    {
        var bytes = File.ReadAllBytes(path);
        try
        {
            return Utf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new PackageHostException("INVALID_UTF8", exception.Message);
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
