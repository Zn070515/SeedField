using System.Text.RegularExpressions;

namespace SeedField.Repository.Tests;

/// <summary>
/// Structural gate: every simulation event must have an explicit presentation
/// decision (log/describe/filter), and non-logged events are an explicit,
/// shrink-only allowlist. This catches the "new event silently disappears" class
/// of bug without asserting visual details.
/// </summary>
public sealed partial class PresentationCoverageTests
{
    private static readonly HashSet<string> NonLogEvents = new(StringComparer.Ordinal)
    {
        "EntityMoved",
        "EntityInventoryLost",
        "MessageExpired",
        "MaterialShipped",
        "EnergyShipped",
        "EnergyStored",
        "EnergyDrawn",
    };

    [Fact]
    public void EverySimulationEventHasAPresentationDecision()
    {
        var root = FindRepoRoot();
        var events = Regex
            .Matches(
                File.ReadAllText(Path.Combine(
                    root,
                    "src",
                    "SeedField.Simulation",
                    "SimulationEvents.cs")),
                @"^public sealed record (\w+)\(",
                RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var describe = File.ReadAllText(Path.Combine(
            root,
            "game",
            "SeedField.Game",
            "Scripts",
            "Main.Events.cs"));
        var draw = File.ReadAllText(Path.Combine(
            root,
            "game",
            "SeedField.Game",
            "Scripts",
            "Main.Draw.cs"));
        var filter = File.ReadAllText(Path.Combine(
            root,
            "game",
            "SeedField.Game",
            "Scripts",
            "Presentation",
            "PlayerEventFilter.cs"));

        var logworthy = ExtractBlock(
            draw,
            "private static bool IsLogworthy",
            "private static Color EventColor");

        var problems = new List<string>();
        foreach (var name in events)
        {
            if (!ContainsWord(filter, name))
            {
                problems.Add($"{name}: missing PlayerEventFilter decision");
            }

            var isLogged = ContainsWord(logworthy, name);
            if (NonLogEvents.Contains(name))
            {
                if (isLogged)
                {
                    problems.Add(
                        $"{name}: listed as non-logged but IsLogworthy includes it; " +
                        "remove the stale allowlist entry");
                }

                continue;
            }

            if (!isLogged)
            {
                problems.Add($"{name}: not in IsLogworthy and not in NonLogEvents");
            }

            if (!ContainsWord(describe, name))
            {
                problems.Add($"{name}: logged events need a Describe case");
            }
        }

        Assert.True(
            problems.Count == 0,
            "Presentation coverage problems:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static string ExtractBlock(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        var end = text.IndexOf(endMarker, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Missing block {startMarker}..{endMarker}");
        return text[start..end];
    }

    private static bool ContainsWord(string text, string word) =>
        Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b");

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
