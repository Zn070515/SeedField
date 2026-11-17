using System.Text.RegularExpressions;

namespace SeedField.Repository.Tests;

/// <summary>
/// Architecture gate: the simulation core stays Godot-free, integer/deterministic and
/// independent of the script runtime; wall-clock/Random/double are rejected.
/// </summary>
public sealed partial class ArchitectureLintTests
{
    [Fact]
    public void SimulationProjectHasNoRuntimeOrGodotReferences()
    {
        var root = FindRepoRoot();
        var project = Path.Combine(
            root,
            "src",
            "SeedField.Simulation",
            "SeedField.Simulation.csproj");
        var text = File.ReadAllText(project);

        Assert.DoesNotContain("Godot", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Runtime.Js", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProjectReference", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SimulationSourcesStayIntegerDeterministicAndClockFree()
    {
        var root = FindRepoRoot();
        var directory = Path.Combine(root, "src", "SeedField.Simulation");
        var forbidden = new (Regex Pattern, string Message)[]
        {
            (new Regex(@"\bSystem\.Random\b", RegexOptions.Compiled),
                "use DeterministicRandom or world state"),
            (new Regex(@"\bnew\s+Random\s*\(", RegexOptions.Compiled),
                "use DeterministicRandom"),
            (new Regex(@"DateTime(Offset)?\.(Now|UtcNow)", RegexOptions.Compiled),
                "wall-clock time is not allowed in the simulation"),
            (new Regex(@"\bStopwatch\b", RegexOptions.Compiled),
                "wall-clock timing is not allowed in the simulation"),
            (new Regex(@"\bThread\.Sleep\s*\(", RegexOptions.Compiled),
                "blocking sleep is not allowed in the simulation"),
            (new Regex(@"\bTask\.Delay\s*\(", RegexOptions.Compiled),
                "async delay is not allowed in the simulation"),
            (new Regex(@"\b(double|float|decimal)\b", RegexOptions.Compiled),
                "floating-point arithmetic is not allowed in the simulation"),
            (new Regex(@"\bGodot\b", RegexOptions.Compiled),
                "the simulation core must not reference Godot"),
        };

        var violations = new List<string>();
        foreach (var path in Directory.EnumerateFiles(
                     directory,
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            if (path.Replace('\\', '/').Contains("/bin/", StringComparison.Ordinal) ||
                path.Replace('\\', '/').Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            var lines = File.ReadLines(path).ToArray();
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var (pattern, message) in forbidden)
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
            "Simulation architecture violations:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
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
