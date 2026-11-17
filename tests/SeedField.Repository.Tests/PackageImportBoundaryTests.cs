namespace SeedField.Repository.Tests;

/// <summary>
/// Executable form of the ADR-0020 gate: the opponent-package namespace may validate
/// in-memory data, but must not gain file, network or process capabilities until the
/// isolation phases are complete.
/// </summary>
public sealed class PackageImportBoundaryTests
{
    private static readonly string[] ForbiddenMarkers =
    {
        "File.ReadAllBytes",
        "File.ReadAllText",
        "File.Open",
        "File.WriteAllBytes",
        "File.WriteAllText",
        "Directory.",
        "ZipFile",
        "HttpClient",
        "WebClient",
        "WebRequest",
        "Socket(",
        "Process.Start",
        "Assembly.Load",
        "BinaryFormatter",
        "DllImport",
        "NativeLibrary.Load",
    };

    [Fact]
    public void OpponentNamespaceHasNoFileNetworkOrProcessApi()
    {
        var directory = Path.Combine(
            RepoRoot,
            "src",
            "SeedField.Runtime.Js",
            "Opponents");
        Assert.True(Directory.Exists(directory), "Opponent namespace directory is missing.");

        var violations = new List<string>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            foreach (var marker in ForbiddenMarkers)
            {
                if (text.Contains(marker, StringComparison.Ordinal))
                {
                    violations.Add($"{Relative(path)} contains forbidden API '{marker}'");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Opponent packages must stay data-only until ADR-0020 isolation is complete:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void PackageHostHasNoNetworkOrProcessApi()
    {
        var directory = Path.Combine(
            RepoRoot,
            "src",
            "SeedField.PackageHost");
        Assert.True(Directory.Exists(directory), "Package host directory is missing.");

        var forbidden = new[]
        {
            "HttpClient",
            "WebClient",
            "WebRequest",
            "Socket(",
            "Process.Start",
            "Assembly.Load",
            "DllImport",
            "NativeLibrary.Load",
        };
        var violations = new List<string>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            foreach (var marker in forbidden)
            {
                if (text.Contains(marker, StringComparison.Ordinal))
                {
                    violations.Add($"{Relative(path)} contains '{marker}'");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "PackageHost must stay offline and must not launch processes:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ThirdPartyImportGateStaysClosedInSource()
    {
        var path = Path.Combine(
            RepoRoot,
            "src",
            "SeedField.Runtime.Js",
            "Opponents",
            "OpponentPackage.cs");
        var text = File.ReadAllText(path);

        Assert.Contains(
            "ThirdPartyImportEnabled = false",
            text,
            StringComparison.Ordinal);
    }

    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SeedField.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(RepoRoot, path).Replace('\\', '/');
}
