using Godot;

namespace SeedField.Game;

/// <summary>
/// Style sample only: strict top-down tile terrain with visible resource patches,
/// block-aligned structures and rotating top-down units. It is deliberately
/// presentation-only and does not read WorldState. Not final art.
/// </summary>
public partial class MindustryStyle : Node2D
{
    private const int Tile = 64;
    private const int Cols = 20;
    private const int Rows = 10;
    private const float OriginX = 0f;
    private const float OriginY = 56f;

    private bool _capture;
    private int _frames;

    private enum Ore
    {
        None,
        Copper,
        Lead,
        Titanium,
        Coal,
    }

    private enum Zone
    {
        Dirt,
        Sand,
        Ash,
        Rust,
    }

    private readonly record struct Patch(int X, int Y, int Radius, Ore Ore);

    private static readonly Patch[] Patches =
    {
        new(3, 2, 2, Ore.Copper),
        new(4, 7, 2, Ore.Lead),
        new(9, 4, 2, Ore.Titanium),
        new(14, 2, 1, Ore.Coal),
        new(16, 7, 2, Ore.Copper),
        new(11, 8, 1, Ore.Lead),
        new(7, 1, 1, Ore.Coal),
    };

    private readonly record struct Structure(
        int X,
        int Y,
        int W,
        int H,
        string Label,
        string Asset,
        Color Fill,
        Color Accent);

    private static readonly Structure[] Structures =
    {
        new(1, 4, 2, 2, "SEED CORE", "waste_core", new Color("1b2927"), new Color("7fe0cf")),
        new(5, 3, 1, 1, "POWER", "waste_generator", new Color("33291f"), new Color("c9a24a")),
        new(5, 5, 1, 1, "TURRET", "waste_turret", new Color("352a22"), new Color("b5543f")),
        new(8, 2, 1, 1, "DRILL", "waste_drill", new Color("33291f"), new Color("c47a45")),
        new(9, 6, 1, 1, "RELAY", "waste_relay", new Color("2b2a24"), new Color("7fa9a0")),
        new(13, 4, 2, 2, "HOSTILE", "waste_hostile", new Color("301f1a"), new Color("b5543f")),
        new(17, 5, 1, 1, "FOUNDRY", "waste_foundry", new Color("2e2a22"), new Color("a89a85")),
    };

    private readonly record struct Unit(
        float X,
        float Y,
        float AngleDeg,
        Color Color,
        string Label,
        string Asset);

    private static readonly Unit[] Units =
    {
        new(6.4f, 6.8f, -28f, new Color("7fe0cf"), "A1", "waste_scout"),
        new(7.4f, 7.3f, -12f, new Color("7fe0cf"), "A2", "waste_scout"),
        new(11.8f, 3.1f, 180f, new Color("b5543f"), "B1", "waste_sentry"),
    };

    private static readonly (string Asset, float X, float Y)[] Props =
    {
        ("prop_barrels", 3.2f, 8.2f),
        ("prop_sandbags", 6.6f, 5.2f),
        ("prop_scrap", 15.0f, 8.4f),
        ("prop_barrels", 18.4f, 2.2f),
        ("prop_sandbags", 3.8f, 2.0f),
        ("prop_scrap", 8.0f, 8.8f),
        ("prop_barrels", 12.4f, 8.6f),
        ("prop_scrap", 16.4f, 6.6f),
    };

