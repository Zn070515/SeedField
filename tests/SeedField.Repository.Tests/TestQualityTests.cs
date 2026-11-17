using System.Text.RegularExpressions;

namespace SeedField.Repository.Tests;

/// <summary>
/// Test quality gate inspired by ArtFlow: no silent skips, no wall-clock sleeps,
/// no unseeded randomness and no trivial assertions.
/// </summary>
public sealed partial class TestQualityTests
{
    private static readonly (Regex Pattern, string Message)[] Forbidden =
    {
        (SkipPattern(), "skipped tests hide failures; remove Skip or make the test deterministic"),
        (SleepPattern(), "Thread.Sleep makes tests slow and flaky"),
        (DelayPattern(), "Task.Delay makes tests slow and flaky"),
        (UnseededRandomPattern(), "use a seeded Random so failures are reproducible"),
        (WallClockPattern(), "tests must not depend on wall-clock time"),
        (TrivialTruePattern(), "Assert.True(true) proves nothing"),
        (TrivialFalsePattern(), "Assert.False(false) proves nothing"),
        (PragmaPattern(), "do not suppress compiler/analyzer warnings in tests"),
    };

    [Fact]
    public void TestsHaveNoSilentSkipsSleepsOrUnseededRandomness()
    {
        var root = FindRepoRoot();
        var testsRoot = Path.Combine(root, "tests");
        var violations = new List<string>();

        foreach (var path in Directory.EnumerateFiles(
                     testsRoot,
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            if (IsGenerated(path) ||
                Path.GetFileName(path) == "TestQualityTests.cs")
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            var lines = File.ReadLines(path).ToArray();
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var (pattern, message) in Forbidden)
                {
                    if (pattern.IsMatch(lines[index]))
                    {
                        violations.Add($"{relative}:{index + 1}: {message}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Test quality violations:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static bool IsGenerated(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal) ||
               normalized.Contains("/obj/", StringComparison.Ordinal);
    }

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

    [GeneratedRegex(@"\[\s*(Fact|Theory)\s*\([^)]*Skip\s*=")]
    private static partial Regex SkipPattern();

    [GeneratedRegex(@"Thread\.Sleep\s*\(")]
    private static partial Regex SleepPattern();

    [GeneratedRegex(@"\bTask\.Delay\s*\(")]
    private static partial Regex DelayPattern();

    [GeneratedRegex(@"new\s+Random\s*\(\s*\)")]
    private static partial Regex UnseededRandomPattern();

    [GeneratedRegex(@"DateTime(Offset)?\.(Now|UtcNow)")]
    private static partial Regex WallClockPattern();

    [GeneratedRegex(@"Assert\.True\s*\(\s*true\s*\)")]
    private static partial Regex TrivialTruePattern();

    [GeneratedRegex(@"Assert\.False\s*\(\s*false\s*\)")]
    private static partial Regex TrivialFalsePattern();

    [GeneratedRegex(@"#pragma\s+warning\s+disable")]
    private static partial Regex PragmaPattern();
}
