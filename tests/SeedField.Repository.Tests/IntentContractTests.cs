using System.Text.RegularExpressions;

namespace SeedField.Repository.Tests;

/// <summary>
/// Structural gate: every IntentKind must have a wire name, a Jint parser case and
/// a TypeScript union entry. Adding an intent without updating the script contract
/// fails here instead of failing silently at runtime.
/// </summary>
public sealed partial class IntentContractTests
{
    private static readonly IReadOnlyDictionary<string, string> WireNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Move"] = "move",
            ["Attack"] = "attack",
            ["SendMessage"] = "send",
            ["Wait"] = "wait",
            ["TransferMaterial"] = "transfer",
            ["SendEnergy"] = "sendEnergy",
            ["DrawEnergy"] = "drawEnergy",
            ["Mine"] = "mine",
            ["Production"] = "production",
        };

    [Fact]
    public void EveryIntentKindHasParserAndTypeScriptCoverage()
    {
        var root = FindRepoRoot();
        var intents = File.ReadAllText(Path.Combine(
            root,
            "src",
            "SeedField.Simulation",
            "Intents.cs"));
        var parser = File.ReadAllText(Path.Combine(
            root,
            "src",
            "SeedField.Runtime.Js",
            "JsController.cs"));
        var contract = File.ReadAllText(Path.Combine(
            root,
            "contracts",
            "seedfield-controller.d.ts"));

        var kinds = Regex
            .Matches(
                ExtractBlock(intents, "public enum IntentKind", "}"),
                @"^\s*(\w+)\s*=\s*\d+",
                RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToList();

        var problems = new List<string>();
        foreach (var kind in kinds)
        {
            if (!WireNames.TryGetValue(kind, out var wire))
            {
                problems.Add($"{kind}: no wire name registered in IntentContractTests");
                continue;
            }

            if (!Regex.IsMatch(parser, $@"case\s+""{Regex.Escape(wire)}""\s*:"))
            {
                problems.Add($"{kind}: Jint parser has no case \"{wire}\"");
            }

            if (!contract.Contains($"\"{wire}\"", StringComparison.Ordinal))
            {
                problems.Add($"{kind}: TypeScript contract has no \"{wire}\" intent");
            }
        }

        Assert.True(
            problems.Count == 0,
            "Intent contract problems:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static string ExtractBlock(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Missing block {startMarker}");
        return text[start..end];
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
