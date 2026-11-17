using Godot;

namespace SeedField.Game;

/// <summary>Shared primitives for presentation drawing.</summary>
public static class VisualDraw
{
    public static void Ellipse(
        CanvasItem canvas,
        Vector2 center,
        float radiusX,
        float radiusY,
        Color color,
        int segments = 28)
    {
        var points = new Vector2[segments];
        for (var index = 0; index < segments; index++)
        {
            var angle = Mathf.Tau * index / segments;
            points[index] = center + new Vector2(
                Mathf.Cos(angle) * radiusX,
                Mathf.Sin(angle) * radiusY);
        }

        canvas.DrawColoredPolygon(points, color);
    }
}
