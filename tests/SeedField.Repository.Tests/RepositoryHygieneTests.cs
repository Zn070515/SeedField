using System.Diagnostics;

namespace SeedField.Repository.Tests;

/// <summary>
/// ArtFlow-style structural gate: runtime data, secrets and build output must never be
/// tracked, and .gitignore must keep covering the runtime roots.
/// </summary>
public sealed class RepositoryHygieneTests
{
    private static readonly string[] ForbiddenDirectories =
    {
        "bin/",
        "obj/",
        "logs/",
        "TestResults/",
        "test-results/",
        ".vs/",
        ".idea/",
        ".godot/",
        "game/.godot/",
        "__pycache__/",
    };

    private static readonly string[] ForbiddenSuffixes =
    {
        ".sqlite3",
        ".db",
        ".pem",
        ".key",
        ".pfx",
        ".log",
        ".zip",
        ".user",
        ".suo",
        ".pyc",
        ".pyo",
    };

    [Fact]
    public void NoRuntimeDataOrSecretsAreTracked()
    {
        var root = FindRepoRoot();
        var offenders = TrackedFiles(root)
            .Where(path => IsForbidden(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Tracked runtime/secret artifacts:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void GitignoreStillCoversRuntimeRoots()
    {
        var root = FindRepoRoot();
        var patterns = File.ReadLines(Path.Combine(root, ".gitignore"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var required in new[]
                 {
                     "bin/",
                     "obj/",
                     "logs/",
                     "TestResults/",
                     ".godot/",
                     "game/.godot/",
                     "*.user",
                     "*.suo",
                     "*.log",
                     "*.tmp",
                     "__pycache__/",
                     "*.pyc",
                 })
        {
            Assert.True(
                patterns.Contains(required),
                $".gitignore lost required pattern '{required}'.");
        }
    }

    private static bool IsForbidden(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (ForbiddenDirectories.Any(directory =>
                normalized.StartsWith(directory, StringComparison.Ordinal) ||
                normalized.Contains("/" + directory, StringComparison.Ordinal)))
        {
            return true;
        }

        var name = Path.GetFileName(normalized);
        if (name.StartsWith(".env", StringComparison.Ordinal) &&
            name != ".env.example")
        {
            return true;
        }

        return ForbiddenSuffixes.Any(suffix =>
            name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> TrackedFiles(string root)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "ls-files -z",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);

        return output
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
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
}
