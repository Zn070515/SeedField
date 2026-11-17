using SeedField.Runtime.Js;

namespace SeedField.PackageHost;

/// <summary>
/// Explicit host/user consent for one package check or import. Loading without
/// confirmation is rejected; unsigned packages additionally require AllowUnsigned.
/// This record is a validation input, not permission to execute: the global
/// ThirdPartyImportEnabled gate remains closed.
/// </summary>
public sealed record PackageImportConsent(bool UserConfirmed, bool AllowUnsigned);

public enum PackageTrustLevel
{
    Signed,
    Unsigned,
}

public sealed record LoadedOpponentPackage(
    OpponentPackage Package,
    PackageTrustLevel Trust,
    string SignerKeyId);

public sealed class PackageHostException : Exception
{
    public PackageHostException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
