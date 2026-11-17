using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Optional tactical layers. Default gameplay draws none of these; F7 enables
/// ranges/IDs and F8 adds diagnostic geometry.
/// </summary>
public static class TacticalOverlay
{
    public static void DrawEntityRanges(
        CanvasItem canvas,
        IEnumerable<VisualEntitySnapshot> entities,
        Func<Vec2, Vector2> map,
        float mapScale)
    {
        foreach (var entity in entities)
        {
            var center = map(entity.Position);
            var color = entity.CivilizationId == "A"
                ? new Color(0.28f, 0.86f, 0.93f)
                : new Color(1.00f, 0.55f, 0.28f);
            if (entity.SensorRangeMm > 0)
            {
                canvas.DrawArc(
                    center,
                    entity.SensorRangeMm * mapScale,
                    0,
                    Mathf.Tau,
                    64,
                    new Color(color, 0.30f),
                    1.3f);
            }

            if (entity.WeaponRangeMm > 0)
            {
                canvas.DrawArc(
                    center,
                    entity.WeaponRangeMm * mapScale,
                    0,
                    Mathf.Tau,
                    64,
                    new Color(1f, 0.34f, 0.25f, 0.24f),
                    1.1f);
            }
        }
    }

    public static void DrawIds(
        CanvasItem canvas,
        IEnumerable<VisualEntitySnapshot> entities,
        Func<Vec2, Vector2> map)
    {
        var index = 0;
        foreach (var entity in entities)
        {
            var position = map(entity.Position);
            canvas.DrawString(
                ThemeDB.FallbackFont,
                position + new Vector2(-18f, -30f - (index % 2) * 13f),
                $"{entity.CivilizationId}{entity.EntityId} · {entity.BlueprintId ?? "device"}",
                HorizontalAlignment.Left,
                -1,
                10,
                new Color(0.86f, 0.94f, 1f));
            index++;
        }
    }

    public static void DrawControlPointRadii(
        CanvasItem canvas,
        IEnumerable<(Vec2 Position, int RadiusMm)> points,
        Func<Vec2, Vector2> map,
        float mapScale)
    {
        foreach (var point in points)
        {
            var center = map(point.Position);
            canvas.DrawArc(
                center,
                point.RadiusMm * mapScale,
                0,
                Mathf.Tau,
                64,
                new Color(1f, 0.82f, 0.35f, 0.45f),
                1.4f);
        }
    }
}

/// <summary>F8 diagnostics; deliberately separate from tactical gameplay info.</summary>
public static class DebugOverlay
{
    public static void Draw(
        CanvasItem canvas,
        Vector2 origin,
        int entities,
        long tick)
    {
        var lines = new[]
        {
            "DEBUG OVERLAY · F8",
            $"tick {tick} · entities {entities} · fps {Engine.GetFramesPerSecond():0}",
            $"process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000f:0.0} ms",
            "cyan = sprite bounds · amber = pivot",
            "F7 tactical · F8 debug · F12 screenshot",
        };

        canvas.DrawRect(
            new Rect2(origin, new Vector2(340, 118)),
            new Color(0.03f, 0.06f, 0.10f, 0.92f));
        for (var index = 0; index < lines.Length; index++)
        {
            canvas.DrawString(
                ThemeDB.FallbackFont,
                origin + new Vector2(12f, 24f + index * 20f),
                lines[index],
                HorizontalAlignment.Left,
                -1,
                index == 0 ? 12 : 10,
                index == 0
                    ? new Color(0.90f, 0.72f, 0.40f)
                    : new Color(0.62f, 0.78f, 0.86f));
        }
    }
}