    public override void _Ready()
    {
        _capture = OS.GetCmdlineUserArgs().Contains("--capture-art");
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

        var path = ProjectSettings.GlobalizePath("res://screenshots/art-mindustry-style.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(path);
        GD.Print(error == Error.Ok
            ? $"ART_SCREENSHOT_SAVED {path}"
            : $"ART_SCREENSHOT_FAILED {error}: {path}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 1280, 720), new Color("100e0b"));

        DrawTileMap();
        DrawDebris();
        DrawGridOverlay();
        DrawPatches();
        DrawProps();
        DrawStructures();
        DrawUnits();
        DrawHud();
    }

    private void DrawTileMap()
    {
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Cols; x++)
            {
                var tile = new Rect2(
                    OriginX + (x * Tile),
                    OriginY + (y * Tile),
                    Tile,
                    Tile);
                var terrain = Tex(ZoneAsset(ZoneAt(x, y)));
                if (terrain is not null)
                {
                    DrawTextureRect(terrain, tile, false);
                    if ((Hash(x, y) % 5) == 0)
                    {
                        DrawRect(tile, new Color(0f, 0f, 0f, 0.06f));
                    }

                    continue;
                }

                var shade = Hash(x, y) % 4;
                var ground = ZoneAt(x, y) switch
                {
                    Zone.Rust => shade switch
                    {
                        0 => new Color("3a241a"),
                        1 => new Color("42291d"),
                        2 => new Color("332016"),
                        _ => new Color("3d2619"),
                    },
                    Zone.Sand => shade switch
                    {
                        0 => new Color("3a3123"),
                        1 => new Color("423726"),
                        2 => new Color("342c1f"),
                        _ => new Color("3e3424"),
                    },
                    Zone.Ash => shade switch
                    {
                        0 => new Color("24262a"),
                        1 => new Color("2a2c30"),
                        2 => new Color("212327"),
                        _ => new Color("27292d"),
                    },
                    _ => shade switch
                    {
                        0 => new Color("262019"),
                        1 => new Color("2b241b"),
                        2 => new Color("221d16"),
                        _ => new Color("2f281e"),
                    },
                };
                DrawRect(new Rect2(OriginX + (x * Tile), OriginY + (y * Tile), Tile, Tile), ground);
            }
        }
    }

