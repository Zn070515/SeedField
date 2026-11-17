using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Static industrial-wasteland backdrop for the frontier map. It only reads map
/// constants and control-point positions; no simulation state is mutated.
/// </summary>
public static class WorldBackdrop
{
    public const int WidthMm = Scenarios.WidthMm;
    public const int HeightMm = Scenarios.HeightMm;

    // Seeded once; each draw resets the seed so the backdrop stays deterministic
    // without allocating a Godot object per frame.
    private static readonly RandomNumberGenerator PlateRandom = new();
    private static readonly RandomNumberGenerator RuinRandom = new();

    public static void Draw(
        CanvasItem canvas,
        Func<Vec2, Vector2> map,
        float mapScale,
        IReadOnlyList<(Vec2 Position, int RadiusMm, string? Owner)> controlPoints,
        Texture2D? ground = null,
        Texture2D? road = null)
    {
        var origin = map(new Vec2(0, 0));
        var size = new Vector2(WidthMm * mapScale, HeightMm * mapScale);
        canvas.DrawRect(new Rect2(origin, size), new Color(0.055f, 0.075f, 0.100f, 1f));
        if (ground is not null)
        {
            var tile = 140f * (mapScale / 0.055f);
            for (var y = 0f; y < size.Y; y += tile)
            {
                for (var x = 0f; x < size.X; x += tile)
                {
                    var drawSize = new Vector2(
                        Mathf.Min(tile, size.X - x),
                        Mathf.Min(tile, size.Y - y));
                    canvas.DrawTextureRect(
                        ground,
                        new Rect2(origin + new Vector2(x, y), drawSize),
                        false,
                        new Color(1f, 1f, 1f, 0.92f));
                }
            }
        }

        DrawPlates(canvas, map, mapScale);
        DrawRoadTexture(canvas, map, mapScale, road);
        DrawRoads(canvas, map, mapScale);
        DrawRuins(canvas, map, mapScale);
        DrawControlPads(canvas, map, mapScale, controlPoints);
        DrawCornerHazards(canvas, map, mapScale);

        canvas.DrawString(
            ThemeDB.FallbackFont,
            origin + new Vector2(10, 20),
            "SECTOR 07 · INDUSTRIAL WASTE",
            HorizontalAlignment.Left,
            -1,
            9,
            new Color(0.44f, 0.56f, 0.64f, 0.85f));
    }

