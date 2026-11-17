using Godot;

namespace SeedField.Game;

/// <summary>
/// A/B camera style test. Presentation-only: it never reads or mutates WorldState.
/// Renders three Blender assets on a procedural industrial terrain with default,
/// selected, tactical and debug overlays at far/mid/near zoom.
/// </summary>
public partial class StyleTest : Node2D
{
    private sealed record Asset(
        string Label,
        Vector2 Position,
        float BaseSize,
        Texture2D Texture,
        string Id);

    private readonly List<Asset> _assets = new();
    private string _route = "oblique";
    private string _zoom = "mid";
    private string _overlay = "default";
    private float _scale = 1f;
    private bool _capture;
    private int _frames;

    public override void _Ready()
    {
        ParseArgs();
        LoadAssets();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_capture)
        {
            return;
        }

        _frames++;
        if (_frames < 4)
        {
            return;
        }

        var path = ProjectSettings.GlobalizePath(
            $"res://screenshots/art-{_route}-{_zoom}-{_overlay}.png");
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"ART_SCREENSHOT_SAVED {path}");
        GetTree().Quit();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 1280, 720), new Color("0b1220"));
        DrawTerrain();
        DrawNetworks();

        var ordered = _assets
            .Select(asset => (Asset: asset, Y: asset.Position.Y))
            .OrderBy(item => item.Y)
            .Select(item => item.Asset)
            .ToList();

        foreach (var asset in ordered)
        {
            DrawAsset(asset);
        }

        if (_overlay == "selected")
        {
            DrawSelection(_assets.Single(asset => asset.Label == "POWER NODE"));
        }

        DrawHud();
        if (_overlay == "debug")
        {
            DrawDebug();
        }
    }

    private void ParseArgs()
    {
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--route=", StringComparison.Ordinal))
            {
                _route = argument["--route=".Length..];
            }
            else if (argument.StartsWith("--zoom=", StringComparison.Ordinal))
            {
                _zoom = argument["--zoom=".Length..];
            }
            else if (argument.StartsWith("--overlay=", StringComparison.Ordinal))
            {
                _overlay = argument["--overlay=".Length..];
            }
            else if (argument == "--capture-art")
            {
                _capture = true;
            }
        }

        _scale = _zoom switch
        {
            "far" => 0.62f,
            "near" => 1.65f,
            _ => 1.0f,
        };
    }

    private void LoadAssets()
    {
        _assets.Add(new Asset(
            "SEED CORE",
            new Vector2(640, 390),
            260f,
            Load($"res://Art/style_{_route}/seed_core_{_route}_000.png"),
            "CORE"));
        _assets.Add(new Asset(
            "POWER NODE",
            new Vector2(420, 325),
            190f,
            Load($"res://Art/style_{_route}/power_node_{_route}_000.png"),
            "P-01"));
        _assets.Add(new Asset(
            "WORKER",
            new Vector2(765, 445),
            120f,
            Load($"res://Art/style_{_route}/worker_frame_{_route}_000.png"),
            "U-07"));
    }

    private static Texture2D Load(string path) => GD.Load<Texture2D>(path);

    private void DrawTerrain()
    {
        var random = new RandomNumberGenerator { Seed = 19 };
        var plates = new[]
        {
            new Rect2(60, 120, 300, 210),
            new Rect2(330, 80, 260, 250),
            new Rect2(560, 130, 330, 230),
            new Rect2(860, 90, 340, 270),
            new Rect2(120, 360, 260, 250),
            new Rect2(360, 330, 330, 230),
            new Rect2(700, 380, 280, 220),
            new Rect2(930, 350, 260, 250),
        };

        foreach (var plate in plates)
        {
            var shade = new Color(
                0.075f + random.Randf() * 0.03f,
                0.10f + random.Randf() * 0.035f,
                0.13f + random.Randf() * 0.04f,
                1f);
            DrawRect(plate, shade);
            DrawRect(plate, new Color(0.18f, 0.28f, 0.36f, 0.35f), false, 1.2f);
        }

        // Circuit traces and industrial seams.
        var trace = new Color(0.20f, 0.42f, 0.48f, 0.55f);
        var seam = new Color(0.18f, 0.22f, 0.27f, 0.75f);
        for (var index = 0; index < 22; index++)
        {
            var start = new Vector2(
                random.RandfRange(80, 1200),
                random.RandfRange(100, 640));
            var horizontal = random.Randf() > 0.5f;
            var end = horizontal
                ? start + new Vector2(random.RandfRange(30, 150), 0)
                : start + new Vector2(0, random.RandfRange(30, 120));
            DrawLine(start, end, trace, 1.2f);
            DrawCircle(end, 2.2f, trace);
        }

        for (var x = 80; x < 1240; x += 160)
        {
            DrawLine(new Vector2(x, 90), new Vector2(x, 670), seam, 1f);
        }

        for (var y = 110; y < 680; y += 120)
        {
            DrawLine(new Vector2(60, y), new Vector2(1220, y), seam, 1f);
        }

        // Ruined slabs and hazard strips.
        for (var index = 0; index < 9; index++)
        {
            var position = new Vector2(
                random.RandfRange(90, 1160),
                random.RandfRange(120, 610));
            var size = new Vector2(
                random.RandfRange(18, 46),
                random.RandfRange(10, 26));
            DrawRect(new Rect2(position, size), new Color(0.16f, 0.18f, 0.20f, 0.9f));
            DrawLine(
                position,
                position + size,
                new Color(0.45f, 0.30f, 0.18f, 0.7f),
                1.4f);
        }
    }

    private void DrawNetworks()
    {
        var core = _assets.Single(asset => asset.Label == "SEED CORE").Position;
        var power = _assets.Single(asset => asset.Label == "POWER NODE").Position;
        var worker = _assets.Single(asset => asset.Label == "WORKER").Position;

        if (_overlay is "tactical" or "debug")
        {
            DrawLine(core, power, new Color(0.25f, 0.95f, 1f, 0.85f), 2.4f);
            DrawLine(core, worker, new Color(0.62f, 0.42f, 1f, 0.75f), 2f);
            for (var t = 0.15f; t < 0.95f; t += 0.12f)
            {
                var point = power.Lerp(core, t);
                DrawCircle(point, 3.2f, new Color(0.55f, 1f, 1f, 0.9f));
            }

            DrawArc(power, 120f * _scale, 0, Mathf.Tau, 48, new Color(0.3f, 0.9f, 1f, 0.35f), 1.6f);
            DrawArc(worker, 150f * _scale, 0, Mathf.Tau, 48, new Color(0.75f, 0.55f, 1f, 0.35f), 1.6f);
            DrawArc(core, 210f * _scale, 0, Mathf.Tau, 64, new Color(1f, 0.75f, 0.35f, 0.25f), 1.8f);

            DrawLine(power, new Vector2(1120, 190), new Color(1f, 0.55f, 0.20f, 0.65f), 1.6f);
            DrawColoredPolygon(
                new[]
                {
                    new Vector2(1120, 190),
                    new Vector2(1100, 214),
                    new Vector2(1140, 214),
                },
                new Color(1f, 0.55f, 0.20f, 0.9f));
            Label("HOSTILE CONTACT", new Vector2(1040, 240), 11, new Color(1f, 0.65f, 0.35f));
        }
        else
        {
            DrawLine(core, power, new Color(0.20f, 0.70f, 0.78f, 0.35f), 1.6f);
            DrawLine(core, worker, new Color(0.50f, 0.35f, 0.85f, 0.30f), 1.4f);
        }
    }

    private void DrawAsset(Asset asset)
    {
        var size = asset.BaseSize * _scale;
        var rect = new Rect2(asset.Position - new Vector2(size, size) / 2f, new Vector2(size, size));

        // Contact shadow: presentation only, not a hit test.
        DrawCircle(asset.Position + new Vector2(0, size * 0.34f), size * 0.28f, new Color(0, 0, 0, 0.35f));
        DrawTextureRect(asset.Texture, rect, false);

        if (_overlay is not "debug")
        {
            Label(asset.Label, asset.Position + new Vector2(-size * 0.5f, size * 0.52f), 11, new Color(0.72f, 0.82f, 0.90f));
        }
    }

    private void DrawSelection(Asset asset)
    {
        var size = asset.BaseSize * _scale;
        DrawArc(asset.Position, size * 0.62f, 0, Mathf.Tau, 64, new Color(0.35f, 0.95f, 1f, 0.9f), 2.2f);
        DrawLine(
            asset.Position + new Vector2(size * 0.42f, -size * 0.42f),
            asset.Position + new Vector2(size * 0.62f, -size * 0.62f),
            new Color(0.35f, 0.95f, 1f, 0.8f),
            1.4f);
        var panel = new Rect2(asset.Position + new Vector2(size * 0.45f, -size * 0.55f), new Vector2(180, 62));
        DrawRect(panel, new Color(0.04f, 0.07f, 0.11f, 0.92f));
        DrawRect(panel, new Color(0.25f, 0.55f, 0.65f, 0.8f), false, 1f);
        Label("POWER NODE", panel.Position + new Vector2(10, 8), 12, new Color(0.75f, 0.90f, 0.95f));
        Label("GRID 500/500", panel.Position + new Vector2(10, 28), 11, new Color(0.45f, 0.75f, 0.80f));
        Label("STATUS ONLINE", panel.Position + new Vector2(10, 46), 11, new Color(0.45f, 0.95f, 0.55f));
    }

    private void DrawHud()
    {
        DrawRect(new Rect2(0, 0, 1280, 78), new Color(0.03f, 0.06f, 0.10f, 0.92f));
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(28, 34),
            "SEED FIELD / STYLE TEST",
            HorizontalAlignment.Left,
            -1,
            22,
            new Color(0.82f, 0.90f, 0.96f));
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(28, 58),
            $"route {_route} · zoom {_zoom} · overlay {_overlay} · Blender 5.2 / Cycles · not final art",
            HorizontalAlignment.Left,
            -1,
            12,
            new Color(0.45f, 0.58f, 0.68f));
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(980, 34),
            "A / B CAMERA TEST",
            HorizontalAlignment.Left,
            -1,
            13,
            new Color(0.55f, 0.68f, 0.78f));
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(980, 54),
            "default · selected · tactical · debug",
            HorizontalAlignment.Left,
            -1,
            11,
            new Color(0.45f, 0.58f, 0.68f));
    }

    private void DrawDebug()
    {
        DrawRect(new Rect2(60, 96, 310, 150), new Color(0.03f, 0.06f, 0.10f, 0.90f));
        DrawRect(new Rect2(60, 96, 310, 150), new Color(0.25f, 0.55f, 0.65f, 0.8f), false, 1f);
        Label("DEBUG OVERLAY", new Vector2(76, 106), 13, new Color(0.75f, 0.90f, 0.95f));
        Label("tick 240 · entities 3 · faults 0", new Vector2(76, 130), 11, new Color(0.55f, 0.68f, 0.78f));
        Label("CORE  core-a  E100/100 M100/100", new Vector2(76, 152), 11, new Color(0.55f, 0.68f, 0.78f));
        Label("P-01  grid 500/500  gen 4/t", new Vector2(76, 174), 11, new Color(0.55f, 0.68f, 0.78f));
        Label("U-07  energy 100/100  intent wait", new Vector2(76, 196), 11, new Color(0.55f, 0.68f, 0.78f));
        Label("visuals are event-driven; no VFX mutation", new Vector2(76, 220), 10, new Color(0.40f, 0.55f, 0.65f));
    }

    private void Label(string text, Vector2 position, int size, Color color)
    {
        DrawString(
            ThemeDB.FallbackFont,
            position,
            text,
            HorizontalAlignment.Left,
            -1,
            size,
            color);
    }
}
