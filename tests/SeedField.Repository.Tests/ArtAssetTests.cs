using System.Security.Cryptography;
using System.Text.Json;

namespace SeedField.Repository.Tests;

/// <summary>
/// Art asset integrity gate: every runtime style-test PNG must be listed, hashed,
/// 512x512 RGBA, and traceable to the Blender source script. This does not judge
/// aesthetics; it prevents unlisted or broken assets from entering the project.
/// </summary>
public sealed class ArtAssetTests
{
    private static readonly byte[] PngSignature =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
    };

    [Fact]
    public void StyleAssetsMatchTheManifest()
    {
        var root = FindRepoRoot();
        var artRoot = Path.Combine(root, "game", "SeedField.Game", "Art");
        var manifestPath = Path.Combine(artRoot, "manifest.json");
        Assert.True(File.Exists(manifestPath), "Art manifest.json is missing.");

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var assets = manifest.RootElement.GetProperty("assets");
        var listed = new HashSet<string>(StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var asset in assets.EnumerateArray())
        {
            var assetId = asset.GetProperty("assetId").GetString() ?? "<missing>";
            var status = asset.GetProperty("status").GetString();
            if (status != "style-test-not-final")
            {
                problems.Add($"{assetId}: status must be style-test-not-final");
            }

            var route = asset.GetProperty("route").GetString();
            if (route is not ("oblique" or "topdown"))
            {
                problems.Add($"{assetId}: unknown camera route '{route}'");
            }

            var resolution = asset.GetProperty("resolutionPx");
            if (resolution[0].GetInt32() != 512 || resolution[1].GetInt32() != 512)
            {
                problems.Add($"{assetId}: resolution must be 512x512");
            }

            foreach (var file in asset.GetProperty("files").EnumerateArray())
            {
                var relative = file.GetProperty("path").GetString() ?? "<missing>";
                if (relative.Contains("..", StringComparison.Ordinal) ||
                    Path.IsPathRooted(relative))
                {
                    problems.Add($"{assetId}: unsafe path '{relative}'");
                    continue;
                }

                var full = Path.GetFullPath(Path.Combine(artRoot, relative));
                if (!full.StartsWith(
                        Path.GetFullPath(artRoot) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"{assetId}: path escapes Art root '{relative}'");
                    continue;
                }

                if (!File.Exists(full))
                {
                    problems.Add($"{assetId}: missing file '{relative}'");
                    continue;
                }

                listed.Add(relative);
                var expectedHash = file.GetProperty("sha256").GetString();
                var actualHash = Convert.ToHexString(
                        SHA256.HashData(File.ReadAllBytes(full)))
                    .ToLowerInvariant();
                if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
                {
                    problems.Add($"{assetId}: sha256 mismatch for '{relative}'");
                }

                problems.AddRange(InspectPng(full, assetId, relative));
            }
        }

        var unlisted = Directory
            .EnumerateFiles(artRoot, "*.png", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(artRoot, path).Replace('\\', '/'))
            .Where(path => !listed.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        problems.AddRange(unlisted.Select(path => $"unlisted asset '{path}'"));

        var ids = assets.EnumerateArray()
            .Select(asset => asset.GetProperty("assetId").GetString())
            .ToList();
        foreach (var expected in new[]
                 {
                     "power_node.oblique.v0",
                     "seed_core.oblique.v0",
                     "worker_frame.oblique.v0",
                     "power_node.topdown.v0",
                     "seed_core.topdown.v0",
                     "worker_frame.topdown.v0",
                 })
        {
            if (!ids.Contains(expected, StringComparer.Ordinal))
            {
                problems.Add($"missing required style asset '{expected}'");
            }
        }

        Assert.True(
            problems.Count == 0,
            "Art asset manifest problems:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static IEnumerable<string> InspectPng(
        string path,
        string assetId,
        string relative)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 33 ||
            !bytes.AsSpan(0, 8).SequenceEqual(PngSignature) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            yield return $"{assetId}: invalid PNG header '{relative}'";
            yield break;
        }

        var width = ReadBigEndianInt32(bytes, 16);
        var height = ReadBigEndianInt32(bytes, 20);
        var bitDepth = bytes[24];
        var colorType = bytes[25];
        if (width != 512 || height != 512)
        {
            yield return $"{assetId}: PNG is {width}x{height}, expected 512x512";
        }

        if (bitDepth != 8)
        {
            yield return $"{assetId}: PNG bit depth is {bitDepth}, expected 8";
        }

        if (colorType != 6)
        {
            yield return $"{assetId}: PNG color type {colorType} lacks alpha (expected 6)";
        }

        if (bytes.Length < 1_000)
        {
            yield return $"{assetId}: PNG '{relative}' looks empty ({bytes.Length} bytes)";
        }
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) |
        (bytes[offset + 1] << 16) |
        (bytes[offset + 2] << 8) |
        bytes[offset + 3];

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
