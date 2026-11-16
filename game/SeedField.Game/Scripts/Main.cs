using Godot;
using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// M0 visual slice. The C# simulation remains authoritative; this node only renders
/// the events it receives and never mutates WorldState.
/// </summary>
public partial class Main : Node2D
{
    private const float TicksPerSecond = 6f;
    private const int PreRunMaxTicks = 800;
    private const int PreRunMinShots = 3;
    private const int EventRetentionTicks = 45;

    private const int ViewWidth = 1280;
    private const int ViewHeight = 720;
    private const float MapScale = 0.055f;

    private const string SavePath = "user://seedfield-save.json";

    private static readonly Vector2 MapOrigin = new(200f, 140f);

    private static readonly Color Background = new("0b1220");
    private static readonly Color Grid = new(0.10f, 0.16f, 0.24f);
    private static readonly Color GridMajor = new(0.18f, 0.28f, 0.38f);
    private static readonly Color Border = new(0.28f, 0.46f, 0.60f);
    private static readonly Color Cyan = new(0.28f, 0.86f, 0.93f);
    private static readonly Color Orange = new(1.00f, 0.55f, 0.28f);
    private static readonly Color Amber = new(1.00f, 0.82f, 0.35f);
    private static readonly Color Text = new(0.82f, 0.90f, 0.96f);
    private static readonly Color Muted = new(0.45f, 0.58f, 0.68f);
    private static readonly Color Red = new(1.00f, 0.38f, 0.30f);
    private static readonly Color Green = new(0.35f, 0.95f, 0.50f);
    private static readonly Color Blue = new(0.35f, 0.75f, 1.00f);

    private WorldState _world = null!;
    private SimulationEngine _engine = null!;
    private ControllerSlot _slotA = null!;
    private ControllerSlot _slotB = null!;
    private readonly List<DeviceControllerSlot> _deviceControllers = new();
    private ProgramEditor _editor = null!;
    private CanvasLayer _editorLayer = null!;
    private ColorRect _editorDimmer = null!;
    private WorkshopPanel _workshop = null!;
    private CanvasLayer _workshopLayer = null!;
    private string _deployedSourceA = string.Empty;
    private int _workshopSpawnIndex;
    private string _capturePath = "res://screenshots/m0-combat.png";
    private readonly List<TimedEvent> _history = new();

    private double _tickAccumulator;
    private bool _paused;
    private bool _captureRequested;
    private bool _captureEditor;
    private bool _captureWorkshop;
    private bool _captureVictory;
    private int _frame;
    private int _faultCount;
    private string _status = "booting";

    public override void _Ready()
    {
        var arguments = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).ToArray();
        GD.Print($"SEEDFIELD_READY args={string.Join(' ', arguments)}");

        ResetScenario();
        BuildProgramEditor();
        BuildWorkshopPanel();

        if (arguments.Contains("--save-smoke"))
        {
            RunSaveSmoke();
            return;
        }

        _captureEditor = arguments.Contains("--capture-editor");
        _captureWorkshop = arguments.Contains("--capture-workshop");
        _captureVictory = arguments.Contains("--capture-victory");
        _paused = arguments.Contains("--capture") ||
                  _captureEditor ||
                  _captureWorkshop ||
                  _captureVictory;
        _captureRequested = _paused;
        _capturePath = _captureEditor
            ? "res://screenshots/m0-program-editor.png"
            : _captureWorkshop
                ? "res://screenshots/m0-workshop.png"
                : _captureVictory
                    ? "res://screenshots/m0-victory.png"
                    : "res://screenshots/m0-combat.png";

