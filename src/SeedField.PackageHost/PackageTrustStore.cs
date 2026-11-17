using System.Security.Cryptography;

namespace SeedField.PackageHost;

/// <summary>
/// Key-id to public-key allowlist. An empty store means no signed third-party package
/// is trusted; tests may add ephemeral keys.
/// </summary>
public sealed class PackageTrustStore
{
    private readonly Dictionary<string, ECDsa> _keys = new(StringComparer.Ordinal);

    public static PackageTrustStore Empty => new();

    public PackageTrustStore Add(string keyId, ECDsa publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentNullException.ThrowIfNull(publicKey);

        if (_keys.ContainsKey(keyId))
        {
            throw new ArgumentException(
                $"Trust key '{keyId}' is already registered.",
                nameof(keyId));
        }

        _keys.Add(keyId, publicKey);
        return this;
    }

    public bool TryGet(string keyId, out ECDsa publicKey)
    {
        ArgumentNullException.ThrowIfNull(keyId);
        return _keys.TryGetValue(keyId, out publicKey!);
    }
}
