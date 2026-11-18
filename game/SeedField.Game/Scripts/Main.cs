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
    internal const float BaseMapScale = 0.055f;
    private const float MinZoom = 0.70f;
    private const float MaxZoom = 1.35f;

    private const string SavePath = "user://seedfield-save.json";

    private static readonly Vector2 BaseMapOrigin = new(200f, 140f);

    private float _zoom = 1f;
    private Vector2 _pan;
    private bool _panning;

    private float MapScale => BaseMapScale * _zoom;

    private Vector2 MapOrigin => BaseMapOrigin + _pan;

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
    private TutorialProgress _tutorial = new("A");
    private ControllerSlot _slotA = null!;
    private ControllerSlot _slotB = null!;
    private readonly List<DeviceControllerSlot> _deviceControllers = new();
    private readonly ControllerBatchRunner _controllerBatch = new();
    private OpponentPackage _opponent = BuiltInOpponents.Skirmisher;
    private DiplomacyProfile _opponentDiplomacy = OpponentDiplomacyProfiles.Default;
    private ProgramEditor _editor = null!;
    private CanvasLayer _editorLayer = null!;
    private ColorRect _editorDimmer = null!;
    private WorkshopPanel _workshop = null!;
    private CanvasLayer _workshopLayer = null!;
    private DiplomacyPanel _diplomacy = null!;
    private CanvasLayer _diplomacyLayer = null!;
    private PowerPanel _power = null!;
    private CanvasLayer _powerLayer = null!;
    private RecoveryPanel _recovery = null!;
    private CanvasLayer _recoveryLayer = null!;
    private string _deployedSourceA = string.Empty;
    private int _workshopSpawnIndex;
    private string _capturePath = "res://screenshots/m0-combat.png";
    private readonly List<TimedEvent> _history = new();

    private double _tickAccumulator;
    private bool _paused;
    private bool _captureRequested;
    private bool _captureEditor;
    private bool _captureWorkshop;
    private bool _captureDiplomacy;
    private bool _captureDiplomacyAi;
    private bool _capturePower;
    private bool _captureRecovery;
    private bool _captureVictory;
    private bool _captureVisual;
    private bool _captureTactical;
    private bool _captureDebug;
    private bool _captureOmniscient;
    private bool _captureView;
    private int _frame;
    private int _faultCount;
    private string _status = "booting";
    private bool _peacefulScenario;

    public override void _Ready()
    {
        var arguments = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).ToArray();
        GD.Print(
            $"SEEDFIELD_READY args={string.Join(' ', arguments)} " +
            $"gcServer={System.Runtime.GCSettings.IsServerGC}");
        if (!System.Runtime.GCSettings.IsServerGC)
        {
            GD.PushWarning("Server GC is not active. Launch with DOTNET_gcServer=1 (Godot ignores the project runtimeconfig).");
        }

        _opponent = arguments.Contains("--opponent-turtle")
            ? BuiltInOpponents.Turtle
            : BuiltInOpponents.Skirmisher;
        _peacefulScenario = arguments.Contains("--peaceful");
        var jintBenchmarkArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--benchmark-jint=", StringComparison.Ordinal));
        if (jintBenchmarkArgument is not null &&
            int.TryParse(jintBenchmarkArgument["--benchmark-jint=".Length..], out var jintDevices))
        {
            _benchmarkJint = true;
            _benchmarkJintDevices = Mathf.Clamp(jintDevices, 2, 100);
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        }

        var benchmarkArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--benchmark-main=", StringComparison.Ordinal));
        if (benchmarkArgument is not null &&
            int.TryParse(benchmarkArgument["--benchmark-main=".Length..], out var benchmarkDevices))
        {
            _benchmarkMain = true;
            _benchmarkDevices = Mathf.Clamp(benchmarkDevices, 1, 100);
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        }

        ResetScenario(_peacefulScenario, preRun: RequiresPreRun(arguments));
        BuildProgramEditor();
        BuildWorkshopPanel();
        BuildDiplomacyPanel();
        BuildPowerPanel();
        BuildRecoveryPanel();
        InitializePresentation(arguments);

        if (_benchmarkJint)
        {
            ConfigureJintBenchmark();
            return;
        }

        var coreStateArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--core-state=", StringComparison.Ordinal));
        if (coreStateArgument is not null)
        {
            ConfigureCoreStateScenario(coreStateArgument["--core-state=".Length..]);
            return;
        }

        if (arguments.Contains("--capture-module-swap"))
        {
            ConfigureModuleSwapCapture();
            return;
        }

        if (arguments.Contains("--event-visibility-smoke"))
        {
            RunEventVisibilitySmoke();
            return;
        }

        if (arguments.Contains("--power-visual-smoke"))
        {
            RunPowerVisualSmoke();
            return;
        }

        if (arguments.Contains("--visibility-smoke"))
        {
            RunVisibilitySmoke();
            return;
        }

        if (arguments.Contains("--module-swap-smoke"))
        {
            RunModuleSwapSmoke();
            return;
        }

        if (arguments.Contains("--core-state-smoke"))
        {
            RunCoreStateSmoke();
            return;
        }

        if (arguments.Contains("--save-smoke"))
        {
            RunSaveSmoke();
            return;
        }

        if (arguments.Contains("--tutorial-smoke")) { RunTutorialSmoke(); return; }

        if (arguments.Contains("--diplomacy-smoke"))
        {
            RunDiplomacySmoke();
            return;
        }

        if (arguments.Contains("--diplomacy-ai-smoke"))
        {
            RunDiplomacyAiSmoke();
            return;
        }

        if (arguments.Contains("--diplomacy-counter-smoke"))
        {
            RunDiplomacyCounterSmoke();
            return;
        }

        if (arguments.Contains("--power-breaker-smoke"))
        {
            RunPowerBreakerSmoke();
            return;
        }

        if (arguments.Contains("--recovery-smoke"))
        {
            RunRecoverySmoke();
            return;
        }

        _captureEditor = arguments.Contains("--capture-editor");
        _captureWorkshop = arguments.Contains("--capture-workshop");
        _captureDiplomacy = arguments.Contains("--capture-diplomacy");
        _captureDiplomacyAi = arguments.Contains("--capture-diplomacy-ai");
        _capturePower = arguments.Contains("--capture-power");
        _captureRecovery = arguments.Contains("--capture-recovery");
        _captureVictory = arguments.Contains("--capture-victory");
        _captureVisual = arguments.Contains("--capture-visual");
        _captureTactical = arguments.Contains("--capture-tactical");
        _captureDebug = arguments.Contains("--capture-debug");
        _captureOmniscient = arguments.Contains("--capture-omniscient");
        _captureView = arguments.Contains("--capture-view");
        _tacticalVisible = _captureTactical;
        _debugVisible = _captureDebug || _captureOmniscient;
        _omniscientDebug |= _captureOmniscient;
        _paused = _benchmarkMain || arguments.Contains("--capture") ||
                  _captureEditor ||
                  _captureWorkshop ||
                  _captureDiplomacy ||
                  _captureDiplomacyAi ||
                  _capturePower ||
                  _captureRecovery ||
                  _captureVictory ||
                  _captureVisual ||
                  _captureTactical ||
                  _captureDebug ||
                  _captureOmniscient ||
                  _captureView;
        _captureRequested = _paused;
        _capturePath = _captureEditor
            ? "res://screenshots/m0-program-editor.png"
            : _captureWorkshop
                ? "res://screenshots/m0-workshop.png"
                : _captureDiplomacyAi
                    ? "res://screenshots/m0-diplomacy-ai.png"
                    : _captureDiplomacy
                        ? "res://screenshots/m0-diplomacy.png"
                        : _capturePower
                            ? "res://screenshots/m0-power.png"
                            : _captureRecovery
                                ? "res://screenshots/m0-recovery.png"
                                : _captureVictory
                                    ? "res://screenshots/m0-victory.png"
                                    : _captureVisual
                                        ? "res://screenshots/art-main-combat.png"
                                        : _captureTactical
                                            ? "res://screenshots/art-main-tactical.png"
                                            : _captureDebug
                                                ? "res://screenshots/art-main-debug.png"
                                                : _captureOmniscient
                                                    ? "res://screenshots/art-main-omniscient.png"
                                                    : _captureView
                                                        ? _zoom < 0.9f
                                                            ? "res://screenshots/art-main-far.png"
                                                            : _zoom > 1.1f
                                                                ? "res://screenshots/art-main-near.png"
                                                                : "res://screenshots/art-main-mid.png"
                                                        : "res://screenshots/m0-combat.png";
        UpdateFacing();

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
            var scoutForCapture = _world.EnumerateEntities().FirstOrDefault(entity =>
                entity.CivilizationId == "A" &&
                string.Equals(entity.BlueprintId, "scout", StringComparison.Ordinal));
            var scoutId = scoutForCapture?.Id ?? new EntityId(1);
            UpgradeFromWorkshop(scoutId);
            _workshop.SelectBlueprint("power");
            _workshop.SelectInstance(scoutId);
            var upgradedScout = _world.FindEntity(scoutId);
            _workshop.SetStatus(
                $"queued power v1 (t{_world.ConstructionOrders.FirstOrDefault()?.CompleteTick}) " +
                $"· upgraded scout to v{upgradedScout?.BlueprintVersion ?? 0}",
                false);
            _status = "workshop · paused";
        }
        else if (_captureDiplomacy)
        {
            OpenDiplomacy();
            SeedDiplomacyCapture();
            _status = "diplomacy · paused";
        }
        else if (_captureDiplomacyAi)
        {
            SeedDiplomacyAiCapture();
            OpenDiplomacy();
            var napAccepted = _world.Treaties.Values.Any(treaty =>
                treaty.Kind == TreatyKind.NonAggression);
            var countered = _world.TradeOffers.Values.Any(offer => offer.Round > 1);
            _diplomacy.SetStatus(
                $"B responded: trade treaty accepted, " +
                $"{(countered ? "offer countered at R2" : "offer settled")}, " +
                $"NAP {(napAccepted ? "accepted" : "rejected")}",
                false);
            _status = "diplomacy AI · paused";
        }
        else if (_capturePower)
        {
            OpenPower();
            var frontier = _world.Regions["frontier"];
            var stock = frontier.GetEnergy("A");
            frontier.TryConsumeEnergy("A", stock);
            RefreshPower();
            _power.SetStatus(
                "frontier grid drained · local buffers still active",
                false);
            _status = "power diagnostics · paused";
        }
        else if (_captureRecovery)
        {
            OpenRecovery();
            for (var i = 0; i < 40; i++)
            {
                StepOnce();
            }

            RecoverFromPanel();
            _status = "core recovery · paused";
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
        TrackMixedBenchmark();
        if (_benchmarkJint)
        {
            RunJintBenchmarkTick();
            QueueRedraw();
            return;
        }
        if (_benchmarkMain)
        {
            UpdateMainBenchmark(delta);
            QueueRedraw();
            return;
        }

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

    private bool AnyOverlayVisible =>
        _editorLayer.Visible ||
        _workshopLayer.Visible ||
        _diplomacyLayer.Visible ||
        _powerLayer.Visible ||
        _recoveryLayer.Visible;

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                _panning = mouseButton.Pressed && !AnyOverlayVisible;
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!AnyOverlayVisible &&
                mouseButton.Pressed &&
                mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomAt(mouseButton.Position, 1.12f);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!AnyOverlayVisible &&
                mouseButton.Pressed &&
                mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomAt(mouseButton.Position, 1f / 1.12f);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (!AnyOverlayVisible && @event is InputEventMouseMotion motion && _panning)
        {
            _pan += motion.Relative;
            QueueRedraw();
            GetViewport().SetInputAsHandled();
            return;
        }

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

        if (key.Keycode == Key.F3)
        {
            ToggleDiplomacy();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F4)
        {
            TogglePower();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F6)
        {
            ToggleRecovery();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F7)
        {
            _tacticalVisible = !_tacticalVisible;
            QueueRedraw();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F8)
        {
            _debugVisible = !_debugVisible;
            QueueRedraw();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F10)
        {
            _omniscientDebug = !_omniscientDebug;
            UpdateFacing();
            QueueRedraw();
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
            _workshopLayer.Visible ||
            _diplomacyLayer.Visible ||
            _powerLayer.Visible ||
            _recoveryLayer.Visible)
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
                ResetScenario(_peacefulScenario);
                _paused = true;
                _status = "reset";
                break;
            case Key.F12:
                CaptureScreenshot();
                break;
        }
    }

    private sealed record TimedEvent(long Tick, SimulationEvent Event);
}
