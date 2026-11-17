using Godot;

namespace SeedField.Game;

/// <summary>
/// World-space style-test scene. Terrain, devices, networks and the camera all use
/// the same coordinates, so far/mid/near captures exercise the real zoom model.
/// Presentation only; no WorldState is read or written.
/// </summary>
public partial class StyleTestWorld : Node2D
{
    private const float WorldWidth = 2400f;
    private const float WorldHeight = 1500f;

    private sealed record StyleAsset(
        string Id,
        string Label,
        Vector2 Position,
        float Footprint,
        Vector2 Pivot,
        float PadRadius,
        bool HasPad,
        Texture2D Texture);

    private readonly List<StyleAsset> _assets = new();

    public string Route { get; init; } = "oblique";
    public string Overlay { get; init; } = "default";
    public string Zoom { get; init; } = "mid";

    public Vector2 FocusPoint => new(1200f, 700f);

    public float CameraZoom => Zoom switch
    {
        "far" => 0.62f,
        "near" => 1.65f,
        _ => 1f,
    };

    public override void _Ready()
    {
        _assets.Clear();
        LoadManifest();
        QueueRedraw();
    }

    private void LoadManifest()
    {
        const string manifestPath = "res://Art/manifest.json";
        if (!Godot.FileAccess.FileExists(manifestPath))
        {
            GD.PushError($"Style test manifest missing: {manifestPath}");
            return;
        }

        var parsed = Json.ParseString(Godot.FileAccess.GetFileAsString(manifestPath));
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError("Style test manifest is not a JSON object.");
            return;
        }

        var root = parsed.AsGodotDictionary();
        if (!root.ContainsKey("assets"))
        {
            GD.PushError("Style test manifest has no assets array.");
            return;
        }

        foreach (var item in root["assets"].AsGodotArray())
        {
            var entry = item.AsGodotDictionary();
            if (entry["route"].AsString() != Route)
            {
                continue;
            }

            var assetId = entry["assetId"].AsString();
            var name = assetId.Split('.')[0];
            var files = entry["files"].AsGodotArray();
            if (files.Count == 0)
            {
                continue;
            }

            var relative = files[0].AsGodotDictionary()["path"].AsString();
            var texture = GD.Load<Texture2D>($"res://Art/{relative}");
            if (texture is null)
            {
                GD.PushError($"Style asset failed to load: res://Art/{relative}");
                continue;
            }

            var pivotArray = entry["pivot"].AsGodotArray();
            var pivot = new Vector2(
                (float)pivotArray[0].AsDouble(),
                (float)pivotArray[1].AsDouble());
            var footprint = (float)entry["footprintPx"].AsDouble();
            var position = name switch
            {
                "seed_core" => new Vector2(1200f, 780f),
                "power_node" => new Vector2(880f, 690f),
                _ => new Vector2(1520f, 910f),
            };

            _assets.Add(new StyleAsset(
                assetId,
                name.Replace('_', ' ').ToUpperInvariant(),
                position,
                footprint,
                pivot,
                footprint * 0.34f,
                name != "seed_core",
                texture));
        }

