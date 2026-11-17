using Godot;

namespace SeedField.Game;

/// <summary>
/// Screen-space HUD for the style test. Lives on a CanvasLayer so camera zoom only
/// affects the world, not the interface. Every value is explicitly demo data.
/// </summary>
public partial class StyleTestHud : Control
{
    public string Route { get; init; } = "oblique";
    public string Zoom { get; init; } = "mid";
    public string Overlay { get; init; } = "default";
    public float WorldZoom { get; init; } = 1f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
        GetViewport().SizeChanged += OnViewportSizeChanged;
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= OnViewportSizeChanged;
    }

    private void OnViewportSizeChanged()
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        // The Control is added and anchored after entering the tree, so its own
        // Size can still be (0, 0) when the first draw runs. Lay the HUD out
        // against the viewport rect, which is valid immediately.
        var viewport = GetViewportRect().Size;
        var width = viewport.X;

        DrawRect(new Rect2(0, 0, width, 78), new Color(0.03f, 0.06f, 0.10f, 0.94f));
        DrawLine(
            new Vector2(0, 78),
            new Vector2(width, 78),
            new Color(0.20f, 0.34f, 0.42f, 0.65f),
            1f);

        Label("SEED FIELD / STYLE TEST", new Vector2(28, 34), 22, new Color(0.82f, 0.90f, 0.96f));
        Label(
            $"route {Route} · zoom {Zoom} ({WorldZoom:0.00}x) · overlay {Overlay} · world-space camera",
            new Vector2(28, 58),
            12,
            new Color(0.45f, 0.58f, 0.68f));

        Label("A / B CAMERA TEST", new Vector2(width - 300, 34), 13, new Color(0.55f, 0.68f, 0.78f));
        Label(
            "default · selected · tactical · debug",
            new Vector2(width - 300, 54),
            11,
            new Color(0.45f, 0.58f, 0.68f));

        DrawRect(
            new Rect2(28, viewport.Y - 44, 430, 26),
            new Color(0.12f, 0.07f, 0.02f, 0.88f));
        DrawRect(
            new Rect2(28, viewport.Y - 44, 430, 26),
            new Color(0.80f, 0.52f, 0.18f, 0.75f),
            false,
            1f);
        Label(
            "STATIC SAMPLE · DEMO DATA · DOES NOT READ WORLDSTATE",
            new Vector2(38, viewport.Y - 26),
            11,
            new Color(0.98f, 0.74f, 0.36f));

        Label(
            $"1 grid = 64 px · viewport {viewport.X:0}×{viewport.Y:0}",
            new Vector2(width - 300, viewport.Y - 26),
            11,
            new Color(0.40f, 0.52f, 0.62f));

        if (Overlay == "selected")
        {
            DrawSelectionPanel(width);
        }

        if (Overlay == "debug")
        {
            DrawDebugPanel();
        }
    }

    private void DrawSelectionPanel(float width)
    {
        var panel = new Rect2(width - 300, 96, 272, 112);
        DrawRect(panel, new Color(0.04f, 0.07f, 0.11f, 0.94f));
        DrawRect(panel, new Color(0.25f, 0.55f, 0.65f, 0.85f), false, 1f);
        Label("DEMO SELECTION", panel.Position + new Vector2(12, 10), 12, new Color(0.98f, 0.74f, 0.36f));
        Label("POWER NODE · P-01", panel.Position + new Vector2(12, 32), 13, new Color(0.78f, 0.90f, 0.95f));
        Label("GRID 500/500  (demo)", panel.Position + new Vector2(12, 54), 11, new Color(0.50f, 0.72f, 0.80f));
        Label("STATUS ONLINE  (demo)", panel.Position + new Vector2(12, 76), 11, new Color(0.45f, 0.92f, 0.58f));
        Label("not a simulation readout", panel.Position + new Vector2(12, 96), 10, new Color(0.55f, 0.62f, 0.68f));
    }

    private void DrawDebugPanel()
    {
        var panel = new Rect2(28, 96, 352, 186);
        DrawRect(panel, new Color(0.03f, 0.06f, 0.10f, 0.94f));
        DrawRect(panel, new Color(0.25f, 0.55f, 0.65f, 0.85f), false, 1f);
        Label("DEBUG OVERLAY · DEMO VALUES", panel.Position + new Vector2(14, 12), 13, new Color(0.78f, 0.90f, 0.95f));
        Label("tick 240 · entities 3 · faults 0", panel.Position + new Vector2(14, 40), 11, new Color(0.55f, 0.68f, 0.78f));
        Label("CORE  core-a  E100/100 M100/100", panel.Position + new Vector2(14, 64), 11, new Color(0.55f, 0.68f, 0.78f));
        Label("P-01  grid 500/500  gen 4/t", panel.Position + new Vector2(14, 88), 11, new Color(0.55f, 0.68f, 0.78f));
        Label("U-07  energy 100/100  intent wait", panel.Position + new Vector2(14, 112), 11, new Color(0.55f, 0.68f, 0.78f));
        Label("cyan = sprite bounds · amber = pivot", panel.Position + new Vector2(14, 140), 11, new Color(0.62f, 0.78f, 0.86f));
        Label("visuals are event-driven; no VFX mutation", panel.Position + new Vector2(14, 164), 10, new Color(0.40f, 0.55f, 0.65f));
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
