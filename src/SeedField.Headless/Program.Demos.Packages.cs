using SeedField.PackageHost;
using SeedField.Runtime.Js;

namespace SeedField.Headless;

internal static partial class Program
{
    /// <summary>
    /// Validation/consent-only package check. It never executes the package; the global
    /// ThirdPartyImportEnabled gate remains false.
    /// </summary>
    private static int RunPackageCheck(string[] args, int index)
    {
        if (index + 1 >= args.Length)
        {
            Console.WriteLine("PACKAGE_CHECK usage: --package-check <dir> [--allow-unsigned] [--confirm]");
            return 2;
        }

        var directory = args[index + 1];
        var allowUnsigned = args.Contains("--allow-unsigned");
        var confirmed = args.Contains("--confirm");

        try
        {
            var loaded = OpponentPackageLoader.Load(
                directory,
                PackageTrustStore.Empty,
                new PackageImportConsent(confirmed, allowUnsigned));
            Console.WriteLine(
                $"PACKAGE_CHECK id={loaded.Package.Manifest.PackageId} " +
                $"version={loaded.Package.Manifest.Version} " +
                $"trust={loaded.Trust} signer={loaded.SignerKeyId} " +
                $"hash={loaded.Package.Manifest.ContentHash}");
            Console.WriteLine(
                $"EXECUTION_DISABLED thirdPartyImport={OpponentPackage.ThirdPartyImportEnabled}");
            return 0;
        }
        catch (PackageHostException exception)
        {
            Console.WriteLine(
                $"PACKAGE_CHECK failed [{exception.Code}] {exception.Message}");
            return 1;
        }
    }
}