    private static void DrawPlates(
        CanvasItem canvas,
        Func<Vec2, Vector2> map,
        float mapScale)
    {
        PlateRandom.Seed = 23;
        var random = PlateRandom;
        const int columns = 8;
        const int rows = 4;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if ((column == 2 && row == 1) || (column == 6 && row == 2))
                {
                    continue;
                }

                var x = column * 2_000 + 90;
                var y = row * 2_000 + 90;
                var plate = new Rect2(
                    map(new Vec2((int)x, (int)y)),
                    new Vector2(1_820 * mapScale, 1_820 * mapScale));
                var tone = random.Randf();
                var fill = new Color(
                    0.075f + tone * 0.020f,
                    0.095f + tone * 0.022f,
                    0.115f + tone * 0.026f,
                    0.32f);
                canvas.DrawRect(plate, fill);
                canvas.DrawRect(
                    plate,
                    new Color(0.20f, 0.30f, 0.38f, 0.24f),
                    false,
                    1f);
                canvas.DrawRect(
                    plate.Grow(-6f),
                    new Color(0.30f, 0.42f, 0.48f, 0.10f),
                    false,
                    1f);
            }
        }
    }

    private static void DrawRoadTexture(
        CanvasItem canvas,
        Func<Vec2, Vector2> map,
        float mapScale,
        Texture2D? road)
    {
        if (road is null)
        {
            return;
        }

        canvas.DrawTextureRect(
            road,
            new Rect2(
                map(new Vec2(1_000, 3_850)),
                new Vector2(14_000 * mapScale, 300 * mapScale)),
            false,
            new Color(0.78f, 0.82f, 0.88f, 0.82f));
    }

    private static void DrawRoads(
        CanvasItem canvas,
        Func<Vec2, Vector2> map,
        float mapScale)
    {
        var road = new Color(0.028f, 0.038f, 0.050f, 0.98f);
        var edge = new Color(0.34f, 0.32f, 0.24f, 0.35f);
        var spine = new Color(0.22f, 0.42f, 0.48f, 0.55f);

        var left = map(new Vec2(1_000, 4_000));
        var right = map(new Vec2(15_000, 4_000));
        canvas.DrawLine(left, right, road, 260f * mapScale, true);
        canvas.DrawLine(left, right, edge, 2f, true);
        for (var x = 1_300f; x < 15_000; x += 520f)
        {
            var a = map(new Vec2((int)x, 3_900));
            var b = map(new Vec2((int)(x + 180), 3_900));
            canvas.DrawLine(a, b, new Color(0.62f, 0.48f, 0.22f, 0.45f), 2f);
        }

        foreach (var x in new[] { 5_000f, 8_000f, 11_000f })
        {
            var top = map(new Vec2((int)x, 900));
            var bottom = map(new Vec2((int)x, 7_100));
            canvas.DrawLine(top, bottom, new Color(0.035f, 0.048f, 0.060f, 0.85f), 90f * mapScale);
            canvas.DrawLine(top, bottom, new Color(0.18f, 0.30f, 0.34f, 0.35f), 1.5f);
        }

        canvas.DrawLine(
            map(new Vec2(2_000, 1_000)),
            map(new Vec2(2_000, 7_000)),
            spine,
            1.2f,
            true);
        canvas.DrawLine(
            map(new Vec2(8_000, 1_000)),
            map(new Vec2(8_000, 7_000)),
            spine,
            1.2f,
            true);
    }

    private static void DrawRuins(
        CanvasItem canvas,
        Func<Vec2, Vector2> map,
        float mapScale)
    {
        RuinRandom.Seed = 41;
        var random = RuinRandom;
        for (var index = 0; index < 26; index++)
        {
            var x = random.RandfRange(400f, WidthMm - 900f);
            var y = random.RandfRange(300f, HeightMm - 500f);
            if (Mathf.Abs(y - 4_000f) < 350f)
            {
                continue;
            }

            var width = random.RandfRange(180f, 520f);
            var height = random.RandfRange(100f, 320f);
            var rect = new Rect2(
                map(new Vec2((int)x, (int)y)),
                new Vector2(width * mapScale, height * mapScale));
            canvas.DrawRect(rect, new Color(0.10f, 0.115f, 0.125f, 0.95f));
            canvas.DrawLine(
                rect.Position,
                rect.End,
                new Color(0.48f, 0.30f, 0.16f, 0.55f),
                1.2f);
            canvas.DrawLine(
                rect.Position + new Vector2(0, rect.Size.Y),
                rect.Position + new Vector2(rect.Size.X, 0),
                new Color(0.25f, 0.32f, 0.36f, 0.45f),
                1f);
        }

        for (var index = 0; index < 5; index++)
        {
            var center = map(new Vec2(
                (int)random.RandfRange(1_200f, 14_800f),
                (int)random.RandfRange(800f, 7_200f)));
            var radius = random.RandfRange(10f, 24f);
            canvas.DrawCircle(center, radius, new Color(0.02f, 0.03f, 0.04f, 0.9f));
            canvas.DrawArc(
                center,
                radius,
                0,
                Mathf.Tau,
                28,
                new Color(0.38f, 0.32f, 0.22f, 0.65f),
                1.4f);
        }
    }

    private static void DrawControlPads(
        CanvasItem canvas,
        Func<Vec2, Vector2> map,
        float mapScale,
        IReadOnlyList<(Vec2 Position, int RadiusMm, string? Owner)> controlPoints)
    {
        foreach (var point in controlPoints)
        {
            var center = map(point.Position);
            var radius = point.RadiusMm * mapScale;
            var color = point.Owner switch
            {
                "A" => new Color(0.28f, 0.86f, 0.93f, 0.20f),
                "B" => new Color(1f, 0.55f, 0.28f, 0.20f),
                _ => new Color(1f, 0.82f, 0.35f, 0.14f),
            };
            canvas.DrawCircle(center, radius * 0.72f, new Color(0.03f, 0.045f, 0.06f, 0.55f));
            canvas.DrawArc(center, radius * 0.72f, 0, Mathf.Tau, 48, color, 3f);
            canvas.DrawArc(
                center,
                radius * 0.55f,
                0,
                Mathf.Tau,
                48,
                new Color(0.20f, 0.34f, 0.40f, 0.35f),
                1f);
            for (var index = 0; index < 6; index++)
            {
                var angle = Mathf.Tau * index / 6f;
                var marker = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                    radius * 0.64f;
                canvas.DrawRect(
                    new Rect2(marker - new Vector2(2f, 2f), new Vector2(4f, 4f)),
                    new Color(0.62f, 0.50f, 0.26f, 0.55f));
            }
        }
    }

    private static void DrawCornerHazards(
        CanvasItem canvas,
        Func<Vec2, Vector2> map,
        float mapScale)
    {
        var hazard = new Color(0.72f, 0.50f, 0.18f, 0.50f);
        foreach (var origin in new[]
                 {
                     new Vec2(700, 700), new Vec2(14_600, 700),
                     new Vec2(700, 6_700), new Vec2(14_600, 6_700),
                 })
        {
            var start = map(origin);
            for (var index = 0; index < 4; index++)
            {
                var offset = new Vector2(index * 9f * mapScale * 20f, 0);
                canvas.DrawLine(
                    start + offset,
                    start + offset + new Vector2(8f, 8f),
                    hazard,
                    1.6f);
            }
        }
    }
}
