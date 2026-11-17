using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Energy/information links with tick-driven pulses. Draw only; the caller
/// supplies whether each side currently has grid energy.
/// </summary>
public static class NetworkVisual
{
    private static readonly Color AColor = new(0.30f, 0.90f, 1f);
    private static readonly Color BColor = new(1f, 0.60f, 0.30f);

    public static void Draw(
        CanvasItem canvas,
        Func<Vec2, Vector2> map,
        IReadOnlyList<(Vec2 Position, string? Owner)> points,
        bool aPowered,
        bool bPowered,
        long tick,
        bool tactical)
    {
        var aHub = map(Scenarios.AStagingPosition);
        var bHub = map(Scenarios.BStagingPosition);
        var index = 0;
        foreach (var point in points)
        {
            var target = map(point.Position);
            var aLink = string.Equals(point.Owner, "A", StringComparison.Ordinal);
            var bLink = string.Equals(point.Owner, "B", StringComparison.Ordinal);
            if (aLink)
            {
                DrawLink(
                    canvas,
                    aHub,
                    target,
                    AColor,
                    aPowered,
                    tick,
                    index,
                    tactical);
            }

            if (bLink)
            {
                DrawLink(
                    canvas,
                    bHub,
                    target,
                    BColor,
                    bPowered,
                    tick,
                    index + 3,
                    tactical);
            }

            index++;
        }
    }

    public static void DrawCoreExit(
        CanvasItem canvas,
        Vector2 start,
        Vector2 end,
        bool powered,
        long tick)
    {
        canvas.DrawLine(
            start,
            end,
            powered
                ? new Color(0.95f, 0.78f, 0.40f, 0.55f)
                : new Color(0.40f, 0.34f, 0.24f, 0.35f),
            1.6f);
        if (!powered)
        {
            return;
        }

        for (var index = 0; index < 2; index++)
        {
            var t = Mathf.PosMod(tick * 0.06f + index * 0.5f, 1f);
            var point = start.Lerp(end, t);
            canvas.DrawCircle(point, 3f, new Color(1f, 0.86f, 0.52f, 0.9f));
        }
    }

    private static void DrawLink(
        CanvasItem canvas,
        Vector2 from,
        Vector2 to,
        Color color,
        bool powered,
        long tick,
        int phase,
        bool tactical)
    {
        var alpha = tactical ? 0.55f : 0.22f;
        canvas.DrawLine(
            from,
            to,
            new Color(color, powered ? alpha : alpha * 0.45f),
            tactical ? 2f : 1.2f);
        if (!powered)
        {
            return;
        }

        for (var index = 0; index < 2; index++)
        {
            var t = Mathf.PosMod(tick * 0.05f + phase * 0.17f + index * 0.5f, 1f);
            var point = from.Lerp(to, t);
            canvas.DrawCircle(
                point,
                tactical ? 4f : 2.6f,
                new Color(color, 0.85f));
        }
    }
}
