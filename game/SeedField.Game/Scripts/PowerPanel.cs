using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Read-only M2 power diagnostics panel. It renders a derived snapshot and never
/// mutates the world.
/// </summary>
public partial class PowerPanel : PanelContainer
{
    public event Action? CloseRequested;

    private Label _summary = null!;
    private ItemList _regions = null!;
    private ItemList _links = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.08f, 0.13f, 0.98f),
            BorderColor = new Color(0.25f, 0.50f, 0.65f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        };
        style.SetCornerRadiusAll(6);
        AddThemeStyleboxOverride("panel", style);

        Position = new Vector2(300, 24);
        Size = new Vector2(680, 560);
        CustomMinimumSize = Size;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);

        var title = new Label { Text = "A POWER GRID · DIAGNOSTICS" };
        title.AddThemeFontSizeOverride("font_size", 19);
        box.AddChild(title);

        _summary = new Label { Text = "GEN · GRID · LOCAL" };
        _summary.AddThemeFontSizeOverride("font_size", 13);
        _summary.Modulate = new Color(0.55f, 0.68f, 0.78f);
        box.AddChild(_summary);

        box.AddChild(SectionLabel("REGIONS · GRID STOCK / GENERATION / LOCAL BUFFER"));
        _regions = new ItemList { CustomMinimumSize = new Vector2(640, 190) };
        box.AddChild(_regions);

        box.AddChild(SectionLabel("POWER LINKS · AUTOMATIC FLOW CAPACITY"));
        _links = new ItemList { CustomMinimumSize = new Vector2(640, 100) };
        box.AddChild(_links);

        var hint = new Label
        {
            Text = "BLACKOUT = zero regional stock; SHED = reserve floor blocks draws. " +
                   "Local buffers may still power devices.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(640, 36),
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.Modulate = new Color(0.55f, 0.68f, 0.78f);
        box.AddChild(hint);

        _status = new Label
        {
            Text = "ready",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(640, 30),
        };
        _status.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(_status);

        var close = new Button { Text = "CLOSE  F4" };
        close.Pressed += () => CloseRequested?.Invoke();
        box.AddChild(close);
    }

    public void Refresh(WorldState world, string civilizationId)
    {
        var diagnostics = PowerGridDiagnosticsBuilder.Build(world, civilizationId);
        _summary.Text =
            $"gen {diagnostics.TotalGenerationPerTick}/t · " +
            $"grid {diagnostics.TotalGridEnergy} · " +
            $"local {diagnostics.TotalLocalEnergy}/{diagnostics.TotalCapacity} · " +
            $"reserve {diagnostics.ReserveEnergy} · " +
            $"transit {diagnostics.TotalInTransitPower}";

        _regions.Clear();
        foreach (var region in diagnostics.Regions)
        {
            var connected = region.ConnectedRegions.Count == 0
                ? "iso"
                : string.Join(",", region.ConnectedRegions.Take(3)) +
                  (region.ConnectedRegions.Count > 3 ? "+" : string.Empty);
            _regions.AddItem(
                $"{region.RegionId} · G{region.GridEnergy} · " +
                $"gen{region.GenerationPerTick}/t · " +
                $"L{region.LocalEnergy}/{region.LocalCapacity} · " +
                $"comp{region.ComponentRegionCount} · {connected} · " +
                $"res{region.ReserveEnergy} · in{region.IncomingPower} · " +
                $"{(region.IsBlackedOut ? "BLACKOUT" : region.IsLoadShed ? "SHED" : "OK")}");
        }

        if (_regions.ItemCount > 0)
        {
            _regions.Select(0);
        }

        _links.Clear();
        foreach (var link in world.Links.Values
                     .Where(link => link.PowerPerTick > 0)
                     .OrderBy(link => link.Id, StringComparer.Ordinal))
        {
            _links.AddItem(
                $"{link.Id} · {link.RegionA} ↔ {link.RegionB} · " +
                $"{link.PowerPerTick} E/t");
        }

        if (_links.ItemCount > 0)
        {
            _links.Select(0);
        }
        else
        {
            _links.AddItem("no automatic power links in this world");
        }
    }

    public void SetStatus(string text, bool error)
    {
        _status.Text = text;
        _status.Modulate = error
            ? new Color(1.00f, 0.45f, 0.35f)
            : new Color(0.50f, 0.95f, 0.60f);
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.Modulate = new Color(0.55f, 0.68f, 0.78f);
        return label;
    }
}
