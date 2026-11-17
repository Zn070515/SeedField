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

    public event Action<string>? OpenBreakerRequested;

    public event Action<string>? CloseBreakerRequested;

    public event Action<string>? MaintainBreakerRequested;

    private Label _summary = null!;
    private ItemList _regions = null!;
    private ItemList _links = null!;
    private Label _status = null!;
    private readonly List<string> _regionIds = new();

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
        Size = new Vector2(680, 620);
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

        var breakerRow = new HBoxContainer();
        breakerRow.AddThemeConstantOverride("separation", 8);
        breakerRow.AddChild(ActionButton("OPEN BREAKER", OnOpenBreaker));
        breakerRow.AddChild(ActionButton("CLOSE BREAKER", OnCloseBreaker));
        breakerRow.AddChild(ActionButton("MAINTAIN", OnMaintainBreaker));
        var breakerHint = new Label
        {
            Text = "Manual open holds until closed. MAINTAIN clears trip wear and lockout.",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        breakerHint.AddThemeFontSizeOverride("font_size", 11);
        breakerHint.Modulate = new Color(0.55f, 0.68f, 0.78f);
        breakerRow.AddChild(breakerHint);
        box.AddChild(breakerRow);

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
        var tripLimit = diagnostics.BreakerTripLimit == 0
            ? "∞"
            : diagnostics.BreakerTripLimit.ToString();
        _summary.Text =
            $"gen {diagnostics.TotalGenerationPerTick}/t · " +
            $"grid {diagnostics.TotalGridEnergy} · " +
            $"local {diagnostics.TotalLocalEnergy}/{diagnostics.TotalCapacity} · " +
            $"res {diagnostics.ReserveEnergy}+{diagnostics.PriorityReserveEnergy} · " +
            $"pri≥{diagnostics.HighPriorityThreshold} · " +
            $"trip≤{tripLimit} · " +
            $"maint {diagnostics.BreakerMaintenanceMaterialCost}M · " +
            $"transit {diagnostics.TotalInTransitPower}";

        _regionIds.Clear();
        _regions.Clear();
        foreach (var region in diagnostics.Regions)
        {
            _regionIds.Add(region.RegionId);
            var connected = region.ConnectedRegions.Count == 0
                ? "iso"
                : string.Join(",", region.ConnectedRegions.Take(3)) +
                  (region.ConnectedRegions.Count > 3 ? "+" : string.Empty);
            var capacity = region.GridCapacity == long.MaxValue
                ? "∞"
                : region.GridCapacity.ToString();
            var breaker = region.BreakerMaintenanceRequired
                ? "MAINT!"
                : region.BreakerOpen
                    ? "BREAKER"
                    : region.IsBlackedOut
                        ? "BLACKOUT"
                        : region.IsLoadShed ? "SHED" : "OK";
            _regions.AddItem(
                $"{region.RegionId} · G{region.GridEnergy}/{capacity} · " +
                $"gen{region.GenerationPerTick}/t · " +
                $"L{region.LocalEnergy}/{region.LocalCapacity} · " +
                $"comp{region.ComponentRegionCount} · {connected} · " +
                $"res{region.ReserveEnergy} · in{region.IncomingPower} · " +
                $"trips{region.BreakerTripCount} · {breaker}");
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

    private void OnOpenBreaker()
    {
        var index = SelectedRegionIndex();
        if (index < 0)
        {
            SetStatus("select a region first", true);
            return;
        }

        OpenBreakerRequested?.Invoke(_regionIds[index]);
    }

    private void OnCloseBreaker()
    {
        var index = SelectedRegionIndex();
        if (index < 0)
        {
            SetStatus("select a region first", true);
            return;
        }

        CloseBreakerRequested?.Invoke(_regionIds[index]);
    }

    private void OnMaintainBreaker()
    {
        var index = SelectedRegionIndex();
        if (index < 0)
        {
            SetStatus("select a region first", true);
            return;
        }

        MaintainBreakerRequested?.Invoke(_regionIds[index]);
    }

    private int SelectedRegionIndex()
    {
        var selected = _regions.GetSelectedItems();
        return selected.Length == 0 ||
               selected[0] < 0 ||
               selected[0] >= _regionIds.Count
            ? -1
            : selected[0];
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        return button;
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.Modulate = new Color(0.55f, 0.68f, 0.78f);
        return label;
    }
}