        _assets.Sort((left, right) => left.Position.Y.CompareTo(right.Position.Y));
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, WorldWidth, WorldHeight), new Color("0b1220"));
        DrawTerrain();
        DrawRoads();
        DrawPlaza();
        DrawNetworks();

        foreach (var asset in _assets.OrderBy(asset => asset.Position.Y))
        {
            DrawAsset(asset);
        }

        if (Overlay == "selected")
        {
            var power = _assets.FirstOrDefault(asset => asset.Id.StartsWith("power_node", StringComparison.Ordinal));
            if (power is not null)
            {
                DrawSelection(power);
            }
        }

        if (Overlay == "tactical")
        {
            DrawTacticalOverlay();
        }

        if (Overlay == "debug")
        {
            DrawDebugOverlay();
        }
    }

    private void DrawTerrain()
    {
        var random = new RandomNumberGenerator { Seed = 19 };
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 6; column++)
            {
                if ((column == 1 && row == 3) || (column == 4 && row == 1))
                {
                    continue;
                }

                var plate = new Rect2(
                    24f + column * 394f,
                    24f + row * 368f,
                    362f,
                    334f);
                var tone = random.Randf();
                var shade = new Color(
                    0.072f + tone * 0.020f,
                    0.098f + tone * 0.024f,
                    0.128f + tone * 0.030f,
                    1f);
                DrawRect(plate, shade);
                DrawRect(plate, new Color(0.18f, 0.28f, 0.36f, 0.34f), false, 1.4f);
                DrawRect(
                    plate.Grow(-7f),
                    new Color(0.24f, 0.36f, 0.44f, 0.16f),
                    false,
                    1f);
            }
        }

        var seam = new Color(0.17f, 0.21f, 0.26f, 0.72f);
        for (var x = 0f; x <= WorldWidth; x += 160f)
        {
            DrawLine(new Vector2(x, 0), new Vector2(x, WorldHeight), seam, 1f);
        }

        for (var y = 0f; y <= WorldHeight; y += 160f)
        {
            DrawLine(new Vector2(0, y), new Vector2(WorldWidth, y), seam, 1f);
        }

        var trace = new Color(0.20f, 0.42f, 0.48f, 0.48f);
        for (var index = 0; index < 34; index++)
        {
            var start = new Vector2(
                random.RandfRange(70f, WorldWidth - 70f),
                random.RandfRange(70f, WorldHeight - 70f));
            var horizontal = random.Randf() > 0.5f;
            var end = horizontal
                ? start + new Vector2(random.RandfRange(36f, 150f), 0)
                : start + new Vector2(0, random.RandfRange(36f, 120f));
            DrawLine(start, end, trace, 1.4f);
            DrawCircle(end, 2.4f, trace);
        }

        for (var index = 0; index < 16; index++)
        {
            var position = new Vector2(
                random.RandfRange(90f, WorldWidth - 120f),
                random.RandfRange(90f, WorldHeight - 90f));
            var size = new Vector2(
                random.RandfRange(22f, 58f),
                random.RandfRange(12f, 32f));
            DrawRect(new Rect2(position, size), new Color(0.15f, 0.17f, 0.19f, 0.92f));
            DrawLine(
                position,
                position + size,
                new Color(0.45f, 0.30f, 0.18f, 0.65f),
                1.6f);
        }
    }

    private void DrawRoads()
    {
        var core = new Vector2(1200f, 780f);
        foreach (var target in new[] { new Vector2(880f, 690f), new Vector2(1520f, 910f) })
        {
            DrawLine(core, target, new Color(0.045f, 0.065f, 0.085f, 0.95f), 34f);
            DrawLine(core, target, new Color(0.22f, 0.30f, 0.36f, 0.55f), 2f);
            for (var t = 0.12f; t < 0.92f; t += 0.10f)
            {
                var point = core.Lerp(target, t);
                DrawLine(
                    point - new Vector2(8f, 0),
                    point + new Vector2(8f, 0),
                    new Color(0.55f, 0.42f, 0.20f, 0.45f),
                    3f);
            }
        }
    }

    private void DrawPlaza()
    {
        var center = new Vector2(1200f, 780f);
        DrawColoredPolygon(Octagon(center, 360f, 210f), new Color(0.045f, 0.065f, 0.090f, 0.94f));
        DrawColoredPolygon(Octagon(center, 315f, 184f), new Color(0.070f, 0.095f, 0.122f, 0.96f));
        DrawColoredPolygon(Octagon(center, 250f, 146f), new Color(0.055f, 0.075f, 0.098f, 0.96f));

        DrawArc(center, 300f, 0, Mathf.Tau, 96, new Color(0.62f, 0.42f, 0.16f, 0.55f), 3f);
        DrawArc(center, 232f, 0, Mathf.Tau, 96, new Color(0.30f, 0.62f, 0.68f, 0.40f), 2f);
        DrawArc(center, 168f, 0, Mathf.Tau, 96, new Color(0.80f, 0.62f, 0.30f, 0.35f), 2f);

        for (var index = 0; index < 5; index++)
        {
            var angle = Mathf.DegToRad(18f + index * 72f);
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var inner = center + direction * 170f;
            var outer = center + direction * 330f;
            DrawLine(inner, outer, new Color(0.36f, 0.28f, 0.16f, 0.75f), 10f);
            DrawCircle(outer, 15f, new Color(0.05f, 0.07f, 0.09f, 0.95f));
            DrawArc(
                outer,
                15f,
                0,
                Mathf.Tau,
                24,
                index % 2 == 0
                    ? new Color(0.30f, 0.90f, 1f, 0.75f)
                    : new Color(1f, 0.64f, 0.22f, 0.75f),
                2.4f);
        }
    }

    private void DrawNetworks()
    {
        var core = new Vector2(1200f, 780f);
        var power = new Vector2(880f, 690f);
        var worker = new Vector2(1520f, 910f);
        var tactical = Overlay is "tactical" or "debug";

        DrawLine(
            core,
            power,
            tactical
                ? new Color(0.25f, 0.95f, 1f, 0.85f)
                : new Color(0.20f, 0.70f, 0.78f, 0.32f),
            tactical ? 4f : 2.4f);
        DrawLine(
            core,
            worker,
            tactical
                ? new Color(0.62f, 0.42f, 1f, 0.75f)
                : new Color(0.50f, 0.35f, 0.85f, 0.28f),
            tactical ? 3f : 2f);

        for (var t = 0.12f; t < 0.95f; t += 0.09f)
        {
            var point = power.Lerp(core, t);
            DrawCircle(point, tactical ? 6f : 3.6f, new Color(0.55f, 1f, 1f, 0.75f));
        }

        for (var t = 0.16f; t < 0.92f; t += 0.11f)
        {
            var point = worker.Lerp(core, t);
            DrawRect(
                new Rect2(point - new Vector2(4f, 4f), new Vector2(8f, 8f)),
                new Color(0.68f, 0.50f, 1f, 0.70f));
        }
    }

    private void DrawAsset(StyleAsset asset)
    {
        var size = asset.Footprint;
        var rect = new Rect2(
            asset.Position - asset.Pivot * size,
            new Vector2(size, size));

        if (asset.HasPad)
        {
            DrawPad(asset);
        }

        // Key light comes from upper-right in the Blender rig; shadows fall left/down.
        DrawEllipse(
            asset.Position + new Vector2(-size * 0.13f, size * 0.03f),
            size * 0.31f,
            size * 0.12f,
            new Color(0f, 0f, 0f, 0.18f));
        DrawEllipse(
            asset.Position + new Vector2(-size * 0.09f, size * 0.015f),
            size * 0.21f,
            size * 0.075f,
            new Color(0f, 0f, 0f, 0.30f));

        DrawTextureRect(asset.Texture, rect, false);

        if (Overlay != "debug")
        {
            var labelOffset = asset.Pivot.Y > 0.7f
                ? new Vector2(-size * 0.34f, 26f)
                : new Vector2(-size * 0.20f, size * 0.36f);
            DrawString(
                ThemeDB.FallbackFont,
                asset.Position + labelOffset,
                asset.Label,
                HorizontalAlignment.Left,
                -1,
                14,
                new Color(0.72f, 0.82f, 0.90f));
        }
    }

    private void DrawPad(StyleAsset asset)
    {
        var pad = Octagon(asset.Position, asset.PadRadius, asset.PadRadius * 0.42f);
        DrawColoredPolygon(pad, new Color(0.035f, 0.050f, 0.065f, 0.98f));
        DrawPolyline(Closed(pad), new Color(0.30f, 0.38f, 0.44f, 0.75f), 2.2f);
        var inner = Octagon(asset.Position, asset.PadRadius * 0.72f, asset.PadRadius * 0.30f);
        DrawPolyline(Closed(inner), new Color(0.20f, 0.50f, 0.56f, 0.40f), 1.4f);
    }

    private void DrawSelection(StyleAsset asset)
    {
        var radius = asset.Footprint * 0.50f;
        DrawArc(
            asset.Position,
            radius,
            0,
            Mathf.Tau,
            64,
            new Color(0.35f, 0.95f, 1f, 0.90f),
            2.6f);
        DrawLine(
            asset.Position + new Vector2(radius * 0.70f, -radius * 0.70f),
            asset.Position + new Vector2(radius * 1.05f, -radius * 1.05f),
            new Color(0.35f, 0.95f, 1f, 0.80f),
            1.6f);
        DrawCircle(
            asset.Position + new Vector2(radius * 1.05f, -radius * 1.05f),
            4f,
            new Color(0.35f, 0.95f, 1f, 0.95f));
    }

    private void DrawTacticalOverlay()
    {
        DrawArc(new Vector2(880f, 690f), 180f, 0, Mathf.Tau, 64, new Color(0.3f, 0.9f, 1f, 0.40f), 2f);
        DrawArc(new Vector2(1520f, 910f), 210f, 0, Mathf.Tau, 64, new Color(0.75f, 0.55f, 1f, 0.40f), 2f);
        DrawArc(new Vector2(1200f, 780f), 300f, 0, Mathf.Tau, 96, new Color(1f, 0.75f, 0.35f, 0.30f), 2f);

        var hostile = new Vector2(1700f, 500f);
        DrawLine(
            new Vector2(880f, 690f),
            hostile,
            new Color(1f, 0.55f, 0.20f, 0.55f),
            2f);
        DrawColoredPolygon(
            new[]
            {
                hostile,
                hostile + new Vector2(-20f, 30f),
                hostile + new Vector2(20f, 30f),
            },
            new Color(1f, 0.55f, 0.20f, 0.90f));
        DrawString(
            ThemeDB.FallbackFont,
            hostile + new Vector2(-44f, 58f),
            "HOSTILE CONTACT",
            HorizontalAlignment.Left,
            -1,
            14,
            new Color(1f, 0.65f, 0.35f));
    }

    private void DrawDebugOverlay()
    {
        foreach (var asset in _assets)
        {
            var size = asset.Footprint;
            var rect = new Rect2(
                asset.Position - asset.Pivot * size,
                new Vector2(size, size));
            DrawRect(rect, new Color(0.35f, 0.95f, 1f, 0.45f), false, 1.4f);
            DrawLine(
                asset.Position - new Vector2(14f, 0),
                asset.Position + new Vector2(14f, 0),
                new Color(1f, 0.55f, 0.20f, 0.95f),
                2f);
            DrawLine(
                asset.Position - new Vector2(0, 14f),
                asset.Position + new Vector2(0, 14f),
                new Color(1f, 0.55f, 0.20f, 0.95f),
                2f);
            DrawString(
                ThemeDB.FallbackFont,
                asset.Position + new Vector2(18f, -12f),
                $"{asset.Id} pivot {asset.Pivot.X:0.00},{asset.Pivot.Y:0.00}",
                HorizontalAlignment.Left,
                -1,
                13,
                new Color(0.90f, 0.72f, 0.40f));
        }
    }

    private static Vector2[] Closed(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];
        points.CopyTo(closed, 0);
        closed[^1] = points[0];
        return closed;
    }

    private static Vector2[] Octagon(Vector2 center, float radiusX, float radiusY)
    {
        var points = new Vector2[8];
        for (var index = 0; index < points.Length; index++)
        {
            var angle = Mathf.DegToRad(22.5f + index * 45f);
            points[index] = center + new Vector2(
                Mathf.Cos(angle) * radiusX,
                Mathf.Sin(angle) * radiusY);
        }

        return points;
    }

    private void DrawEllipse(
        Vector2 center,
        float radiusX,
        float radiusY,
        Color color)
    {
        const int segments = 36;
        var points = new Vector2[segments];
        for (var index = 0; index < segments; index++)
        {
            var angle = Mathf.Tau * index / segments;
            points[index] = center + new Vector2(
                Mathf.Cos(angle) * radiusX,
                Mathf.Sin(angle) * radiusY);
        }

        DrawColoredPolygon(points, color);
    }
}
