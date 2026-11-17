using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SeedField.Repository.Tests;

/// <summary>
/// Machine-enforced repository rules from AGENTS.md and the engineering baseline.
/// These are lint tests: failures point at the offending file and the rule.
/// </summary>
public sealed partial class RepositoryConventionTests
{
    private const int MaxCSharpLines = 600;
    private const int MaxMarkdownLines = 600;

    [Fact]
    public void CSharpSourceFilesStayWithinTheLineLimit()
    {
        var violations = EnumerateFiles("*.cs")
            .Select(path => (Path: Relative(path), Lines: CountLines(path)))
            .Where(item => item.Lines > MaxCSharpLines)
            .OrderByDescending(item => item.Lines)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .Select(item => $"{item.Path}: {item.Lines} lines")
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"C# files must stay at or below {MaxCSharpLines} lines; split by responsibility:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void HeadlessRuntimeEnablesServerGarbageCollection()
    {
        var path = Path.Combine(
            FindRepoRoot(),
            "src",
            "SeedField.Headless",
            "SeedField.Headless.csproj");

        Assert.Contains(
            "<ServerGarbageCollection>true</ServerGarbageCollection>",
            File.ReadAllText(path));
    }

    [Fact]
    public void MarkdownFilesStayWithinTheLineLimit()
    {
        var violations = EnumerateFiles("*.md")
            .Select(path => (Path: Relative(path), Lines: CountLines(path)))
            .Where(item => item.Lines > MaxMarkdownLines)
            .OrderByDescending(item => item.Lines)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .Select(item => $"{item.Path}: {item.Lines} lines")
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Markdown files must stay at or below {MaxMarkdownLines} lines; split the document:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void EveryProjectTargetsNet10()
    {
        var projects = EnumerateFiles("*.csproj").ToList();
        Assert.NotEmpty(projects);

        foreach (var project in projects)
        {
            var document = XDocument.Load(project);
            var frameworks = document.Descendants()
                .Where(element =>
                    element.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                .Select(element => (element.Name.LocalName, Value: element.Value.Trim()))
                .ToList();

            Assert.True(
                frameworks.Count > 0,
                $"{Relative(project)} does not declare TargetFramework.");

            foreach (var (name, value) in frameworks)
            {
                Assert.True(
                    value == "net10.0",
                    $"{Relative(project)} must use net10.0 (found {name}={value}).");
            }
        }
    }

    [Fact]
    public void SimulationCoreStaysFreeOfGodot()
    {
        var projectDirectory = Path.Combine(RepoRoot, "src", "SeedField.Simulation");
        var projectFile = Path.Combine(projectDirectory, "SeedField.Simulation.csproj");
        Assert.True(File.Exists(projectFile), "Simulation project file is missing.");

        var projectText = File.ReadAllText(projectFile);
        Assert.DoesNotContain("Godot", projectText, StringComparison.OrdinalIgnoreCase);

        var godotUsings = Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path))
            .Where(path => File.ReadLines(path).Any(line =>
                line.TrimStart().StartsWith("using Godot", StringComparison.Ordinal)))
            .Select(Relative)
            .ToList();

        Assert.True(
            godotUsings.Count == 0,
            "The simulation core must not reference Godot:" +
            Environment.NewLine + string.Join(Environment.NewLine, godotUsings));
    }

    [Fact]
    public void DesignBibleManifestMatchesEveryFile()
    {
        var baselineDirectory = Path.Combine(
            RepoRoot,
            "docs",
            "design-bible",
            "v1.9-rev2");
        var manifestPath = Path.Combine(baselineDirectory, "MANIFEST.json");
        Assert.True(File.Exists(manifestPath), "Design-bible MANIFEST.json is missing.");

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        var files = root.GetProperty("files");
        var declaredCount = root.GetProperty("file_count").GetInt32();
        Assert.Equal(declaredCount, files.GetArrayLength());

        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files.EnumerateArray())
        {
            var relativePath = file.GetProperty("path").GetString();
            Assert.False(string.IsNullOrWhiteSpace(relativePath), "Manifest path is empty.");

            var fullPath = Path.Combine(baselineDirectory, relativePath);
            Assert.True(
                File.Exists(fullPath),
                $"Manifest lists a missing file: {relativePath}");
            listed.Add(relativePath);

            var expectedSize = file.GetProperty("size_bytes").GetInt64();
            Assert.True(
                new FileInfo(fullPath).Length == expectedSize,
                $"Size mismatch for {relativePath}; run tools/refresh_design_manifest.py.");

            var expectedHash = file.GetProperty("sha256").GetString();
            var actualHash = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(fullPath)))
                .ToLowerInvariant();
            Assert.True(
                string.Equals(expectedHash, actualHash, StringComparison.Ordinal),
                $"SHA-256 mismatch for {relativePath}; run tools/refresh_design_manifest.py.");
        }

        var unlisted = Directory
            .EnumerateFiles(baselineDirectory, "*.md", SearchOption.AllDirectories)
            .Select(path => Path
                .GetRelativePath(baselineDirectory, path)
                .Replace('\\', '/'))
            .Where(path => !listed.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unlisted.Count == 0,
            "Design-bible files are missing from MANIFEST.json; " +
            "run tools/refresh_design_manifest.py:" +
            Environment.NewLine + string.Join(Environment.NewLine, unlisted));
    }

    [Fact]
    public void AdrFilesFollowTheNumberingConvention()
    {
        var adrDirectory = Path.Combine(RepoRoot, "docs", "engineering", "adr");
        var numbers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(adrDirectory, "*.md"))
        {
            var name = Path.GetFileName(path);
            var match = AdrFilePattern().Match(name);
            Assert.True(
                match.Success,
                $"ADR filename must match ADR-NNNN-kebab-case.md: {name}");
            Assert.True(
                numbers.Add(match.Groups[1].Value),
                $"Duplicate ADR number {match.Groups[1].Value}.");
        }

        Assert.NotEmpty(numbers);
    }

    [GeneratedRegex(@"^ADR-(\d{4})-[a-z0-9-]+\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex AdrFilePattern();

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

        throw new InvalidOperationException("Repository root (SeedField.slnx) was not found.");
    }

    private static IEnumerable<string> EnumerateFiles(string pattern) =>
        Directory
            .EnumerateFiles(RepoRoot, pattern, SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static bool IsGenerated(string path)
    {
        // Classify by path relative to the repository root: agent worktrees live
        // under .claude/ and must not be linted, and an absolute checkout path such
        // as D:/bin/SeedField must not silently skip every source file.
        var segments = Relative(path).Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment =>
            segment is "bin" or "obj" or ".git" or ".claude" or "TestResults" or ".godot");
    }

    private static int CountLines(string path) => File.ReadLines(path).Count();

    private static string Relative(string path) =>
        Path.GetRelativePath(RepoRoot, path).Replace('\\', '/');
}
