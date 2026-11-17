using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main
{
    private readonly Dictionary<long, FacingState> _facing = new();
    private readonly Dictionary<long, long> _hitUntil = new();
    private readonly List<VisualEntitySnapshot> _visibleEntities = new();
    private readonly HashSet<long> _visibleEntityIds = new();
    private readonly VisualEffectFeed _effects = new();
    private VisualAssetCatalog _visualCatalog = null!;
    private VisualAssetCatalog _worldCatalog = null!;
    private EntityVisual _entityVisual = null!;
    private CoreVisual _coreVisual = null!;
    private long _bootstrapUntilTick;
    private bool _tacticalVisible;
    private bool _debugVisible;
    private bool _classicVisuals;
    private bool _omniscientDebug;

    private readonly record struct FacingState(Vec2 Position, int Direction, bool Moving);

    private void InitializePresentation(string[] arguments)
    {
        _classicVisuals = arguments.Contains("--classic-visuals");
        _omniscientDebug = arguments.Contains("--omniscient-debug");
        var viewArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--view=", StringComparison.Ordinal));
        if (viewArgument is not null)
        {
            _zoom = viewArgument["--view=".Length..] switch
            {
                "far" => 0.78f,
                "near" => 1.25f,
                _ => 1f,
            };
            CenterView();
        }
        if (_classicVisuals)
        {
            return;
        }

        _visualCatalog = VisualAssetCatalog.Load();
        _worldCatalog = VisualAssetCatalog.Load("topdown");
        _entityVisual = new EntityVisual(_visualCatalog);
        _coreVisual = new CoreVisual(_visualCatalog);
        UpdateFacing();
    }

    private void UpdateFacing()
    {
        RefreshVisibleEntities();

        var alive = new HashSet<long>();
        foreach (var entity in _visibleEntities)
        {
            alive.Add(entity.EntityId);
            var previous = _facing.GetValueOrDefault(entity.EntityId);
            var moving = previous != default &&
                         (previous.Position.X != entity.Position.X ||
                          previous.Position.Y != entity.Position.Y);
            var direction = previous != default
                ? DirectionFor(previous.Position, entity.Position, previous.Direction)
                : 0;
            _facing[entity.EntityId] = new FacingState(entity.Position, direction, moving);
        }

        foreach (var stale in _facing.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _facing.Remove(stale);
            _hitUntil.Remove(stale);
        }

        foreach (var expired in _hitUntil
                     .Where(entry => _world.Tick >= entry.Value)
                     .Select(entry => entry.Key)
                     .ToList())
        {
            _hitUntil.Remove(expired);
        }
    }

    private void RefreshVisibleEntities()
    {
        _visibleEntities.Clear();
        _visibleEntityIds.Clear();
        if (_classicVisuals)
        {
            return;
        }

        var own = _world.EnumerateEntities()
            .Where(entity => entity.RegionId == "frontier" &&
                             string.Equals(entity.CivilizationId, "A", StringComparison.Ordinal))
            .Select(VisualEntitySnapshot.From);
        _visibleEntities.AddRange(own);

        if (_omniscientDebug)
        {
            _visibleEntities.AddRange(_world.EnumerateEntities()
                .Where(entity => entity.RegionId == "frontier" &&
                                 !string.Equals(entity.CivilizationId, "A", StringComparison.Ordinal))
                .Select(VisualEntitySnapshot.From));
        }
        else
        {
            var observation = ObservationBuilder.Build(_world, "A");
            _visibleEntities.AddRange(observation.Entities
                .Where(entity => !entity.IsSelf && entity.RegionId == "frontier")
                .Select(VisualEntitySnapshot.FromObserved));
        }

        if (_benchmarkMain)
        {
            AddBenchmarkEntities();
        }

        _visibleEntities.Sort((left, right) => left.Position.Y.CompareTo(right.Position.Y));
        foreach (var entity in _visibleEntities)
        {
            _visibleEntityIds.Add(entity.EntityId);
        }
    }

    private void AddBenchmarkEntities()
    {
        var columns = Mathf.CeilToInt(Mathf.Sqrt(_benchmarkDevices));
        var rows = Mathf.CeilToInt(_benchmarkDevices / (float)columns);
        for (var index = 0; index < _benchmarkDevices; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var position = new Vec2(
                1_200 + column * (13_600 / Mathf.Max(1, columns - 1)),
                900 + row * (6_200 / Mathf.Max(1, rows - 1)));
            var weapon = index % 3 == 0;
            _visibleEntities.Add(new VisualEntitySnapshot(
                EntityId: -(index + 1),
                CivilizationId: "A",
                Position: position,
                Health: 100,
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 100,
                BlueprintId: null,
                BlueprintVersion: 1,
                HasSensor: !weapon,
                SensorRangeMm: weapon ? 0 : 5_000,
                HasWeapon: weapon,
                WeaponRangeMm: weapon ? 3_500 : 0,
                HasDrive: true,
                HasComm: true));
        }
    }

    private bool IsPlayerVisible(SimulationEvent simulationEvent) =>
        _classicVisuals ||
        PlayerEventFilter.IsVisible(simulationEvent, _visibleEntityIds, _omniscientDebug);

    private static int DirectionFor(Vec2 previous, Vec2 current, int fallback)
    {
        var dx = current.X - previous.X;
        var dy = current.Y - previous.Y;
        if (dx == 0 && dy == 0)
        {
            return fallback;
        }

        var angle = Mathf.Atan2(dy, dx);
        return Mathf.PosMod(Mathf.RoundToInt(angle / Mathf.Tau * 8f), 8);
    }

    private int FacingFor(long entityId) =>
        _facing.TryGetValue(entityId, out var facing) ? facing.Direction : 0;

    private bool MovingFor(long entityId) =>
        _facing.TryGetValue(entityId, out var facing) && facing.Moving;

    private bool HitFlashFor(long entityId) =>
        _hitUntil.TryGetValue(entityId, out var until) && _world.Tick < until;

    private void DrawPresentationDebug()
    {
        DebugOverlay.Draw(
            this,
            new Vector2(900, 130),
            _world.EnumerateEntities().Count(),
            _world.Tick);
    }

    private void TrackPresentationEvent(SimulationEvent simulationEvent)
    {
        if (simulationEvent is EntityBootstrapped bootstrapped &&
            string.Equals(bootstrapped.CivilizationId, "A", StringComparison.Ordinal))
        {
            _bootstrapUntilTick = bootstrapped.Tick + 12;
        }

        if (simulationEvent is ShotFired shot)
        {
            _hitUntil[shot.TargetId.Value] = shot.Tick + 6;
        }
        else if (simulationEvent is EntityDestroyed destroyed)
        {
            if (_facing.TryGetValue(destroyed.EntityId.Value, out var last))
            {
                _effects.Add(
                    last.Position,
                    new Color(1f, 0.34f, 0.18f),
                    destroyed.Tick,
                    24,
                    220f,
                    "DESTROYED");
            }

            _hitUntil.Remove(destroyed.EntityId.Value);
        }
        else if (simulationEvent is PowerBlackout blackout &&
                 blackout.CivilizationId == "A")
        {
            _effects.Add(
                Scenarios.AStagingPosition,
                new Color(1f, 0.25f, 0.18f),
                blackout.Tick,
                36,
                1_900f,
                "BLACKOUT");
        }
        else if (simulationEvent is PowerRestored restored &&
                 restored.CivilizationId == "A")
        {
            _effects.Add(
                Scenarios.AStagingPosition,
                new Color(0.35f, 1f, 0.55f),
                restored.Tick,
                30,
                1_700f,
                "GRID RESTORED");
        }
        else if (simulationEvent is ConstructionQueued queued &&
                 queued.CivilizationId == "A")
        {
            _effects.Add(
                queued.Position,
                new Color(0.40f, 0.85f, 1f),
                queued.Tick,
                40,
                380f,
                "BUILD");
        }
        else if (simulationEvent is EntityDeployed deployed &&
                 deployed.CivilizationId == "A")
        {
            _effects.Add(
                deployed.Position,
                new Color(0.55f, 1f, 0.65f),
                deployed.Tick,
                22,
                300f,
                "DEPLOY");
        }
        else if (simulationEvent is EntityUpgraded upgraded &&
                 upgraded.CivilizationId == "A" &&
                 _facing.TryGetValue(upgraded.EntityId.Value, out var upgradedLast))
        {
            _effects.Add(
                upgradedLast.Position,
                new Color(0.45f, 0.90f, 1f),
                upgraded.Tick,
                22,
                280f,
                $"v{upgraded.NewVersion}");
        }
    }

    private void DrawPresentationEffects()
    {
        _effects.Prune(_world.Tick);
        _effects.Draw(this, Map, _world.Tick, _zoom);
    }

    private CoreVisualState CurrentCoreState()
    {
        var snapshot = CoreRecoveryDiagnosticsBuilder.Build(_world, "A");
        return CoreVisualStateRules.Classify(
            snapshot,
            _world.Tick,
            _bootstrapUntilTick);
    }

    private void RunModuleSwapSmoke()
    {
        ResetScenario(peaceful: true, preRun: false);
        var scout = _world.EnumerateEntities().FirstOrDefault(entity =>
            entity.CivilizationId == "A" &&
            string.Equals(entity.BlueprintId, "scout", StringComparison.Ordinal));
        if (scout is null)
        {
            GD.Print("MODULE_SWAP_SMOKE missing scout ok=False");
            GetTree().Quit(1);
            return;
        }

        var before = VisualEntitySnapshot.From(scout).VisualAssetName;
        var latest = _world.Workshop.GetVersions("scout").Last();
        var result = _engine.ExecuteDeploymentCommand(
            new UpgradeCommand("A", scout.Id, latest));
        var after = _world.FindEntity(scout.Id) is { } upgraded
            ? VisualEntitySnapshot.From(upgraded).VisualAssetName
            : "<destroyed>";
        var ok = result.Success &&
                 before == "worker_frame_sensor" &&
                 after == "worker_frame_weapon";
        GD.Print(
            $"MODULE_SWAP_SMOKE before={before} after={after} " +
            $"version={latest.Version} ok={ok}");
        GetTree().Quit(ok ? 0 : 1);
    }

    private void RunEventVisibilitySmoke()
    {
        var visible = new HashSet<long> { 1, 2 };
        var hiddenShot = new ShotFired(
            10, new EntityId(3), new EntityId(4), 5, new Vec2(0, 0), new Vec2(1, 1), 2);
        var visibleShot = new ShotFired(
            10, new EntityId(1), new EntityId(2), 5, new Vec2(0, 0), new Vec2(1, 1), 2);
        var hiddenDestroyed = new EntityDestroyed(10, new EntityId(4));
        var hiddenMessage = new MessageQueued(
            10, 1, new EntityId(4), "A", "frontier", "contact", 12, 1, "direct");
        var visibleMessage = new MessageQueued(
            10, 2, new EntityId(1), "A", "frontier", "contact", 12, 1, "direct");
        var hiddenOk = !PlayerEventFilter.IsVisible(hiddenShot, visible, false) &&
                       PlayerEventFilter.IsVisible(visibleShot, visible, false) &&
                       !PlayerEventFilter.IsVisible(hiddenDestroyed, visible, false) &&
                       !visible.Contains(hiddenMessage.FromEntityId.Value) &&
                       PlayerEventFilter.IsVisible(visibleMessage, visible, false) &&
                       PlayerEventFilter.IsVisible(hiddenShot, visible, true);
        GD.Print($"EVENT_VISIBILITY_SMOKE hiddenShot/visibleShot/destroyed/message ok={hiddenOk}");
        GetTree().Quit(hiddenOk ? 0 : 1);
    }

    private void RunPowerVisualSmoke()
    {
        ResetScenario(peaceful: true, preRun: false);
        var before = GridClosed("A");
        var result = _engine.ExecutePowerCommand(new OpenBreakerCommand("A", "frontier"));
        var after = GridClosed("A");
        var powered = _world.Regions["frontier"].GetEnergy("A") > 0 && after;
        var ok = before && result.Success && !after && !powered;
        GD.Print(
            $"POWER_VISUAL_SMOKE before={before} after={after} " +
            $"energy={_world.Regions["frontier"].GetEnergy("A")} powered={powered} " +
            $"ok={ok}");
        GetTree().Quit(ok ? 0 : 1);
    }

    private void RunVisibilitySmoke()
    {
        ResetScenario(peaceful: false, preRun: true);
        RefreshVisibleEntities();
        var hidden = _visibleEntities.All(entity => entity.EntityId != 4);
        _omniscientDebug = true;
        RefreshVisibleEntities();
        var omniscient = _visibleEntities.Any(entity => entity.EntityId == 4);
        var ok = hidden && omniscient;
        GD.Print(
            $"VISIBILITY_SMOKE unseenB4={hidden} omniscientB4={omniscient} ok={ok}");
        GetTree().Quit(ok ? 0 : 1);
    }

    private void RunCoreStateSmoke()
    {
        ResetScenario(peaceful: true, preRun: true);
        var idle = CurrentCoreState();
        var core = _world.Cores["A"];
        var region = _world.Regions[core.RegionId];
        var recovery = _engine.ExecuteRecoveryCommand(new BootstrapCoreCommand(
            "A",
            Scenarios.EmergencyRecovery,
            new Vec2(region.WidthMm / 2, region.HeightMm / 2)));
        if (recovery.Event is not null)
        {
            TrackPresentationEvent(recovery.Event);
        }

        var bootstrap = CurrentCoreState();
        for (var index = 0; index < 13; index++)
        {
            StepOnce();
        }

        var regenerating = CurrentCoreState();
        ResetScenario(peaceful: false, preRun: true);
        var besieged = CurrentCoreState();
        var ok = idle == CoreVisualState.Idle &&
                 bootstrap == CoreVisualState.Bootstrap &&
                 regenerating == CoreVisualState.Regenerating &&
                 besieged == CoreVisualState.Besieged;
        GD.Print(
            $"CORE_STATE_SMOKE idle={idle} bootstrap={bootstrap} " +
            $"regenerating={regenerating} besieged={besieged} ok={ok}");
        GetTree().Quit(ok ? 0 : 1);
    }

    private void ConfigureCoreStateScenario(string state)
    {
        switch (state)
        {
            case "regenerating":
                ResetScenario(peaceful: true, preRun: true);
                RecoverCoreForPresentation();
                for (var index = 0; index < 13; index++)
                {
                    StepOnce();
                }

                break;
            case "bootstrap":
                ResetScenario(peaceful: true, preRun: true);
                RecoverCoreForPresentation();
                break;
            case "besieged":
                ResetScenario(peaceful: false, preRun: true);
                break;
            default:
                ResetScenario(peaceful: true, preRun: true);
                break;
        }

        _capturePath = $"res://screenshots/art-core-{state}.png";
        _paused = true;
        _captureRequested = true;
    }

    private void RecoverCoreForPresentation()
    {
        var core = _world.Cores["A"];
        var region = _world.Regions[core.RegionId];
        var result = _engine.ExecuteRecoveryCommand(new BootstrapCoreCommand(
            "A",
            Scenarios.EmergencyRecovery,
            new Vec2(region.WidthMm / 2, region.HeightMm / 2)));
        if (result.Event is not null)
        {
            TrackPresentationEvent(result.Event);
        }
    }

    private void ConfigureModuleSwapCapture()
    {
        ResetScenario(peaceful: true, preRun: false);
        var scout = _world.EnumerateEntities().FirstOrDefault(entity =>
            entity.CivilizationId == "A" &&
            string.Equals(entity.BlueprintId, "scout", StringComparison.Ordinal));
        if (scout is not null)
        {
            var latest = _world.Workshop.GetVersions("scout").Last();
            _engine.ExecuteDeploymentCommand(new UpgradeCommand("A", scout.Id, latest));
            UpdateFacing();
        }

        _capturePath = "res://screenshots/art-module-swap.png";
        _paused = true;
        _captureRequested = true;
    }
}