        if (_captureEditor)
        {
            OpenProgramEditor();
            _editor.Source = Scenarios.ScriptA.Replace(
                "distance(me, target) <= me.weaponRangeMm",
                "distance(me, target) <= me.weaponRangeMm + 700");
            DeployProgram();
            for (var i = 0; i < 12; i++)
            {
                StepOnce();
            }

            _editor.SetStatus($"deployed v{_slotA.Version} · stepped to tick {_world.Tick}", false);
            _status = "program editor · paused";
        }
        else if (_captureWorkshop)
        {
            OpenWorkshop();
            DeployFromWorkshop("power");
            UpgradeFromWorkshop(new EntityId(1));
            _workshop.SelectBlueprint("power");
            _workshop.SelectInstance(new EntityId(1));
            _workshop.SetStatus(
                $"deployed power v1 · upgraded scout to v{_world.FindEntity(new EntityId(1))!.BlueprintVersion}",
                false);
            _status = "workshop · paused";
        }
        else if (_captureVictory)
        {
            for (var i = 0; i < 3_000 && _world.Match?.IsFinished != true; i++)
            {
                StepOnce();
            }

            _status = _world.Match is { IsFinished: true } match
                ? $"victory {match.WinnerCivilizationId} · tick {_world.Tick}"
                : $"no victory · tick {_world.Tick}";
        }
        else
        {
            _status = _paused ? "combat frame · capture" : "running";
        }

