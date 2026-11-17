using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Read-only recovery diagnostics plus host command buttons. It renders a derived
/// snapshot and never mutates the world directly.
/// </summary>
public partial class RecoveryPanel : PanelContainer
{
    public event Action? CloseRequested;

    public event Action? RecoverRequested;

    public event Action? MaintainRequested;

    private Label _summary = null!;
    private Label _pressure = null!;
    private Label _anchors = null!;
    private Label _siege = null!;
    private Label _status = null!;
    private Button _recover = null!;

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
        Size = new Vector2(680, 470);
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

        var title = new Label { Text = "A SEED CORE · RECOVERY" };
        title.AddThemeFontSizeOverride("font_size", 19);
        box.AddChild(title);

        _summary = BodyLabel("reserve · quota · debt");
        box.AddChild(_summary);

        box.AddChild(SectionLabel("FAST RECOVERY"));
        _pressure = BodyLabel("quota · window · cost");
        box.AddChild(_pressure);

        box.AddChild(SectionLabel("SHELL & SIEGE"));
        _anchors = BodyLabel("anchors · coverage");
        box.AddChild(_anchors);
        _siege = BodyLabel("siege · quota penalty");
        box.AddChild(_siege);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        _recover = ActionButton("RECOVER MIN", () => RecoverRequested?.Invoke());
        actions.AddChild(_recover);
        actions.AddChild(ActionButton("MAINTAIN CORE", () => MaintainRequested?.Invoke()));
        var hint = new Label
        {
            Text = "RECOVER spends reserves; MAINTAIN pays material to clear quota/debt.",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.Modulate = new Color(0.55f, 0.68f, 0.78f);
        actions.AddChild(hint);
        box.AddChild(actions);

        var rule = BodyLabel(
            "Baseline core regeneration is never stopped by siege. " +
            "Losses only make fast recovery slower or more expensive.");
        box.AddChild(rule);

        _status = BodyLabel("ready");
        box.AddChild(_status);

        var close = new Button { Text = "CLOSE  F6" };
        close.Pressed += () => CloseRequested?.Invoke();
        box.AddChild(close);
    }

    public void Refresh(WorldState world, string civilizationId)
    {
        var snapshot = CoreRecoveryDiagnosticsBuilder.Build(world, civilizationId);
        if (!snapshot.HasCore)
        {
            _summary.Text = "no seed core for this civilization";
            _pressure.Text = "—";
            _anchors.Text = "—";
            _siege.Text = "—";
            _recover.Disabled = true;
            return;
        }

        _recover.Disabled = !snapshot.CanRecover;
        _summary.Text =
            $"{snapshot.RegionId} · E {snapshot.EnergyReserve}/{snapshot.MaxEnergyReserve} · " +
            $"M {snapshot.MaterialReserve}/{snapshot.MaxMaterialReserve} · " +
            $"maint {snapshot.RecoveryMaintenanceMaterialCost}M · " +
            $"wrecks {snapshot.WreckCount}";
        _pressure.Text =
            $"quota {snapshot.RecoveriesInWindow}/{snapshot.EffectiveQuota} " +
            $"(base {snapshot.QuotaPerWindow}) · " +
            $"window {snapshot.RecoveryWindowTicks}t · reset {snapshot.WindowResetsInTicks}t · " +
            $"debt streak {snapshot.RecoveryStreak} · cost ×{snapshot.CostMultiplierPercent}%";
        _anchors.Text =
            $"anchors {snapshot.AliveAnchors}/{snapshot.AnchorTarget} · " +
            $"gap {snapshot.AnchorGap} · +{snapshot.AnchorRecoveryCostPercent}% per gap";
        _siege.Text = snapshot.IsBesieged
            ? $"BESIEGED · +{snapshot.SiegeRecoveryCostPercent}% cost · " +
              $"-{snapshot.SiegeQuotaPenalty} quota"
            : "no hostile civilization in directly linked regions";
    }

    public void SetStatus(string text, bool error)
    {
        _status.Text = text;
        _status.Modulate = error
            ? new Color(1.00f, 0.45f, 0.35f)
            : new Color(0.50f, 0.95f, 0.60f);
    }

    private static Label BodyLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(640, 24),
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.Modulate = new Color(0.72f, 0.82f, 0.90f);
        return label;
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.Modulate = new Color(0.55f, 0.68f, 0.78f);
        return label;
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        return button;
    }
}
