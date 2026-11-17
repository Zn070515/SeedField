using Godot;

namespace SeedField.Game;

/// <summary>
/// Draws one device sprite, contact shadow and status bars. It never reads or
/// mutates WorldState; Main hands it a VisualEntitySnapshot.
/// </summary>
public sealed class EntityVisual
{
    public const float ScreenScale = 0.26f;

    private readonly VisualAssetCatalog _catalog;

    public EntityVisual(VisualAssetCatalog catalog)
    {
        _catalog = catalog;
    }

    public void Draw(
        CanvasItem canvas,
        VisualEntitySnapshot entity,
        Vector2 position,
        int direction,
        bool showId,
        bool debug,
        long tick,
        bool moving,
        bool hitFlash)
    {
        var frame = _catalog.Get(entity.VisualAssetName, direction);
        if (frame is null)
        {
            canvas.DrawCircle(
                position,
                7f,
                entity.CivilizationId == "A" ? new Color("43d9e8") : new Color("f2a33a"));
            return;
        }

        var size = frame.Footprint * ScreenScale;
        var rect = new Rect2(position - frame.Pivot * size, new Vector2(size, size));

        VisualDraw.Ellipse(
            canvas,
            position + new Vector2(-size * 0.12f, size * 0.03f),
            size * 0.28f,
            size * 0.10f,
            new Color(0f, 0f, 0f, 0.24f));
        if (moving)
        {
            DrawTrackDust(canvas, position, size, direction, tick);
        }

        canvas.DrawTextureRect(frame.Diffuse, rect, false);

        var healthFraction = entity.MaxHealth <= 0
            ? 0f
            : (float)entity.Health / entity.MaxHealth;
        var energyFraction = entity.EnergyCapacity <= 0
            ? 0f
            : (float)entity.Energy / entity.EnergyCapacity;
        DrawBar(
            canvas,
            position + new Vector2(-15f, -size * 0.62f),
            30f,
            3f,
            healthFraction,
            new Color(0.35f, 0.95f, 0.50f));
        if (entity.EnergyCapacity > 0)
        {
            DrawBar(
                canvas,
                position + new Vector2(-15f, size * 0.40f),
                30f,
                2.5f,
                energyFraction,
                new Color(0.35f, 0.75f, 1f));
        }

        if (showId)
        {
            canvas.DrawString(
                ThemeDB.FallbackFont,
                position + new Vector2(-18f, size * 0.55f + 12f),
                $"{entity.CivilizationId}{entity.EntityId}",
                HorizontalAlignment.Left,
                -1,
                11,
                new Color(0.82f, 0.90f, 0.96f));
        }

        if (hitFlash)
        {
            canvas.DrawArc(
                position,
                size * 0.34f,
                0,
                Mathf.Tau,
                40,
                new Color(1f, 0.30f, 0.22f, 0.9f),
                2.2f);
        }

        if (debug)
        {
            canvas.DrawRect(rect, new Color(0.35f, 0.95f, 1f, 0.55f), false, 1.2f);
            canvas.DrawLine(
                position - new Vector2(8f, 0f),
                position + new Vector2(8f, 0f),
                new Color(1f, 0.55f, 0.20f, 0.95f),
                1.5f);
            canvas.DrawLine(
                position - new Vector2(0f, 8f),
                position + new Vector2(0f, 8f),
                new Color(1f, 0.55f, 0.20f, 0.95f),
                1.5f);
            canvas.DrawString(
                ThemeDB.FallbackFont,
                position + new Vector2(10f, -size * 0.62f - 8f),
                $"{entity.CivilizationId}{entity.EntityId} {entity.VisualAssetName} v{entity.BlueprintVersion}",
                HorizontalAlignment.Left,
                -1,
                10,
                new Color(0.90f, 0.72f, 0.40f));
        }
    }

    private static void DrawTrackDust(
        CanvasItem canvas,
        Vector2 position,
        float size,
        int direction,
        long tick)
    {
        var backAngle = (direction + 4) * Mathf.Tau / 8f;
        var back = new Vector2(Mathf.Cos(backAngle), Mathf.Sin(backAngle));
        for (var puff = 0; puff < 2; puff++)
        {
            var phase = Mathf.PosMod((float)tick + puff * 2f, 4f) / 4f;
            VisualDraw.Ellipse(
                canvas,
                position + back * size * (0.34f + phase * 0.22f),
                size * (0.07f + phase * 0.03f),
                size * (0.035f + phase * 0.015f),
                new Color(0.58f, 0.52f, 0.40f, 0.18f * (1f - phase)));
        }
    }

    private static void DrawBar(
        CanvasItem canvas,
        Vector2 position,
        float width,
        float height,
        float fraction,
        Color color)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);
        canvas.DrawRect(
            new Rect2(position, new Vector2(width, height)),
            new Color(0.13f, 0.18f, 0.23f));
        canvas.DrawRect(
            new Rect2(position, new Vector2(width * fraction, height)),
            color);
    }

}
