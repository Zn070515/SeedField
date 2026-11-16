using Godot;

namespace SeedField.Game;

/// <summary>
/// In-game JavaScript editor for civilization A. It only collects text and raises
/// deploy/close requests; Main owns the live ControllerSlot.
/// </summary>
public partial class ProgramEditor : PanelContainer
{
    public event Action? DeployRequested;

    public event Action? CloseRequested;

    private TextEdit _text = null!;
    private Label _status = null!;

    public string Source
    {
        get => _text.Text;
        set => _text.Text = value;
    }

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

        Position = new Vector2(300, 64);
        Size = new Vector2(680, 600);
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

        var title = new Label { Text = "A CONTROLLER · JAVASCRIPT" };
        title.AddThemeFontSizeOverride("font_size", 19);
        box.AddChild(title);

        var hint = new Label
        {
            Text = "Edit decide(observation), then deploy. Deployment replaces the live controller; "
                + "a failed load keeps the previous version running.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.Modulate = new Color(0.55f, 0.68f, 0.78f);
        box.AddChild(hint);

        _text = new TextEdit
        {
            CustomMinimumSize = new Vector2(640, 430),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        var mono = new SystemFont { FontNames = ["Cascadia Mono", "Consolas", "Courier New"] };
        _text.AddThemeFontOverride("font", mono);
        _text.AddThemeFontSizeOverride("font_size", 13);
        box.AddChild(_text);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var deploy = new Button { Text = "DEPLOY  Ctrl+Enter" };
        deploy.Pressed += () => DeployRequested?.Invoke();
        row.AddChild(deploy);

        var close = new Button { Text = "CLOSE  F1" };
        close.Pressed += () => CloseRequested?.Invoke();
        row.AddChild(close);

        _status = new Label
        {
            Text = "ready",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _status.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(_status);

        box.AddChild(row);
    }

    public void SetStatus(string text, bool error)
    {
        _status.Text = text;
        _status.Modulate = error
            ? new Color(1.00f, 0.45f, 0.35f)
            : new Color(0.50f, 0.95f, 0.60f);
    }

    public void FocusEditor() => _text.GrabFocus();
}
