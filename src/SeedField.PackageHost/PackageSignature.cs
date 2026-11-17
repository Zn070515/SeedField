using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SeedField.Runtime.Js;

namespace SeedField.PackageHost;

/// <summary>
/// ECDSA P-256/SHA-256 signature over a canonical manifest. The canonical form sorts
/// entry points and capabilities and includes the content hash, so reordering JSON or
/// changing a program invalidates the signature.
/// </summary>
public static class PackageSignature
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string Canonicalize(OpponentPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var canonical = new
        {
            schemaVersion = manifest.SchemaVersion,
            packageId = manifest.PackageId,
            version = manifest.Version,
            kind = manifest.Kind,
            engineCompat = manifest.EngineCompat,
            entryPoints = (manifest.EntryPoints ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            requiredCapabilities = (manifest.RequiredCapabilities ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            contentHash = manifest.ContentHash,
        };

        return JsonSerializer.Serialize(canonical, Options);
    }

    public static OpponentPackageManifest Sign(
        OpponentPackageManifest manifest,
        ECDsa privateKey,
        string keyId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        var data = Encoding.UTF8.GetBytes(Canonicalize(manifest));
        var signature = privateKey.SignData(data, HashAlgorithmName.SHA256);
        return manifest with
        {
            SignerKeyId = keyId,
            Signature = Convert.ToBase64String(signature),
        };
    }

    public static bool Verify(
        OpponentPackageManifest manifest,
        ECDsa publicKey)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(publicKey);

        if (string.IsNullOrWhiteSpace(manifest.Signature))
        {
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(manifest.Signature);
        }
        catch (FormatException)
        {
            return false;
        }

        var data = Encoding.UTF8.GetBytes(Canonicalize(manifest));
        return publicKey.VerifyData(
            data,
            signature,
            HashAlgorithmName.SHA256);
    }
}
