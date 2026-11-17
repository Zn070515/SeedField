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
    private int _frame;
    private int _faultCount;
    private string _status = "booting";
    private bool _peacefulScenario;

    public override void _Ready()
    {
        var arguments = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).ToArray();
        GD.Print($"SEEDFIELD_READY args={string.Join(' ', arguments)}");

        _opponent = arguments.Contains("--opponent-turtle")
            ? BuiltInOpponents.Turtle
            : BuiltInOpponents.Skirmisher;
        _peacefulScenario = arguments.Contains("--peaceful");

        ResetScenario(_peacefulScenario);
        BuildProgramEditor();
        BuildWorkshopPanel();
        BuildDiplomacyPanel();
        BuildPowerPanel();
        BuildRecoveryPanel();
        InitializePresentation(arguments);

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
        _tacticalVisible = _captureTactical;
        _debugVisible = _captureDebug || _captureOmniscient;
        _omniscientDebug |= _captureOmniscient;
        _paused = arguments.Contains("--capture") ||
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
                  _captureOmniscient;
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
            TreatyProposed proposed =>
                $"t{proposed.Tick,4}  {proposed.ProposerCivilizationId} → " +
                $"{proposed.TargetCivilizationId} propose {KindLabel(proposed.Kind)}",
            TreatyAccepted accepted =>
                $"t{accepted.Tick,4}  {accepted.CivilizationA} ↔ " +
                $"{accepted.CivilizationB} {KindLabel(accepted.Kind)} accepted",
            TreatyRejected rejected =>
                $"t{rejected.Tick,4}  {rejected.ProposerCivilizationId} → " +
                $"{rejected.TargetCivilizationId} {KindLabel(rejected.Kind)} rejected",
            TreatyBroken broken =>
                $"t{broken.Tick,4}  {KindLabel(broken.Kind)} broken by " +
                $"{broken.BrokenByCivilizationId}",
            TreatyProposalExpired expired =>
                $"t{expired.Tick,4}  {KindLabel(expired.Kind)} proposal " +
                $"{expired.ProposalId} expired",
            TradeProposed proposed =>
                $"t{proposed.Tick,4}  trade {proposed.TradeOfferId} proposed: " +
                $"give M{proposed.GiveMaterial}/E{proposed.GiveEnergy} → " +
                $"want M{proposed.WantMaterial}/E{proposed.WantEnergy}",
            TradeAccepted accepted =>
                $"t{accepted.Tick,4}  trade {accepted.TradeOfferId} settled " +
                $"M{accepted.GiveMaterial + accepted.WantMaterial}/" +
                $"E{accepted.GiveEnergy + accepted.WantEnergy}",
            TradeRejected rejected =>
                $"t{rejected.Tick,4}  trade {rejected.TradeOfferId} rejected; " +
                $"refund M{rejected.RefundedMaterial}/E{rejected.RefundedEnergy}",
            TradeCancelled cancelled =>
                $"t{cancelled.Tick,4}  trade {cancelled.TradeOfferId} cancelled; " +
                $"refund M{cancelled.RefundedMaterial}/E{cancelled.RefundedEnergy}",
            TradeExpired expired =>
                $"t{expired.Tick,4}  trade {expired.TradeOfferId} expired; " +
                $"refund M{expired.RefundedMaterial}/E{expired.RefundedEnergy}",
            TradeCountered countered =>
                $"t{countered.Tick,4}  trade {countered.OriginalTradeOfferId} → " +
                $"{countered.NewTradeOfferId} countered by {countered.CivilizationId} " +
                $"R{countered.Round} give M{countered.GiveMaterial}/E{countered.GiveEnergy} → " +
                $"want M{countered.WantMaterial}/E{countered.WantEnergy}",
            PowerFlowed flowed =>
                $"t{flowed.Tick,4}  power {flowed.CivilizationId} " +
                $"{flowed.FromRegionId} → {flowed.ToRegionId} +{flowed.Amount}E",
            PowerShipped shipped =>
                $"t{shipped.Tick,4}  power {shipped.CivilizationId} " +
                $"{shipped.FromRegionId} → {shipped.ToRegionId} " +
                $"{shipped.Amount}E shipped · arrives t{shipped.ArriveTick}",
            PowerArrived arrived =>
                $"t{arrived.Tick,4}  power {arrived.CivilizationId} arrived " +
                $"{arrived.ToRegionId} +{arrived.Amount}E" +
                (arrived.LostAmount > 0 ? $" (−{arrived.LostAmount}E loss)" : string.Empty),
            PowerShortfall shortfall =>
                $"t{shortfall.Tick,4}  {shortfall.CivilizationId} power shortfall " +
                $"{shortfall.EntityId} in {shortfall.RegionId}: need {shortfall.Requested}E, " +
                $"have {shortfall.Available}E ({shortfall.Source})",
            EnergyAutoDrawn autoDrawn =>
                $"t{autoDrawn.Tick,4}  {autoDrawn.EntityId} auto-drew {autoDrawn.Amount}E " +
                $"from {autoDrawn.RegionId}",
            EnergyWasted wasted =>
                $"t{wasted.Tick,4}  {wasted.CivilizationId} wasted {wasted.Amount}E " +
                $"in {wasted.RegionId} ({wasted.Source})",
            PowerBlackout blackout =>
                $"t{blackout.Tick,4}  {blackout.CivilizationId} breaker opened " +
                $"in {blackout.RegionId}",
            PowerRestored restored =>
                $"t{restored.Tick,4}  {restored.CivilizationId} breaker closed " +
                $"in {restored.RegionId} (grid {restored.GridEnergy})",
            BreakerLockedOut lockedOut =>
                $"t{lockedOut.Tick,4}  {lockedOut.CivilizationId} breaker locked out " +
                $"in {lockedOut.RegionId} after {lockedOut.TripCount} trips " +
                $"({lockedOut.Reason})",
            BreakerMaintained maintained =>
                $"t{maintained.Tick,4}  {maintained.CivilizationId} breaker maintained " +
                $"in {maintained.RegionId} (−{maintained.MaterialSpent}M)",
            RecoveryQuotaDenied quota =>
                $"t{quota.Tick,4}  {quota.CivilizationId} recovery quota exhausted " +
                $"in {quota.RegionId} ({quota.Used}/{quota.Limit}, reset in {quota.ResetsInTicks}t)",
            WreckCreated wreck =>
                $"t{wreck.Tick,4}  wreck {wreck.WreckId} in {wreck.RegionId}: " +
                $"{wreck.BlueprintId} v{wreck.BlueprintVersion} · {wreck.MaterialRecoverable}M",
            WreckSalvaged salvaged =>
                $"t{salvaged.Tick,4}  {salvaged.CivilizationId} salvaged wreck " +
                $"{salvaged.WreckId} in {salvaged.RegionId} (+{salvaged.MaterialRecovered}M)",
            _ => $"t{simulationEvent.Tick,4}  {simulationEvent.GetType().Name}",
        };

    internal static string KindLabel(TreatyKind kind) =>
        kind == TreatyKind.NonAggression ? "NAP" : "TRADE";

    private static Vector2 Map(Vec2 position) =>
        MapOrigin + new Vector2(position.X * MapScale, position.Y * MapScale);

    private static string SaveAbsolutePath => ProjectSettings.GlobalizePath(SavePath);

    private static Font HudFont => ThemeDB.FallbackFont;

    private sealed record TimedEvent(long Tick, SimulationEvent Event);
}
