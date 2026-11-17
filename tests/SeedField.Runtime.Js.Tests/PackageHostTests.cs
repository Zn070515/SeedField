using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SeedField.PackageHost;
using SeedField.Runtime.Js;

namespace SeedField.Runtime.Js.Tests;

public sealed class PackageHostTests
{
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void SignedPackageLoadsWithTrustedKey()
    {
        using var temp = new TempPackage(signed: true);

        var loaded = OpponentPackageLoader.Load(
            temp.Directory,
            new PackageTrustStore().Add("test-key", temp.Key),
            new PackageImportConsent(UserConfirmed: true, AllowUnsigned: false));

        Assert.Equal(PackageTrustLevel.Signed, loaded.Trust);
        Assert.Equal("test.pkg", loaded.Package.Manifest.PackageId);
        Assert.Equal("test-key", loaded.SignerKeyId);
    }

    [Fact]
    public void TamperedProgramFailsContentHash()
    {
        using var temp = new TempPackage(signed: true);
        File.WriteAllText(
            Path.Combine(temp.Directory, "programs", "controller.js"),
            "function decide(){ return [{ type: 'wait', entityId: 9 }]; }");

        var exception = Assert.Throws<PackageHostException>(() =>
            Load(temp, new PackageImportConsent(true, false)));

        Assert.Equal("CONTENT_HASH_MISMATCH", exception.Code);
    }

    [Fact]
    public void TamperedSignatureIsRejected()
    {
        using var temp = new TempPackage(signed: true);
        temp.RewriteManifest(manifest => manifest with
        {
            Signature = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        });

        var exception = Assert.Throws<PackageHostException>(() =>
            Load(temp, new PackageImportConsent(true, false)));

        Assert.Equal("INVALID_SIGNATURE", exception.Code);
    }

    [Fact]
    public void UnknownSignerIsRejected()
    {
        using var temp = new TempPackage(signed: true);

        var exception = Assert.Throws<PackageHostException>(() =>
            OpponentPackageLoader.Load(
                temp.Directory,
                PackageTrustStore.Empty,
                new PackageImportConsent(true, false)));

        Assert.Equal("UNTRUSTED_SIGNER", exception.Code);
    }

    [Fact]
    public void UnsignedPackageNeedsAllowUnsignedConsent()
    {
        using var temp = new TempPackage(signed: false);

        var rejected = Assert.Throws<PackageHostException>(() =>
            Load(temp, new PackageImportConsent(true, false)));
        Assert.Equal("UNSIGNED_PACKAGE", rejected.Code);

        var loaded = Load(temp, new PackageImportConsent(true, true));
        Assert.Equal(PackageTrustLevel.Unsigned, loaded.Trust);
    }

    [Fact]
    public void MissingConsentIsRejectedBeforeReadingPrograms()
    {
        using var temp = new TempPackage(signed: false);

        var exception = Assert.Throws<PackageHostException>(() =>
            Load(temp, new PackageImportConsent(false, true)));

        Assert.Equal("CONSENT_REQUIRED", exception.Code);
    }

    [Fact]
    public void UnexpectedFileIsRejected()
    {
        using var temp = new TempPackage(signed: true);
        File.WriteAllText(Path.Combine(temp.Directory, "payload.bin"), "x");

        var exception = Assert.Throws<PackageHostException>(() =>
            Load(temp, new PackageImportConsent(true, false)));

        Assert.Equal("UNEXPECTED_FILE", exception.Code);
    }

    [Fact]
    public void UnsafeEntryPointPathIsRejected()
    {
        using var temp = new TempPackage(signed: true);
        temp.RewriteManifest(manifest => manifest with
        {
            EntryPoints = new[] { "../evil.js" },
        });

        var exception = Assert.Throws<PackageHostException>(() =>
            Load(temp, new PackageImportConsent(true, false)));

        Assert.Equal("UNSAFE_PATH", exception.Code);
    }

    [Fact]
    public void InvalidUtf8IsRejected()
    {
        using var temp = new TempPackage(signed: false);
        File.WriteAllBytes(
            Path.Combine(temp.Directory, "programs", "controller.js"),
            new byte[] { 0xFF, 0xFE });

        var exception = Assert.Throws<PackageHostException>(() =>
            Load(temp, new PackageImportConsent(true, true)));

        Assert.Equal("INVALID_UTF8", exception.Code);
    }

    [Fact]
    public void OversizedProgramIsRejected()
    {
        using var temp = new TempPackage(signed: false);
        File.WriteAllText(
            Path.Combine(temp.Directory, "programs", "controller.js"),
            new string('a', OpponentPackage.MaxProgramBytes + 1));

        var exception = Assert.Throws<PackageHostException>(() =>
            Load(temp, new PackageImportConsent(true, true)));

        Assert.Equal("PACKAGE_TOO_LARGE", exception.Code);
    }

    [Fact]
    public void ThirdPartyExecutionGateStaysClosed()
    {
        Assert.False(OpponentPackage.ThirdPartyImportEnabled);
    }

    private static LoadedOpponentPackage Load(
        TempPackage temp,
        PackageImportConsent consent) =>
        OpponentPackageLoader.Load(
            temp.Directory,
            new PackageTrustStore().Add("test-key", temp.Key),
            consent);

    private sealed class TempPackage : IDisposable
    {
        private OpponentPackageManifest _manifest;
        private readonly string _program =
            "function decide(){ return [{ type: 'wait', entityId: 1 }]; }";

        public TempPackage(bool signed)
        {
            Directory = Path.Combine(
                Path.GetTempPath(),
                $"seedfield-package-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(
                Path.Combine(Directory, "programs"));
            File.WriteAllText(
                Path.Combine(Directory, "programs", "controller.js"),
                _program);

            Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _manifest = new OpponentPackageManifest(
                OpponentPackage.CurrentSchemaVersion,
                "test.pkg",
                "1.0.0",
                "offline-opponent",
                OpponentPackage.CurrentEngineCompat,
                new[] { "programs/controller.js" },
                new[] { "tactical-control" },
                OpponentPackage.ComputeContentHash(
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["programs/controller.js"] = _program,
                    }));
            if (signed)
            {
                _manifest = PackageSignature.Sign(
                    _manifest,
                    Key,
                    "test-key");
            }

            WriteManifest();
        }

        public string Directory { get; }

        public ECDsa Key { get; }

        public void RewriteManifest(
            Func<OpponentPackageManifest, OpponentPackageManifest> change)
        {
            _manifest = change(_manifest);
            WriteManifest();
        }

        public void Dispose()
        {
            Key.Dispose();
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch
            {
                // Test cleanup only.
            }
        }

        private void WriteManifest() =>
            File.WriteAllText(
                Path.Combine(Directory, OpponentPackageLoader.ManifestFileName),
                JsonSerializer.Serialize(_manifest, ManifestJson),
                new UTF8Encoding(false));
    }
}