    private void DrawDebris()
    {
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Cols; x++)
            {
                var hash = Hash(x + 7, y + 13);
                var tileX = OriginX + (x * Tile);
                var tileY = OriginY + (y * Tile);

                if (hash % 5 == 0)
                {
                    var count = 2 + (hash % 3);
                    for (var i = 0; i < count; i++)
                    {
                        var h = Hash(x * 19 + i, y * 23 + i);
                        var px = tileX + 8 + (h % 48);
                        var py = tileY + 8 + ((h / 48) % 48);
                        var size = 1.5f + (i % 3);
                        DrawCircle(
                            new Vector2(px, py),
                            size,
                            new Color("4a4030", 0.65f));
                    }
                }

                if (hash % 11 == 0)
                {
                    var start = new Vector2(tileX + 12, tileY + 38);
                    DrawPolyline(
                        new[]
                        {
                            start,
                            start + new Vector2(14, -9),
                            start + new Vector2(30, 4),
                            start + new Vector2(42, -7),
                        },
                        new Color("17130e", 0.8f),
                        1.5f);
                }

                if (hash % 13 == 0)
                {
                    DrawCircle(
                        new Vector2(tileX + 34, tileY + 30),
                        17f,
                        new Color("5a3a22", 0.14f));
                }

                if (hash % 17 == 0)
                {
                    DrawRock(tileX + 20 + (hash % 22), tileY + 18 + ((hash / 7) % 24), hash);
                }

                if (hash % 23 == 0)
                {
                    DrawScrap(tileX + 16, tileY + 24, hash);
                }
            }
        }
    }

    private void DrawRock(float x, float y, int hash)
    {
        var size = 7f + (hash % 6);
        DrawPolygon(
            new[]
            {
                new Vector2(x - size, y + (size * 0.45f)),
                new Vector2(x - (size * 0.35f), y - size),
                new Vector2(x + (size * 0.65f), y - (size * 0.7f)),
                new Vector2(x + size, y + (size * 0.35f)),
                new Vector2(x, y + (size * 0.8f)),
            },
            new[] { new Color("211d18"), new Color("2e281f"), new Color("3a3327"), new Color("2a241c"), new Color("1a1712") });
        DrawLine(
            new Vector2(x - (size * 0.35f), y - size),
            new Vector2(x + (size * 0.65f), y - (size * 0.7f)),
            new Color("5a4e3c"),
            1.5f);
    }

    private void DrawScrap(float x, float y, int hash)
    {
        var rust = new Color("6b3f26");
        DrawRect(new Rect2(x, y, 18, 7), new Color("3a2a1e"));
        DrawRect(new Rect2(x + 2, y + 1, 14, 4), rust);
        DrawRect(new Rect2(x + 5, y + 8, 12, 8), new Color("2e261d"));
        DrawLine(new Vector2(x + 7, y + 9), new Vector2(x + 15, y + 14), new Color("8a5a38"), 1.5f);
        DrawCircle(new Vector2(x + 4, y + 12), 2f, new Color(0f, 0f, 0f, 0.4f));
        if (hash % 2 == 0)
        {
            DrawLine(new Vector2(x + 16, y + 2), new Vector2(x + 24, y - 3), new Color("4a3a2a"), 1.5f);
        }
    }

    private void DrawGridOverlay()
    {
        var grid = new Color("3a3125", 0.55f);
        for (var x = 0; x <= Cols; x++)
        {
            DrawLine(
                new Vector2(OriginX + (x * Tile), OriginY),
                new Vector2(OriginX + (x * Tile), OriginY + (Rows * Tile)),
                grid,
                1f);
        }

        for (var y = 0; y <= Rows; y++)
        {
            DrawLine(
                new Vector2(OriginX, OriginY + (y * Tile)),
                new Vector2(OriginX + (Cols * Tile), OriginY + (y * Tile)),
                grid,
                1f);
        }

        DrawRect(
            new Rect2(OriginX, OriginY, Cols * Tile, Rows * Tile),
            new Color("4a4030"),
            filled: false,
            width: 2f);
    }

    private void DrawPatches()
    {
        foreach (var patch in Patches)
        {
            var texture = Tex(OreAsset(patch.Ore));
            var center = new Vector2(
                OriginX + ((patch.X + 0.5f) * Tile),
                OriginY + ((patch.Y + 0.5f) * Tile));
            var size = (patch.Radius * 1.4f + 1.2f) * Tile;
            if (texture is null)
            {
                DrawCircle(center, size * 0.35f, OreColor(patch.Ore));
                continue;
            }

            DrawTextureRect(
                texture,
                new Rect2(center - new Vector2(size, size) * 0.5f, new Vector2(size, size)),
                false);
        }
    }

    private void DrawProps()
    {
        foreach (var (asset, x, y) in Props)
        {
            var texture = Tex(asset);
            if (texture is null)
            {
                continue;
            }

            var center = new Vector2(
                OriginX + ((x + 0.5f) * Tile),
                OriginY + ((y + 0.5f) * Tile));
            const float size = 62f;
            DrawTextureRect(
                texture,
                new Rect2(center - new Vector2(size, size) * 0.5f, new Vector2(size, size)),
                false);
        }
    }

    private void DrawStructures()
    {
        foreach (var structure in Structures)
        {
            var rect = new Rect2(
                OriginX + (structure.X * Tile) + 5,
                OriginY + (structure.Y * Tile) + 5,
                (structure.W * Tile) - 10,
                (structure.H * Tile) - 10);
            var texture = Tex(structure.Asset);
            if (texture is not null)
            {
                var size = rect.Size * 1.45f;
                var origin = rect.Position + ((rect.Size - size) * 0.5f);
                DrawTextureRect(
                    texture,
                    new Rect2(origin + new Vector2(4f, 6f), size),
                    false,
                    new Color(0f, 0f, 0f, 0.35f));
                DrawTextureRect(texture, new Rect2(origin, size), false);
            }
            else
            {
                DrawRect(rect.Grow(2), new Color("0b0f12"));
                DrawRect(rect, structure.Fill);
                DrawRect(rect, new Color("06090b"), filled: false, width: 3f);
                DrawRect(
                    rect.Grow(-5),
                    new Color(structure.Accent, 0.16f),
                    filled: false,
                    width: 2f);
                DrawBlockDetail(rect, structure.Accent);
            }

            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(rect.Position.X, rect.End.Y + 15),
                structure.Label,
                HorizontalAlignment.Left,
                -1,
                10,
                new Color(structure.Accent, 0.85f));
        }
    }

    private void DrawBlockDetail(Rect2 rect, Color accent)
    {
        var inner = rect.Grow(-12);
        DrawRect(inner, new Color("1a1510"));
        DrawRect(inner, new Color(accent, 0.45f), filled: false, width: 2f);

        var core = inner.Size * 0.28f;
        DrawRect(
            new Rect2(inner.Position + (inner.Size * 0.36f), core),
            accent);

        for (var i = 0; i < 4; i++)
        {
            var offset = 10f + (i * 9f);
            DrawLine(
                new Vector2(inner.Position.X + offset, inner.Position.Y + 4),
                new Vector2(inner.Position.X + offset, inner.Position.Y + 9),
                new Color(accent, 0.55f),
                2f);
        }
    }

    private void DrawUnits()
    {
        foreach (var unit in Units)
        {
            var position = new Vector2(
                OriginX + (unit.X * Tile),
                OriginY + (unit.Y * Tile));
            var texture = Tex(unit.Asset);
            if (texture is not null)
            {
                const float size = 64f;
                DrawSetTransform(position, Mathf.DegToRad(unit.AngleDeg), Vector2.One);
                DrawTextureRect(
                    texture,
                    new Rect2(-size * 0.5f, -size * 0.5f, size, size),
                    false);
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
            else
            {
                var angle = Mathf.DegToRad(unit.AngleDeg);
                var forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var right = new Vector2(-forward.Y, forward.X);
                var nose = position + (forward * 17f);
                var left = position - (forward * 9f) - (right * 9f);
                var back = position - (forward * 12f);
                var rightPoint = position - (forward * 9f) + (right * 9f);

                DrawPolygon(
                    new[] { nose, rightPoint, back, left },
                    new[] { unit.Color, unit.Color, new Color(unit.Color, 0.75f), unit.Color });
                DrawLine(back, nose, new Color(unit.Color.Lightened(0.35f)), 1.5f);
                DrawCircle(nose, 2.5f, Colors.White);
            }

            DrawString(
                ThemeDB.FallbackFont,
                position + new Vector2(-9, 26),
                unit.Label,
                HorizontalAlignment.Left,
                -1,
                9,
                new Color(unit.Color, 0.8f));
        }
    }

    private void DrawHud()
    {
        DrawRect(new Rect2(0, 0, 1280, 44), new Color("0e0c09"));
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(16, 27),
            "SEED FIELD · WASTELAND MAP SAMPLE",
            HorizontalAlignment.Left,
            -1,
            16,
            new Color("e2d7c3"));

        var x = 610f;
        DrawOreChip(ref x, new Color("c47a45"), "COPPER", "1 240");
        DrawOreChip(ref x, new Color("8e8494"), "LEAD", "860");
        DrawOreChip(ref x, new Color("78a8a0"), "TITANIUM", "410");
        DrawOreChip(ref x, new Color("4b4842"), "COAL", "1 980");

        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(16, 712),
            "POST-APOCALYPTIC WASTELAND · STRICT TOP-DOWN · 64 px TILE GRID · DEMO DATA · NOT FINAL ART",
            HorizontalAlignment.Left,
            -1,
            11,
            new Color("9a8c78"));
    }

    private void DrawOreChip(ref float x, Color color, string label, string amount)
    {
        DrawRect(new Rect2(x, 13, 14, 14), new Color(color, 0.22f));
        DrawRect(new Rect2(x + 3, 16, 8, 8), color);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(x + 22, 26),
            $"{label} {amount}",
            HorizontalAlignment.Left,
            -1,
            11,
            new Color(color.Lightened(0.25f)));
        x += 165f;
    }

    private static Zone ZoneAt(int x, int y)
    {
        if (x <= 6 && y >= 5)
        {
            return Zone.Rust;
        }

        if (x >= 14)
        {
            return Zone.Sand;
        }

        if (y <= 2)
        {
            return Zone.Ash;
        }

        return Zone.Dirt;
    }

    private static Ore OreAt(int x, int y)
    {
        foreach (var patch in Patches)
        {
            var dx = x - patch.X;
            var dy = y - patch.Y;
            if ((dx * dx) + (dy * dy) <= patch.Radius * patch.Radius)
            {
                return patch.Ore;
            }
        }

        return Ore.None;
    }

    private static Color OreColor(Ore ore) =>
        ore switch
        {
            Ore.Copper => new Color("c47a45"),
            Ore.Lead => new Color("8e8494"),
            Ore.Titanium => new Color("78a8a0"),
            Ore.Coal => new Color("4b4842"),
            _ => Colors.Transparent,
        };

    private static int Hash(int x, int y)
    {
        var h = (uint)((x * 374761393) + (y * 668265263));
        h = (h ^ (h >> 13)) * 1274126177u;
        return (int)((h ^ (h >> 16)) & 0x7fffffff);
    }
}