        QueueRedraw();
    }

    public override void _ExitTree()
    {
        foreach (var controller in _deviceControllers)
        {
            controller.Dispose();
        }

        _slotA?.Dispose();
        _slotB?.Dispose();
    }

    public override void _Process(double delta)
    {
        _frame++;

        if (_captureRequested && _frame >= 3)
        {
            CaptureScreenshot();
            return;
        }

        if (!_paused)
        {
            _tickAccumulator += delta * TicksPerSecond;
            var guard = 0;
            while (_tickAccumulator >= 1.0 && guard++ < 8)
            {
                StepOnce();
                _tickAccumulator -= 1.0;
            }
        }

        QueueRedraw();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        if (key.Keycode == Key.F5)
        {
            SaveGame();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F9)
        {
            LoadGame();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F2)
        {
            ToggleWorkshop();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F1)
        {
            ToggleProgramEditor();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_editorLayer.Visible && key.Keycode == Key.Enter && key.CtrlPressed)
        {
            DeployProgram();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key ||
            !key.Pressed ||
            key.Echo ||
            _editorLayer.Visible ||
            _workshopLayer.Visible)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.Space:
                _paused = !_paused;
                _status = _paused ? "paused" : "running";
                break;
            case Key.N:
                StepOnce();
                break;
            case Key.R:
                ResetScenario();
                _paused = true;
                _status = "reset";
                break;
            case Key.F12:
                CaptureScreenshot();
                break;
        }
    }

    private void BuildProgramEditor()
    {
        _editorLayer = new CanvasLayer { Layer = 20, Visible = false };
        AddChild(_editorLayer);

        _editorDimmer = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        _editorDimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _editorLayer.AddChild(_editorDimmer);

        _editor = new ProgramEditor();
        _editor.DeployRequested += DeployProgram;
        _editor.CloseRequested += CloseProgramEditor;
        _editorLayer.AddChild(_editor);
        _editor.Source = _deployedSourceA;
    }

    private void RebuildDeviceControllers()
    {
        foreach (var controller in _deviceControllers)
        {
            controller.Dispose();
        }

        _deviceControllers.Clear();

        foreach (var entity in _world.EnumerateEntities())
        {
            RegisterDeviceController(entity);
        }
    }

    private void RegisterDeviceController(EntityState entity)
    {
        var source = entity.CivilizationId == "A"
            ? _slotA.DeployedScript
            : _slotB.DeployedScript;
        _deviceControllers.Add(new DeviceControllerSlot(
            entity.Id,
            entity.CivilizationId,
            source));
    }

    private void BuildWorkshopPanel()
    {
        _workshopLayer = new CanvasLayer { Layer = 19, Visible = false };
        AddChild(_workshopLayer);

        var dimmer = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _workshopLayer.AddChild(dimmer);

        _workshop = new WorkshopPanel();
        _workshop.DeployRequested += DeployFromWorkshop;
        _workshop.UpgradeRequested += UpgradeFromWorkshop;
        _workshop.CloseRequested += CloseWorkshop;
        _workshopLayer.AddChild(_workshop);
        _workshop.Refresh(_world);
    }

    private void ToggleWorkshop()
    {
        if (_workshopLayer.Visible)
        {
            CloseWorkshop();
        }
        else
        {
            OpenWorkshop();
        }
    }

    private void OpenWorkshop()
    {
        if (_editorLayer.Visible)
        {
            CloseProgramEditor();
        }

        _workshopLayer.Visible = true;
        _workshop.Refresh(_world);
        _paused = true;
        _status = "workshop · paused";
        QueueRedraw();
    }

    private void CloseWorkshop()
    {
        _workshopLayer.Visible = false;
        _paused = _captureRequested;
        _status = _paused ? "capture" : "running";
        QueueRedraw();
    }

    private void DeployFromWorkshop(string blueprintId)
    {
        var versions = _world.Workshop.GetVersions(blueprintId);
        if (versions.Count == 0)
        {
            _workshop.SetStatus($"design '{blueprintId}' has no published version", true);
            return;
        }

        var latest = versions[^1];
        var result = _engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            latest,
            "frontier",
            NextWorkshopSpawn()));

        if (result.Success && result.EntityId is { } entityId)
        {
            var entity = _world.FindEntity(entityId);
            if (entity is not null)
            {
                RegisterDeviceController(entity);
            }
        }

        _workshop.SetStatus(
            result.Success
                ? result.EntityId is { } deployedId
                    ? $"deployed {blueprintId} v{latest.Version} as entity {deployedId}"
                    : $"queued {blueprintId} v{latest.Version}: {result.Message}"
                : $"deploy failed [{result.ErrorCode}]: {result.Message}",
            !result.Success);

        _workshop.Refresh(_world);
        QueueRedraw();
    }

    private void UpgradeFromWorkshop(EntityId entityId)
    {
        var entity = _world.FindEntity(entityId);
        if (entity is null || string.IsNullOrEmpty(entity.BlueprintId))
        {
            _workshop.SetStatus($"entity {entityId} has no blueprint reference", true);
            return;
        }

        var versions = _world.Workshop.GetVersions(entity.BlueprintId);
        if (versions.Count == 0)
        {
            _workshop.SetStatus($"blueprint '{entity.BlueprintId}' is missing", true);
            return;
        }

        var latest = versions[^1];
        if (latest.Version == entity.BlueprintVersion)
        {
            _workshop.SetStatus($"entity {entityId} is already at v{latest.Version}", false);
        }
        else
        {
            var result = _engine.ExecuteDeploymentCommand(new UpgradeCommand(
                "A",
                entityId,
                latest));
            _workshop.SetStatus(
                result.Success
                    ? $"upgraded entity {entityId}: {result.Message}"
                    : $"upgrade failed [{result.ErrorCode}]: {result.Message}",
                !result.Success);
        }

        _workshop.Refresh(_world);
        QueueRedraw();
    }

    private Vec2 NextWorkshopSpawn()
    {
        var index = _workshopSpawnIndex++;
        return new Vec2(
            1_200 + (index % 4 * 300),
            2_200 + (index / 4 * 300));
    }

    private void ToggleProgramEditor()
    {
        if (_editorLayer.Visible)
        {
            CloseProgramEditor();
        }
        else
        {
            OpenProgramEditor();
        }
    }

    private void OpenProgramEditor()
    {
        if (_workshopLayer.Visible)
        {
            CloseWorkshop();
        }

        _editorLayer.Visible = true;
        _paused = true;
        _status = "program editor · paused";
        _editor.FocusEditor();
        QueueRedraw();
    }

    private void CloseProgramEditor()
    {
        _editorLayer.Visible = false;
        _paused = _captureRequested;
        _status = _paused ? "capture" : "running";
        QueueRedraw();
    }

    private void DeployProgram()
    {
        var result = _slotA.Deploy(_editor.Source);
        if (result.Success)
        {
            _deployedSourceA = _editor.Source;
            RebuildDeviceControllers();
            _editor.SetStatus($"deployed v{result.Version} at tick {_world.Tick}", false);
        }
        else
        {
            _editor.SetStatus($"deploy failed; still v{result.Version}: {result.Error}", true);
        }

        QueueRedraw();
    }

    private void RunSaveSmoke()
    {
        var before = WorldStateHasher.Compute(_world);
        var tickBefore = _world.Tick;

        SaveGame();

        for (var i = 0; i < 10; i++)
        {
            StepOnce();
        }

        var mutated = WorldStateHasher.Compute(_world);
        LoadGame();
        var after = WorldStateHasher.Compute(_world);
        var ok = before == after && mutated != after && _world.Tick == tickBefore;

        GD.Print(
            $"SAVE_SMOKE ok={ok} before={before:x16} mutated={mutated:x16} after={after:x16} " +
            $"tick={_world.Tick} A=v{_slotA.Version} B=v{_slotB.Version}");

        GetTree().Quit();
    }

    private void SaveGame()
    {
        try
        {
            var json = GameSaveSerializer.Save(_world, new[] { _slotA, _slotB });
            System.IO.File.WriteAllText(SaveAbsolutePath, json);
            var message = $"saved tick {_world.Tick} · A v{_slotA.Version}";
            _status = message;
            if (_editorLayer.Visible)
            {
                _editor.SetStatus(message, false);
            }
        }
        catch (Exception exception)
        {
            _status = $"save failed: {exception.Message}";
            if (_editorLayer.Visible)
            {
                _editor.SetStatus(_status, true);
            }
        }

        QueueRedraw();
    }

    private void LoadGame()
    {
        try
        {
            if (!System.IO.File.Exists(SaveAbsolutePath))
            {
                _status = "no save file";
            }
            else
            {
                var loaded = GameSaveSerializer.Load(System.IO.File.ReadAllText(SaveAbsolutePath));
                var slotA = loaded.Controllers.FirstOrDefault(controller =>
                    controller.CivilizationId == "A");
                var slotB = loaded.Controllers.FirstOrDefault(controller =>
                    controller.CivilizationId == "B");

                if (slotA is null || slotB is null)
                {
                    foreach (var controller in loaded.Controllers)
                    {
                        controller.Dispose();
                    }

                    throw new SaveFormatException(
                        "MISSING_CONTROLLER",
                        "Save must contain controllers for civilizations A and B.");
                }

                foreach (var extra in loaded.Controllers.Where(controller =>
                             controller != slotA && controller != slotB))
                {
                    extra.Dispose();
                }

                _slotA?.Dispose();
                _slotB?.Dispose();
                _slotA = slotA;
                _slotB = slotB;
                _world = loaded.World;
                _engine = new SimulationEngine(_world);
                _deployedSourceA = _slotA.DeployedScript;
                RebuildDeviceControllers();
                _history.Clear();
                _faultCount = 0;
                _tickAccumulator = 0;
                _paused = true;
                _editor.Source = _deployedSourceA;
                _workshop?.Refresh(_world);

                _status = $"loaded tick {_world.Tick} · A v{_slotA.Version}";
            }
        }
        catch (Exception exception)
        {
            _status = $"load failed: {exception.Message}";
        }

        if (_editorLayer.Visible)
        {
            _editor.SetStatus(_status, _status.StartsWith("load failed", StringComparison.Ordinal));
        }

        QueueRedraw();
    }

    private void ResetScenario()
    {
        _slotA?.Dispose();
        _slotB?.Dispose();

        if (string.IsNullOrWhiteSpace(_deployedSourceA))
        {
            _deployedSourceA = Scenarios.ScriptA;
        }

        _world = Scenarios.BuildWorld();
        _engine = new SimulationEngine(_world);
        _slotA = new ControllerSlot("A", _deployedSourceA);
        _slotB = new ControllerSlot("B", BuiltInOpponents.Skirmisher.ProgramSource);
        RebuildDeviceControllers();

        _history.Clear();
        _faultCount = 0;
        _tickAccumulator = 0;
        _workshopSpawnIndex = 0;

        PreRunToCombat();
    }

    private void PreRunToCombat()
    {
        while (_world.Tick < PreRunMaxTicks && _world.Statistics.ShotsFired < PreRunMinShots)
        {
            StepOnce();
        }

        _status = $"combat at tick {_world.Tick}";
        GD.Print($"SEEDFIELD_PRERUN tick={_world.Tick} shots={_world.Statistics.ShotsFired} entities={_world.EnumerateEntities().Count()}");
        PruneHistory();
        QueueRedraw();
    }

    private void StepOnce()
    {
        var intents = new List<Intent>();

        foreach (var controller in _deviceControllers)
        {
            if (_world.FindEntity(controller.EntityId) is null)
            {
                continue;
            }

            var decision = controller.Decide(
                _engine.BuildDeviceObservation(controller.EntityId));
            intents.AddRange(decision.Intents);
            _faultCount += decision.Faults.Count;
        }

        var result = _engine.Step(intents);
        foreach (var simulationEvent in result.Events)
        {
            _history.Add(new TimedEvent(result.Tick, simulationEvent));
        }

        PruneHistory();
    }

    private void PruneHistory()
    {
        _history.RemoveAll(item => _world.Tick - item.Tick > EventRetentionTicks);
        if (_history.Count > 400)
        {
            _history.RemoveRange(0, _history.Count - 400);
        }
    }

    private void CaptureScreenshot()
    {
        RenderingServer.ForceDraw();
        var image = GetViewport().GetTexture().GetImage();
        var path = ProjectSettings.GlobalizePath(_capturePath);
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        var error = image.SavePng(path);
        GD.Print($"SCREENSHOT_SAVED {path} ({error})");
        _captureRequested = false;
        GetTree().Quit();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, ViewWidth, ViewHeight), Background);

        DrawGrid();
        DrawStagingZones();
        DrawControlPoint();
        DrawRanges();
        DrawTracers();
        DrawMessages();
        DrawEntities();
        DrawHud();
    }

    private void DrawGrid()
    {
        for (var x = 0; x <= Scenarios.WidthMm; x += 1_000)
        {
            var major = x % 4_000 == 0;
            DrawLine(
                Map(new Vec2(x, 0)),
                Map(new Vec2(x, Scenarios.HeightMm)),
                major ? GridMajor : Grid,
                major ? 1.2f : 0.7f);
        }

        for (var y = 0; y <= Scenarios.HeightMm; y += 1_000)
        {
            var major = y % 4_000 == 0;
            DrawLine(
                Map(new Vec2(0, y)),
                Map(new Vec2(Scenarios.WidthMm, y)),
                major ? GridMajor : Grid,
                major ? 1.2f : 0.7f);
        }

        DrawRect(
            new Rect2(MapOrigin, new Vector2(Scenarios.WidthMm * MapScale, Scenarios.HeightMm * MapScale)),
            Border,
            filled: false,
            width: 1.6f,
            antialiased: true);

        DrawString(
            HudFont,
            MapOrigin + new Vector2(0, -10),
            "FRONTIER  ·  16 000 × 8 000 mm  ·  authoritative C# SimulationCore",
            HorizontalAlignment.Left,
            -1,
            12,
            Muted);
    }

    private void DrawStagingZones()
    {
        DrawArc(Map(new Vec2(2_000, 4_000)), 1_500 * MapScale, 0, Mathf.Tau, 72, new Color(Cyan, 0.22f), 2f, true);
        DrawArc(Map(new Vec2(13_000, 4_000)), 1_500 * MapScale, 0, Mathf.Tau, 72, new Color(Orange, 0.22f), 2f, true);

        DrawString(
            HudFont,
            Map(new Vec2(2_000, 4_000)) + new Vector2(-62, 74),
            "A · STAGING",
            HorizontalAlignment.Left,
            -1,
            12,
            new Color(Cyan, 0.75f));
        DrawString(
            HudFont,
            Map(new Vec2(13_000, 4_000)) + new Vector2(-62, 74),
            "B · STAGING",
            HorizontalAlignment.Left,
            -1,
            12,
            new Color(Orange, 0.75f));
    }

    private void DrawControlPoint()
    {
        foreach (var point in _world.ControlPoints.Values)
        {
            var center = Map(point.Position);
            var radius = point.RadiusMm * MapScale;
            var color = point.OwnerCivilizationId switch
            {
                "A" => Cyan,
                "B" => Orange,
                _ => Amber,
            };

            DrawCircle(center, radius, new Color(color, 0.05f));
            DrawArc(center, radius, 0, Mathf.Tau, 80, new Color(color, 0.6f), 2f, true);
            DrawCircle(center, 5f, new Color(color, 0.8f));

            var name = point.Id.Replace("relay-", string.Empty).ToUpperInvariant();
            var label = point.IsCaptured
                ? $"{name} · {point.OwnerCivilizationId}"
                : point.HoldingCivilizationId is null
                    ? $"{name} · CONTESTED"
                    : $"{name} · {point.HoldingCivilizationId} " +
                      $"{point.HoldTicks}/{point.HoldTicksRequired}";

            DrawString(
                HudFont,
                center + new Vector2(-46, -radius - 10),
                label,
                HorizontalAlignment.Left,
                -1,
                11,
                color);
        }
    }

    private void DrawRanges()
    {
        foreach (var entity in _world.EnumerateEntities())
        {
            var center = Map(entity.Position);
            var color = CivilizationColor(entity.CivilizationId);

            if (entity.Sensor is { RangeMm: > 0 } sensor)
            {
                DrawArc(
                    center,
                    sensor.RangeMm * MapScale,
                    0,
                    Mathf.Tau,
                    80,
                    new Color(color, 0.16f),
                    1.1f,
                    true);
            }

            if (entity.Weapon is { RangeMm: > 0 } weapon)
            {
                DrawArc(
                    center,
                    weapon.RangeMm * MapScale,
                    0,
                    Mathf.Tau,
                    80,
                    new Color(Red, 0.11f),
                    1f,
                    true);
            }
        }
    }

    private void DrawTracers()
    {
        foreach (var item in _history)
        {
            if (item.Event is not ShotFired shot)
            {
                continue;
            }

            var age = _world.Tick - item.Tick;
            if (age < 0 || age > 8)
            {
                continue;
            }

            var alpha = 1f - (age / 8f);
            var from = Map(shot.From);
            var target = Map(shot.Target);

            DrawLine(from, target, new Color(1f, 0.45f, 0.20f, alpha), 2.6f, true);
            DrawCircle(target, 4f + (age * 0.9f), new Color(1f, 0.58f, 0.24f, alpha * 0.85f));
            DrawCircle(from, 2.4f, new Color(1f, 0.86f, 0.55f, alpha));
        }
    }

    private void DrawMessages()
    {
        foreach (var item in _history)
        {
            if (item.Event is not MessageQueued queued)
            {
                continue;
            }

            var age = _world.Tick - item.Tick;
            if (age < 0 || age > 4)
            {
                continue;
            }

            var sender = _world.FindEntity(queued.FromEntityId);
            if (sender is null)
            {
                continue;
            }

            var senderPosition = Map(sender.Position);
            var alpha = 1f - (age / 4f);
            DrawArc(
                senderPosition,
                8f + (age * 4f),
                0,
                Mathf.Tau,
                36,
                new Color(Amber, alpha * 0.85f),
                2f,
                true);

            var mate = _world.EnumerateEntities().FirstOrDefault(entity =>
                entity.Id != sender.Id &&
                string.Equals(entity.CivilizationId, sender.CivilizationId, StringComparison.Ordinal));

            if (mate is not null)
            {
                DrawLine(
                    senderPosition,
                    Map(mate.Position),
                    new Color(Amber, alpha * 0.22f),
                    1.2f,
                    true);
            }
        }
    }

    private void DrawEntities()
    {
        foreach (var entity in _world.EnumerateEntities())
        {
            var position = Map(entity.Position);
            var color = CivilizationColor(entity.CivilizationId);

            DrawCircle(position, 12f, new Color(color, 0.14f));
            DrawCircle(position, 6.5f, color);
            DrawArc(position, 10.5f, 0, Mathf.Tau, 36, color, 1.4f, true);

            DrawBar(position + new Vector2(-13f, -21f), 26f, 3f, (float)entity.Health / entity.MaxHealth, Green);
            DrawBar(position + new Vector2(-13f, 17f), 26f, 2.5f, (float)entity.Energy / entity.EnergyCapacity, Blue);

            DrawString(
                HudFont,
                position + new Vector2(-19f, 34f),
                $"{entity.CivilizationId}{entity.Id.Value}",
                HorizontalAlignment.Left,
                -1,
                11,
                new Color(color, 0.9f));
        }
    }

    private void DrawHud()
    {
        var stats = _world.Statistics;

        DrawString(HudFont, new Vector2(24, 40), "SEED FIELD / 种子域", HorizontalAlignment.Left, -1, 22, Text);
        DrawString(
            HudFont,
            new Vector2(24, 62),
            "M0 visual slice — Godot 4.7.2 Mono · .NET 10 · Jint 4.18.0",
            HorizontalAlignment.Left,
            -1,
            12,
            Muted);

        DrawString(
            HudFont,
            new Vector2(24, 88),
            $"tick {_world.Tick}   entities {_world.EnumerateEntities().Count()}   " +
            $"shots {stats.ShotsFired}   damage {stats.DamageDealt}   " +
            $"messages {stats.MessagesDelivered}   E spent {stats.EnergySpentTotal}   " +
            $"faults {_faultCount}   A program v{_slotA.Version}",
            HorizontalAlignment.Left,
            -1,
            13,
            Text);

        DrawString(
            HudFont,
            new Vector2(1_120, 40),
            _paused ? "PAUSED" : "RUNNING",
            HorizontalAlignment.Left,
            -1,
            15,
            _paused ? Amber : Green);
        DrawString(HudFont, new Vector2(1_120, 60), _status, HorizontalAlignment.Left, 150, 11, Muted);

        if (_world.Match is { } match)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var point in _world.ControlPoints.Values)
            {
                if (point.OwnerCivilizationId is { } owner)
                {
                    counts[owner] = counts.GetValueOrDefault(owner) + 1;
                }
            }

            var matchText = match.IsFinished
                ? $"MATCH · WINNER {match.WinnerCivilizationId}"
                : $"MATCH · A {counts.GetValueOrDefault("A")} / " +
                  $"B {counts.GetValueOrDefault("B")} · first to {match.Rules.CapturedPointsToWin}";
            DrawString(
                HudFont,
                new Vector2(24, 112),
                matchText,
                HorizontalAlignment.Left,
                -1,
                12,
                match.IsFinished ? Green : Amber);
        }

        var ready = _world.Workshop.IsMinimumUsable;
        DrawString(
            HudFont,
            new Vector2(1_120, 82),
            ready ? "BRANCHES 5/5 · PvE READY" : "BRANCHES INCOMPLETE",
            HorizontalAlignment.Left,
            -1,
            10,
            ready ? Green : Amber);

        DrawLegend();
        DrawEventLog();
        DrawControls();
    }

    private void DrawLegend()
    {
        var x = 1_120f;
        var y = 108f;
        DrawString(HudFont, new Vector2(x, y), "LEGEND", HorizontalAlignment.Left, -1, 12, Muted);

        DrawCircle(new Vector2(x + 5, y + 22), 5f, Cyan);
        DrawString(HudFont, new Vector2(x + 18, y + 27), "civilization A", HorizontalAlignment.Left, -1, 12, Text);

        DrawCircle(new Vector2(x + 5, y + 44), 5f, Orange);
        DrawString(HudFont, new Vector2(x + 18, y + 49), "civilization B", HorizontalAlignment.Left, -1, 12, Text);

        DrawArc(new Vector2(x + 5, y + 68), 9f, 0, Mathf.Tau, 24, new Color(Cyan, 0.55f), 1.2f);
        DrawString(HudFont, new Vector2(x + 18, y + 73), "sensor range", HorizontalAlignment.Left, -1, 12, Text);

        DrawArc(new Vector2(x + 5, y + 92), 9f, 0, Mathf.Tau, 24, new Color(Red, 0.55f), 1.2f);
        DrawString(HudFont, new Vector2(x + 18, y + 97), "weapon range", HorizontalAlignment.Left, -1, 12, Text);

        DrawLine(new Vector2(x, y + 112), new Vector2(x + 11, y + 112), new Color(1f, 0.55f, 0.25f), 2.4f);
        DrawString(HudFont, new Vector2(x + 18, y + 117), "shot event", HorizontalAlignment.Left, -1, 12, Text);

        DrawArc(new Vector2(x + 5, y + 138), 7f, 0, Mathf.Tau, 24, new Color(Amber, 0.8f), 1.6f);
        DrawString(HudFont, new Vector2(x + 18, y + 143), "message queued", HorizontalAlignment.Left, -1, 12, Text);
    }

    private void DrawEventLog()
    {
        DrawString(HudFont, new Vector2(24, 612), "EVENT LOG · authoritative simulation events", HorizontalAlignment.Left, -1, 12, Muted);

        var entries = _history
            .Where(item => IsLogworthy(item.Event))
            .TakeLast(5)
            .ToList();

        var y = 636f;
        foreach (var entry in entries)
        {
            DrawString(
                HudFont,
                new Vector2(24, y),
                Describe(entry.Event),
                HorizontalAlignment.Left,
                -1,
                12,
                EventColor(entry.Event));
            y += 15f;
        }

        if (entries.Count == 0)
        {
            DrawString(HudFont, new Vector2(24, y), "no combat or communication yet", HorizontalAlignment.Left, -1, 12, Muted);
        }
    }

    private void DrawControls()
    {
        DrawString(
            HudFont,
            new Vector2(24, 716),
            $"OPPONENT {BuiltInOpponents.Skirmisher.Manifest.PackageId} " +
            $"v{BuiltInOpponents.Skirmisher.Manifest.Version} · " +
            "SPACE pause · N step · F1 program · F2 workshop · F5 save · F9 load · F12 screenshot",
            HorizontalAlignment.Left,
            -1,
            11,
            Muted);
    }

    private void DrawBar(Vector2 position, float width, float height, float fraction, Color color)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);
        DrawRect(new Rect2(position, new Vector2(width, height)), new Color(0.13f, 0.18f, 0.23f));
        DrawRect(new Rect2(position, new Vector2(width * fraction, height)), color);
    }

    private static Color CivilizationColor(string civilizationId) =>
        string.Equals(civilizationId, "A", StringComparison.Ordinal) ? Cyan : Orange;

    private static bool IsLogworthy(SimulationEvent simulationEvent) =>
        simulationEvent is ShotFired
            or EntityDestroyed
            or MessageDelivered
            or MessageQueued
            or CoreRegenerated
            or EntityBootstrapped
            or ControlPointCaptured
            or MatchEnded;

    private static Color EventColor(SimulationEvent simulationEvent) =>
        simulationEvent switch
        {
            ShotFired => new Color(1f, 0.58f, 0.30f),
            EntityDestroyed => Red,
            MessageDelivered => Amber,
            MessageQueued => new Color(0.75f, 0.66f, 0.40f),
            _ => Text,
        };

    private static string Describe(SimulationEvent simulationEvent) =>
        simulationEvent switch
        {
            ShotFired shot =>
                $"t{shot.Tick,4}  {shot.AttackerId} → {shot.TargetId}   hit {shot.Damage}   −{shot.EnergySpent}E",
            EntityDestroyed destroyed => $"t{destroyed.Tick,4}  {destroyed.EntityId} destroyed",
            MessageDelivered delivered =>
                $"t{delivered.Tick,4}  message {delivered.MessageId} delivered to {delivered.ToCivilizationId}",
            MessageQueued queued =>
                $"t{queued.Tick,4}  message {queued.MessageId} queued → t{queued.DeliverTick}",
            CoreRegenerated core =>
                $"t{core.Tick,4}  core {core.CivilizationId} +{core.EnergyAdded}E +{core.MaterialAdded}M",
            EntityBootstrapped bootstrapped =>
                $"t{bootstrapped.Tick,4}  {bootstrapped.BlueprintName} bootstrapped from core",
            ControlPointCaptured captured =>
                $"t{captured.Tick,4}  {captured.ControlPointId} captured by {captured.CivilizationId}",
            MatchEnded ended =>
                $"t{ended.Tick,4}  match won by {ended.CivilizationId} ({ended.CapturedPoints} points)",
            _ => $"t{simulationEvent.Tick,4}  {simulationEvent.GetType().Name}",
        };

    private static Vector2 Map(Vec2 position) =>
        MapOrigin + new Vector2(position.X * MapScale, position.Y * MapScale);

    private static string SaveAbsolutePath => ProjectSettings.GlobalizePath(SavePath);

    private static Font HudFont => ThemeDB.FallbackFont;

    private sealed record TimedEvent(long Tick, SimulationEvent Event);
}
