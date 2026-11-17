using System.Text.Json;
using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public sealed record VisualAssetFrame(
    Texture2D Diffuse,
    Texture2D? Normal,
    Texture2D? Emissive,
    Vector2 Pivot,
    float Footprint,
    int Angle);

/// <summary>
/// Read-only view of Art/manifest.json. Presentation code asks for an asset and a
/// direction; it never walks WorldState or writes any file.
/// </summary>
public sealed class VisualAssetCatalog
{
    private readonly Dictionary<string, List<VisualAssetFrame>> _frames =
        new(StringComparer.Ordinal);

    public static VisualAssetCatalog Load(
        string route = "oblique",
        IReadOnlySet<string>? only = null)
    {
        var catalog = new VisualAssetCatalog();
        const string manifestPath = "res://Art/manifest.json";
        if (!Godot.FileAccess.FileExists(manifestPath))
        {
            GD.PushError($"Visual manifest missing: {manifestPath}");
            return catalog;
        }

        using var document = JsonDocument.Parse(
            Godot.FileAccess.GetFileAsString(manifestPath));
        foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("route").GetString() != route)
            {
                continue;
            }

            var id = asset.GetProperty("assetId").GetString() ?? string.Empty;
            var name = id.Split('.')[0];
            if (only is not null && !only.Contains(name))
            {
                continue;
            }


            var pivotArray = asset.GetProperty("pivot");
            var pivot = new Vector2(
                (float)pivotArray[0].GetDouble(),
                (float)pivotArray[1].GetDouble());
            var footprint = (float)asset.GetProperty("footprintPx").GetDouble();
            var frames = new List<VisualAssetFrame>();

            foreach (var file in asset.GetProperty("files").EnumerateArray())
            {
                var relative = file.GetProperty("path").GetString() ?? string.Empty;
                var diffuse = Godot.GD.Load<Texture2D>($"res://Art/{relative}");
                if (diffuse is null)
                {
                    GD.PushError($"Visual asset failed to load: res://Art/{relative}");
                    continue;
                }

                frames.Add(new VisualAssetFrame(
                    diffuse,
                    LoadPass(file, "normal"),
                    LoadPass(file, "emissive"),
                    pivot,
                    footprint,
                    ParseAngle(relative)));
            }

            if (frames.Count > 0)
            {
                frames.Sort((left, right) => left.Angle.CompareTo(right.Angle));
                catalog._frames[name] = frames;
            }
        }

        return catalog;
    }

    public VisualAssetFrame? Get(string name, int direction = 0)
    {
        if (!_frames.TryGetValue(name, out var frames) || frames.Count == 0)
        {
            return null;
        }

        if (frames.Count == 1)
        {
            return frames[0];
        }

        var index = frames.Count == 8
            ? Mathf.PosMod(direction, 8)
            : Mathf.PosMod(direction * frames.Count / 8, frames.Count);
        return frames[index];
    }

    public VisualAssetFrame? GetCore(CoreVisualState state) => state switch
    {
        CoreVisualState.Absent => null,
        CoreVisualState.Regenerating => Get("seed_core_regenerating"),
        CoreVisualState.Bootstrap => Get("seed_core_bootstrap"),
        CoreVisualState.Besieged => Get("seed_core_besieged"),
        _ => Get("seed_core"),
    };

    private static Texture2D? LoadPass(JsonElement file, string passName)
    {
        if (!file.TryGetProperty(passName, out var pass))
        {
            return null;
        }

        var relative = pass.GetProperty("path").GetString();
        return string.IsNullOrEmpty(relative)
            ? null
            : Godot.GD.Load<Texture2D>($"res://Art/{relative}");
    }

    private static int ParseAngle(string relative)
    {
        var stem = System.IO.Path.GetFileNameWithoutExtension(relative);
        var underscore = stem.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(stem[(underscore + 1)..], out var angle)
            ? angle
            : 0;
    }
}
