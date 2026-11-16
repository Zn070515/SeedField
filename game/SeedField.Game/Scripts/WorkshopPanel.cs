using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Minimal M1 workshop: lists published blueprint versions and A-side instances.
/// Deployment/upgrade requests are raised to Main, which owns the world.
/// </summary>
public partial class WorkshopPanel : PanelContainer
{
    public event Action<string>? DeployRequested;

    public event Action<EntityId>? UpgradeRequested;

    private Label _coverage = null!;
    private ItemList _blueprints = null!;
    private ItemList _instances = null!;
    private Label _status = null!;
    private readonly List<string> _blueprintIds = new();
    private readonly List<EntityId> _entityIds = new();

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

        Position = new Vector2(300, 40);
        Size = new Vector2(680, 640);
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

        var title = new Label { Text = "A WORKSHOP · DESIGNS AND INSTANCES" };
        title.AddThemeFontSizeOverride("font_size", 19);
        box.AddChild(title);

        _coverage = new Label { Text = "FIVE BRANCHES" };
        _coverage.AddThemeFontSizeOverride("font_size", 14);
        box.AddChild(_coverage);

        var blueprintLabel = new Label { Text = "LATEST PUBLISHED VERSION PER DESIGN" };
        blueprintLabel.AddThemeFontSizeOverride("font_size", 12);
        blueprintLabel.Modulate = new Color(0.55f, 0.68f, 0.78f);
        box.AddChild(blueprintLabel);

        _blueprints = new ItemList { CustomMinimumSize = new Vector2(640, 170) };
        box.AddChild(_blueprints);

        var deployRow = new HBoxContainer();
        deployRow.AddThemeConstantOverride("separation", 12);
        var deploy = new Button { Text = "DEPLOY SELECTED" };
        deploy.Pressed += OnDeployPressed;
        deployRow.AddChild(deploy);
        var deployHint = new Label
        {
            Text = "Material cost is not enforced in this M1 prototype.",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        deployHint.AddThemeFontSizeOverride("font_size", 11);
        deployHint.Modulate = new Color(0.55f, 0.68f, 0.78f);
        deployRow.AddChild(deployHint);
        box.AddChild(deployRow);

        var instanceLabel = new Label { Text = "A-SIDE DEPLOYED INSTANCES" };
        instanceLabel.AddThemeFontSizeOverride("font_size", 12);
        instanceLabel.Modulate = new Color(0.55f, 0.68f, 0.78f);
        box.AddChild(instanceLabel);

        _instances = new ItemList { CustomMinimumSize = new Vector2(640, 130) };
        box.AddChild(_instances);

        var upgradeRow = new HBoxContainer();
        upgradeRow.AddThemeConstantOverride("separation", 12);
        var upgrade = new Button { Text = "UPGRADE SELECTED TO LATEST" };
        upgrade.Pressed += OnUpgradePressed;
        upgradeRow.AddChild(upgrade);
        var upgradeHint = new Label
        {
            Text = "Existing instances never change on publish.",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        upgradeHint.AddThemeFontSizeOverride("font_size", 11);
        upgradeHint.Modulate = new Color(0.55f, 0.68f, 0.78f);
        upgradeRow.AddChild(upgradeHint);
        box.AddChild(upgradeRow);

        _status = new Label { Text = "ready", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(_status);

        var close = new Button { Text = "CLOSE  F2" };
        close.Pressed += () => CloseRequested?.Invoke();
        box.AddChild(close);
    }

    public event Action? CloseRequested;

    public void Refresh(WorldState world)
    {
        _blueprintIds.Clear();
        _blueprints.Clear();

        foreach (var group in world.Workshop.GetAllVersions()
                     .GroupBy(blueprint => blueprint.BlueprintId)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var latest = group.OrderBy(blueprint => blueprint.Version).Last();
            _blueprintIds.Add(latest.BlueprintId);
            _blueprints.AddItem(
                $"{latest.BlueprintId} v{latest.Version} · {latest.Name} · {DescribeBranches(latest.Branches)}");
        }

        if (_blueprints.ItemCount > 0)
        {
            _blueprints.Select(0);
        }

        _entityIds.Clear();
        _instances.Clear();

        foreach (var entity in world.EnumerateEntities()
                     .Where(entity => entity.CivilizationId == "A" && entity.BlueprintId is not null)
                     .OrderBy(entity => entity.Id.Value))
        {
            _entityIds.Add(entity.Id);
            _instances.AddItem(
                $"{entity.Id} · {entity.BlueprintId} v{entity.BlueprintVersion} · " +
                $"HP {entity.Health}/{entity.MaxHealth} · E {entity.Energy}");
        }

        if (_instances.ItemCount > 0)
        {
            _instances.Select(0);
        }

        var coverage = world.Workshop.Coverage;
        var missing = DeviceBranch.All & ~coverage;
        var ready = missing == DeviceBranch.None;
        _coverage.Text = ready
            ? "FIVE BRANCHES 5/5 · PvE READY"
            : $"FIVE BRANCHES INCOMPLETE · missing {missing}";
        _coverage.Modulate = ready
            ? new Color(0.50f, 0.95f, 0.60f)
            : new Color(1.00f, 0.82f, 0.35f);
    }

    public void SelectBlueprint(string blueprintId)
    {
        var index = _blueprintIds.IndexOf(blueprintId);
        if (index >= 0)
        {
            _blueprints.Select(index);
        }
    }

    public void SelectInstance(EntityId entityId)
    {
        var index = _entityIds.IndexOf(entityId);
        if (index >= 0)
        {
            _instances.Select(index);
        }
    }

    public void SetStatus(string text, bool error)
    {
        _status.Text = text;
        _status.Modulate = error
            ? new Color(1.00f, 0.45f, 0.35f)
            : new Color(0.50f, 0.95f, 0.60f);
    }

    private void OnDeployPressed()
    {
        var selected = _blueprints.GetSelectedItems();
        if (selected.Length == 0 || selected[0] < 0 || selected[0] >= _blueprintIds.Count)
        {
            SetStatus("select a design first", true);
            return;
        }

        DeployRequested?.Invoke(_blueprintIds[selected[0]]);
    }

    private void OnUpgradePressed()
    {
        var selected = _instances.GetSelectedItems();
        if (selected.Length == 0 || selected[0] < 0 || selected[0] >= _entityIds.Count)
        {
            SetStatus("select a deployed instance first", true);
            return;
        }

        UpgradeRequested?.Invoke(_entityIds[selected[0]]);
    }

    private static string DescribeBranches(DeviceBranch branches)
    {
        var parts = new List<string>();
        if ((branches & DeviceBranch.Energy) != 0) parts.Add("E");
        if ((branches & DeviceBranch.Sensing) != 0) parts.Add("S");
        if ((branches & DeviceBranch.Communication) != 0) parts.Add("C");
        if ((branches & DeviceBranch.State) != 0) parts.Add("T");
        if ((branches & DeviceBranch.Execution) != 0) parts.Add("X");
        return string.Join("/", parts);
    }
}
