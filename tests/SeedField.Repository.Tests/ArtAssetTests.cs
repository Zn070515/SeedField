using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace SeedField.Repository.Tests;

/// <summary>
/// Art asset integrity gate: every runtime style-test PNG must be listed, hashed,
/// 512x512 RGBA, have real transparent margins and alpha content, expose a pivot
/// that matches its alpha bounds, and contain the expected direction frames.
/// This does not judge aesthetics; it prevents broken or mis-anchored assets.
/// </summary>
public sealed class ArtAssetTests
{
    private static readonly byte[] PngSignature =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
    };

    private static readonly Dictionary<string, int[]> ExpectedDirections = new(StringComparer.Ordinal)
    {
        ["power_node"] = new[] { 0, 90, 180, 270 },
        ["seed_core"] = new[] { 0, 36 },
        ["seed_core_regenerating"] = new[] { 0 },
        ["seed_core_bootstrap"] = new[] { 0 },
        ["seed_core_besieged"] = new[] { 0 },
        ["worker_frame"] = new[] { 0, 45, 90, 135, 180, 225, 270, 315 },
        ["worker_frame_drill"] = new[] { 0, 45, 90, 135, 180, 225, 270, 315 },
        ["worker_frame_sensor"] = new[] { 0, 45, 90, 135, 180, 225, 270, 315 },
        ["direction_probe"] = new[]
        {
            0, 23, 45, 68, 90, 113, 135, 158, 180, 203, 225, 248, 270, 293, 315, 338,
        },
    };

    private static readonly Dictionary<string, string> ExpectedStates = new(StringComparer.Ordinal)
    {
        ["seed_core"] = "idle",
        ["seed_core_regenerating"] = "regenerating",
        ["seed_core_bootstrap"] = "bootstrap",
        ["seed_core_besieged"] = "besieged",
    };

    [Fact]
    public void StyleAssetsMatchTheManifest()
    {
        var root = FindRepoRoot();
        var artRoot = Path.Combine(root, "game", "SeedField.Game", "Art");
        var manifestPath = Path.Combine(artRoot, "manifest.json");
        Assert.True(File.Exists(manifestPath), "Art manifest.json is missing.");

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.True(
            manifest.RootElement.GetProperty("schemaVersion").GetInt32() >= 3,
            "Art manifest schemaVersion must be at least 3.");

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

            var footprint = asset.GetProperty("footprintPx").GetInt32();
            if (footprint is < 64 or > 768)
            {
                problems.Add($"{assetId}: footprintPx {footprint} is outside 64..768");
            }

            var pivot = asset.GetProperty("pivot");
            var pivotX = pivot[0].GetDouble();
            var pivotY = pivot[1].GetDouble();
            if (pivotX is < 0 or > 1 || pivotY is < 0 or > 1)
            {
                problems.Add($"{assetId}: pivot must be normalized 0..1");
            }

            var files = asset.GetProperty("files").EnumerateArray().ToList();
            var declaredDirections = asset.GetProperty("directions").GetInt32();
            if (declaredDirections != files.Count)
            {
                problems.Add(
                    $"{assetId}: directions {declaredDirections} != file count {files.Count}");
            }

            var name = assetId.Split('.')[0];
            if (ExpectedStates.TryGetValue(name, out var expectedState))
            {
                var actualState = asset.TryGetProperty("state", out var stateElement)
                    ? stateElement.GetString()
                    : null;
                if (actualState != expectedState)
                {
                    problems.Add($"{assetId}: state must be '{expectedState}', got '{actualState}'");
                }
            }

            int[] expectedAngles;
            if (route == "topdown")
            {
                expectedAngles = new[] { 0 };
            }
            else if (ExpectedDirections.TryGetValue(name, out var registeredAngles))
            {
                expectedAngles = registeredAngles;
            }
            else
            {
                problems.Add(
                    $"{assetId}: no expected direction set registered for '{name}'");
                expectedAngles = Array.Empty<int>();
            }

            if (declaredDirections != expectedAngles.Length)
            {
                problems.Add(
                    $"{assetId}: expected {expectedAngles.Length} directions, got {declaredDirections}");
            }

            var angles = new List<int>();
            AlphaInfo? frontFrame = null;
            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
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
                var bytes = File.ReadAllBytes(full);
                var expectedHash = file.GetProperty("sha256").GetString();
                var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
                {
                    problems.Add($"{assetId}: sha256 mismatch for '{relative}'");
                }

                var angle = ParseAngle(relative);
                if (angle is null)
                {
                    problems.Add($"{assetId}: file name lacks a direction angle '{relative}'");
                }
                else
                {
                    angles.Add(angle.Value);
                    var expectedName = $"{name}_{route}_{angle.Value:000}.png";
                    if (Path.GetFileName(relative) != expectedName)
                    {
                        problems.Add($"{assetId}: expected file name '{expectedName}'");
                    }
                }

                AlphaInfo alpha;
                try
                {
                    alpha = DecodeAlpha(bytes);
                }
                catch (Exception exception) when (
                    exception is InvalidDataException or IOException)
                {
                    problems.Add($"{assetId}: cannot decode '{relative}': {exception.Message}");
                    continue;
                }

                if (alpha.Width != 512 || alpha.Height != 512)
                {
                    problems.Add($"{assetId}: PNG is {alpha.Width}x{alpha.Height}, expected 512x512");
                }

                if (alpha.Coverage < 0.04)
                {
                    problems.Add($"{assetId}: '{relative}' is only {alpha.Coverage:P0} opaque");
                }

                if (alpha.Coverage > 0.80)
                {
                    problems.Add($"{assetId}: '{relative}' is {alpha.Coverage:P0} opaque; alpha likely broken");
                }

                if (alpha.MinX < 2 || alpha.MinY < 2 ||
                    alpha.MaxX > alpha.Width - 3 || alpha.MaxY > alpha.Height - 3)
                {
                    problems.Add(
                        $"{assetId}: sprite touches the frame edge " +
                        $"({alpha.MinX},{alpha.MinY})-({alpha.MaxX},{alpha.MaxY})");
                }

                var recordedBox = file.GetProperty("alphaBBox");
                if (recordedBox[0].GetInt32() != alpha.MinX ||
                    recordedBox[1].GetInt32() != alpha.MinY ||
                    recordedBox[2].GetInt32() != alpha.MaxX ||
                    recordedBox[3].GetInt32() != alpha.MaxY)
                {
                    problems.Add($"{assetId}: alphaBBox is stale for '{relative}'");
                }

                foreach (var passName in new[] { "normal", "emissive" })
                {
                    if (!file.TryGetProperty(passName, out var pass))
                    {
                        continue;
                    }

                    var passRelative = pass.GetProperty("path").GetString() ?? "<missing>";
                    if (passRelative.Contains("..", StringComparison.Ordinal) ||
                        Path.IsPathRooted(passRelative))
                    {
                        problems.Add($"{assetId}: unsafe {passName} path '{passRelative}'");
                        continue;
                    }

                    var passFull = Path.GetFullPath(Path.Combine(artRoot, passRelative));
                    if (!passFull.StartsWith(
                            Path.GetFullPath(artRoot) + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        problems.Add($"{assetId}: {passName} path escapes Art root");
                        continue;
                    }

                    if (!File.Exists(passFull))
                    {
                        problems.Add($"{assetId}: missing {passName} file '{passRelative}'");
                        continue;
                    }

                    listed.Add(passRelative);
                    var passBytes = File.ReadAllBytes(passFull);
                    var passHash = Convert.ToHexString(SHA256.HashData(passBytes))
                        .ToLowerInvariant();
                    if (!string.Equals(
                            pass.GetProperty("sha256").GetString(),
                            passHash,
                            StringComparison.Ordinal))
                    {
                        problems.Add($"{assetId}: {passName} sha256 mismatch");
                    }

                    AlphaInfo passAlpha;
                    try
                    {
                        passAlpha = DecodeAlpha(passBytes);
                    }
                    catch (Exception exception) when (
                        exception is InvalidDataException or IOException)
                    {
                        problems.Add(
                            $"{assetId}: cannot decode {passName} '{passRelative}': " +
                            exception.Message);
                        continue;
                    }

                    if (passAlpha.Width != 512 || passAlpha.Height != 512)
                    {
                        problems.Add($"{assetId}: {passName} must be 512x512");
                    }

                    var passBox = pass.GetProperty("alphaBBox");
                    if (passBox[0].GetInt32() != passAlpha.MinX ||
                        passBox[1].GetInt32() != passAlpha.MinY ||
                        passBox[2].GetInt32() != passAlpha.MaxX ||
                        passBox[3].GetInt32() != passAlpha.MaxY)
                    {
                        problems.Add($"{assetId}: {passName} alphaBBox is stale");
                    }
                }

                if (index == 0)
                {
                    frontFrame = alpha;
                }
            }

            angles.Sort();
            if (!angles.SequenceEqual(expectedAngles))
            {
                problems.Add(
                    $"{assetId}: direction angles [{string.Join(",", angles)}] != " +
                    $"[{string.Join(",", expectedAngles)}]");
            }

            if (frontFrame is not null)
            {
                if (Math.Abs(pivotX - 0.5) > 0.001)
                {
                    problems.Add($"{assetId}: pivot X must be 0.5, got {pivotX}");
                }

                if (route == "topdown")
                {
                    if (Math.Abs(pivotY - 0.5) > 0.001)
                    {
                        problems.Add($"{assetId}: topdown pivot Y must be 0.5, got {pivotY}");
                    }
                }
                else
                {
                    var expectedPivotY = (frontFrame.MaxY + 1) / (double)frontFrame.Height;
                    if (Math.Abs(pivotY - expectedPivotY) > 0.01)
                    {
                        problems.Add(
                            $"{assetId}: oblique pivot Y {pivotY:0.000} does not match " +
                            $"front alpha bottom {expectedPivotY:0.000}");
                    }
                }
            }
        }

        var coreFrames = assets.EnumerateArray()
            .Where(asset => asset.TryGetProperty("state", out _) &&
                            asset.GetProperty("route").GetString() == "oblique")
            .ToList();
        if (coreFrames.Count > 0)
        {
            var firstPivot = coreFrames[0].GetProperty("pivot");
            var firstFootprint = coreFrames[0].GetProperty("footprintPx").GetInt32();
            foreach (var coreFrame in coreFrames.Skip(1))
            {
                var pivot = coreFrame.GetProperty("pivot");
                var assetId = coreFrame.GetProperty("assetId").GetString();
                if (Math.Abs(pivot[0].GetDouble() - firstPivot[0].GetDouble()) > 0.001 ||
                    Math.Abs(pivot[1].GetDouble() - firstPivot[1].GetDouble()) > 0.001)
                {
                    problems.Add($"{assetId}: core state pivot differs from idle");
                }

                if (coreFrame.GetProperty("footprintPx").GetInt32() != firstFootprint)
                {
                    problems.Add($"{assetId}: core state footprint differs from idle");
                }
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
                     "seed_core_regenerating.oblique.v0",
                     "seed_core_bootstrap.oblique.v0",
                     "seed_core_besieged.oblique.v0",
                     "worker_frame.oblique.v0",
                     "worker_frame_drill.oblique.v0",
                     "worker_frame_sensor.oblique.v0",
                     "direction_probe.oblique.v0",
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

    private static int? ParseAngle(string relative)
    {
        var name = Path.GetFileNameWithoutExtension(relative);
        var underscore = name.LastIndexOf('_');
        if (underscore < 0)
        {
            return null;
        }

        return int.TryParse(name[(underscore + 1)..], out var angle) ? angle : null;
    }

    private static AlphaInfo DecodeAlpha(byte[] bytes)
    {
        if (bytes.Length < 33 ||
            !bytes.AsSpan(0, 8).SequenceEqual(PngSignature) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException("invalid PNG header");
        }

        var width = ReadBigEndianInt32(bytes, 16);
        var height = ReadBigEndianInt32(bytes, 20);
        var bitDepth = bytes[24];
        var colorType = bytes[25];
        var interlace = bytes[28];
        if (bitDepth != 8 || colorType != 6 || interlace != 0)
        {
            throw new InvalidDataException(
                $"expected 8-bit RGBA non-interlaced, got depth={bitDepth} " +
                $"color={colorType} interlace={interlace}");
        }

        using var idat = new MemoryStream();
        var offset = 8;
        while (offset + 12 <= bytes.Length)
        {
            var length = ReadBigEndianInt32(bytes, offset);
            var kind = bytes.AsSpan(offset + 4, 4);
            if (kind.SequenceEqual("IDAT"u8))
            {
                idat.Write(bytes, offset + 8, length);
            }

            if (kind.SequenceEqual("IEND"u8))
            {
                break;
            }

            offset += 12 + length;
        }

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var rawStream = new MemoryStream();
        zlib.CopyTo(rawStream);
        var raw = rawStream.ToArray();

        var stride = width * 4;
        if (raw.Length != height * (stride + 1))
        {
            throw new InvalidDataException(
                $"unexpected decompressed size {raw.Length} for {width}x{height}");
        }

        var previous = new byte[stride];
        var current = new byte[stride];
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        long opaque = 0;
        var cursor = 0;

        for (var y = 0; y < height; y++)
        {
            var filter = raw[cursor++];
            Buffer.BlockCopy(raw, cursor, current, 0, stride);
            cursor += stride;
            Unfilter(current, previous, filter, 4);
            for (var x = 0; x < width; x++)
            {
                if (current[x * 4 + 3] > 8)
                {
                    opaque++;
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }

            (previous, current) = (current, previous);
        }

        if (maxX < 0)
        {
            throw new InvalidDataException("image is fully transparent");
        }

        return new AlphaInfo(
            width,
            height,
            minX,
            minY,
            maxX,
            maxY,
            opaque / (double)(width * height));
    }

    private static void Unfilter(byte[] row, byte[] previous, int filter, int bpp)
    {
        for (var index = 0; index < row.Length; index++)
        {
            var left = index >= bpp ? row[index - bpp] : 0;
            var up = previous[index];
            var upLeft = index >= bpp ? previous[index - bpp] : 0;
            row[index] = filter switch
            {
                0 => row[index],
                1 => (byte)(row[index] + left),
                2 => (byte)(row[index] + up),
                3 => (byte)(row[index] + ((left + up) >> 1)),
                4 => (byte)(row[index] + Paeth(left, up, upLeft)),
                _ => throw new InvalidDataException($"unknown PNG filter {filter}"),
            };
        }
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var upLeftDistance = Math.Abs(estimate - upLeft);
        if (leftDistance <= upDistance && leftDistance <= upLeftDistance)
        {
            return left;
        }

        return upDistance <= upLeftDistance ? up : upLeft;
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

    private sealed record AlphaInfo(
        int Width,
        int Height,
        int MinX,
        int MinY,
        int MaxX,
        int MaxY,
        double Coverage);
}
