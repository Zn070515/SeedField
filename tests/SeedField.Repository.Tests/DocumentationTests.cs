using System.Text.RegularExpressions;

namespace SeedField.Repository.Tests;

/// <summary>
/// ArtFlow-style documentation gate: local links resolve, ADRs are indexed and
/// structured, and plans carry verification/rollback sections without placeholders.
/// </summary>
public sealed partial class DocumentationTests
{
    [Fact]
    public void LocalMarkdownLinksResolve()
    {
        var root = FindRepoRoot();
        var problems = new List<string>();

        foreach (var path in MarkdownFiles(root))
        {
            var relative = Relative(root, path);
            var text = File.ReadAllText(path);
            foreach (Match match in LinkPattern().Matches(text))
            {
                var target = match.Groups[1].Value.Trim();
                if (target.StartsWith('<') && target.EndsWith('>'))
                {
                    target = target[1..^1];
                }

                target = target.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                if (target.Length == 0 ||
                    target.StartsWith('#') ||
                    target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var withoutAnchor = target.Split('#')[0];
                if (withoutAnchor.Length == 0)
                {
                    continue;
                }

                var decoded = Uri.UnescapeDataString(withoutAnchor);
                var full = decoded.StartsWith('/')
                    ? Path.Combine(root, decoded.TrimStart('/'))
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, decoded));

                if (!File.Exists(full) && !Directory.Exists(full))
                {
                    problems.Add($"{relative}: broken link '{target}'");
                }
            }
        }

        Assert.True(
            problems.Count == 0,
            "Broken local Markdown links:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void EveryAdrIsIndexedAndStructured()
    {
        var root = FindRepoRoot();
        var adrDirectory = Path.Combine(root, "docs", "engineering", "adr");
        var index = string.Join(
            "\n",
            File.ReadAllText(Path.Combine(root, "docs", "engineering", "IMPLEMENTATION_BASELINE.md")),
            File.ReadAllText(Path.Combine(root, "README.md")));
        var problems = new List<string>();

        foreach (var path in Directory.EnumerateFiles(adrDirectory, "ADR-*.md"))
        {
            var name = Path.GetFileName(path);
            if (!index.Contains(name, StringComparison.Ordinal))
            {
                problems.Add($"{name}: not linked from README or IMPLEMENTATION_BASELINE");
            }

            var number = int.Parse(name[4..8]);
            if (number < 13)
            {
                continue;
            }

            var text = File.ReadAllText(path);
            foreach (var heading in new[] { "## 非目标", "## 后果" })
            {
                if (!text.Contains(heading, StringComparison.Ordinal))
                {
                    problems.Add($"{name}: missing '{heading}'");
                }
            }
        }

        Assert.True(
            problems.Count == 0,
            "ADR index/structure problems:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void PlansHaveVerificationRollbackAndNoPlaceholders()
    {
        var root = FindRepoRoot();
        var plansDirectory = Path.Combine(root, "docs", "superpowers", "plans");
        if (!Directory.Exists(plansDirectory))
        {
            return;
        }

        var problems = new List<string>();
        foreach (var path in Directory.EnumerateFiles(plansDirectory, "*.md"))
        {
            var name = Path.GetFileName(path);
            var text = File.ReadAllText(path);
            if (!Regex.IsMatch(text, @"^##\s+.*Verification", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                problems.Add($"{name}: missing 'Verification' section");
            }

            if (!Regex.IsMatch(text, @"^##\s+.*Rollback", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                problems.Add($"{name}: missing 'Rollback' section");
            }

            foreach (Match match in PlaceholderPattern().Matches(text))
            {
                problems.Add($"{name}: placeholder '{match.Value}'");
            }
        }

        Assert.True(
            problems.Count == 0,
            "Plan gate problems:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static IEnumerable<string> MarkdownFiles(string root) =>
        Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .Where(path =>
                !path.Replace('\\', '/').Contains("/.git/", StringComparison.Ordinal) &&
                !path.Replace('\\', '/').Contains("/bin/", StringComparison.Ordinal) &&
                !path.Replace('\\', '/').Contains("/obj/", StringComparison.Ordinal) &&
                !path.Replace('\\', '/').Contains("/node_modules/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

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

    [GeneratedRegex(@"\[[^\]]*\]\(([^)]+)\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"\b(TBD|TODO|implement later|fill in details)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderPattern();
}
