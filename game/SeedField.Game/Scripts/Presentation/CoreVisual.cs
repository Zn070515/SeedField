using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>Draws the permanent core and its derived visual state.</summary>
public sealed class CoreVisual
{
    public const float ScreenScale = 0.28f;

    private readonly VisualAssetCatalog _catalog;

    public CoreVisual(VisualAssetCatalog catalog)
    {
        _catalog = catalog;
    }

    public void Draw(
        CanvasItem canvas,
        Vector2 position,
        CoreVisualState state,
        long tick,
        bool debug)
    {
        var frame = _catalog.GetCore(state);
        if (frame is null)
        {
            canvas.DrawCircle(position, 22f, new Color(1f, 0.82f, 0.35f));
            return;
        }

        var size = frame.Footprint * ScreenScale;
        var rect = new Rect2(position - frame.Pivot * size, new Vector2(size, size));
        var color = StateColor(state);
        var pulse = 0.55f + 0.20f * Mathf.Sin(tick * 0.22f);

        VisualDraw.Ellipse(
            canvas,
            position + new Vector2(-size * 0.10f, size * 0.02f),
            size * 0.36f,
            size * 0.13f,
            new Color(0f, 0f, 0f, 0.26f));
        canvas.DrawTextureRect(frame.Diffuse, rect, false);

        canvas.DrawArc(
            position,
            size * 0.54f,
            0,
            Mathf.Tau,
            64,
            new Color(color, pulse),
            2.4f);
        for (var index = 0; index < 5; index++)
        {
            var angle = tick * 0.015f + index * Mathf.Tau / 5f;
            var socket = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                size * 0.55f;
            canvas.DrawCircle(socket, 3.2f, new Color(color, 0.85f));
        }
        canvas.DrawString(
            ThemeDB.FallbackFont,
            position + new Vector2(-52f, size * 0.62f + 16f),
            $"CORE A · {state.ToString().ToUpperInvariant()}",
            HorizontalAlignment.Left,
            -1,
            11,
            color);

        if (debug)
        {
            canvas.DrawRect(rect, new Color(1f, 0.75f, 0.35f, 0.55f), false, 1.2f);
            canvas.DrawString(
                ThemeDB.FallbackFont,
                position + new Vector2(10f, -size * 0.50f),
                $"core pivot {frame.Pivot.X:0.00},{frame.Pivot.Y:0.00}",
                HorizontalAlignment.Left,
                -1,
                10,
                new Color(0.90f, 0.72f, 0.40f));
        }
    }

    private static Color StateColor(CoreVisualState state) => state switch
    {
        CoreVisualState.Besieged => new Color(1f, 0.30f, 0.20f),
        CoreVisualState.Bootstrap => new Color(1f, 0.62f, 0.20f),
        CoreVisualState.Regenerating => new Color(0.35f, 0.88f, 1f),
        CoreVisualState.Absent => new Color(0.45f, 0.50f, 0.55f),
        _ => new Color(0.95f, 0.78f, 0.40f),
    };

}
