using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main
{
    private readonly Dictionary<long, FacingState> _facing = new();
    private VisualAssetCatalog _visualCatalog = null!;
    private EntityVisual _entityVisual = null!;
    private CoreVisual _coreVisual = null!;
    private long _bootstrapUntilTick;
    private bool _tacticalVisible;
    private bool _debugVisible;
    private bool _classicVisuals;

    private readonly record struct FacingState(Vec2 Position, int Direction);

    private void InitializePresentation(string[] arguments)
    {
        _classicVisuals = arguments.Contains("--classic-visuals");
        if (_classicVisuals)
        {
            return;
        }

        _visualCatalog = VisualAssetCatalog.Load();
        _entityVisual = new EntityVisual(_visualCatalog);
        _coreVisual = new CoreVisual(_visualCatalog);
    }

    private void UpdateFacing()
    {
        if (_classicVisuals)
        {
            return;
        }

        var alive = new HashSet<long>();
        foreach (var entity in _world.EnumerateEntities()
                     .Where(entity => entity.RegionId == "frontier"))
        {
            alive.Add(entity.Id.Value);
            var direction = _facing.TryGetValue(entity.Id.Value, out var previous)
                ? DirectionFor(previous.Position, entity.Position, previous.Direction)
                : 0;
            _facing[entity.Id.Value] = new FacingState(entity.Position, direction);
        }

        foreach (var stale in _facing.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _facing.Remove(stale);
        }
    }

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

    private void DrawPresentationEntities()
    {
        var snapshots = _world.EnumerateEntities()
            .Where(entity => entity.RegionId == "frontier")
            .Select(VisualEntitySnapshot.From)
            .OrderBy(entity => entity.Position.Y)
            .ToList();

        if (_tacticalVisible)
        {
            TacticalOverlay.DrawEntityRanges(this, snapshots, Map, MapScale);
            TacticalOverlay.DrawControlPointRadii(
                this,
                _world.ControlPoints.Values.Select(point => (
                    point.Position,
                    point.RadiusMm,
                    point.Id.Replace("relay-", string.Empty).ToUpperInvariant())),
                Map,
                MapScale);
        }

        foreach (var entity in snapshots)
        {
            _entityVisual.Draw(
                this,
                entity,
                Map(entity.Position),
                FacingFor(entity.EntityId),
                _debugVisible,
                _debugVisible);
        }

        if (_tacticalVisible)
        {
            TacticalOverlay.DrawIds(this, snapshots, Map);
        }
    }

    private void DrawPresentationCore()
    {
        var snapshot = CoreRecoveryDiagnosticsBuilder.Build(_world, "A");
        var state = CoreVisualStateRules.Classify(
            snapshot,
            _world.Tick,
            _bootstrapUntilTick);
        _coreVisual.Draw(
            this,
            Map(new Vec2(900, 2_000)),
            state,
            _world.Tick,
            _debugVisible);
    }

    private void DrawPresentationDebug()
    {
        DebugOverlay.Draw(
            this,
            new Vector2(24, 130),
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
                 after == "worker_frame_drill";
        GD.Print(
            $"MODULE_SWAP_SMOKE before={before} after={after} " +
            $"version={latest.Version} ok={ok}");
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
        _ = _world.EnumerateEntities().First(entity =>
            entity.CivilizationId == "A" &&
            string.Equals(entity.BlueprintId, "scout", StringComparison.Ordinal));
        _capturePath = "res://screenshots/art-module-swap.png";
        _paused = true;
        _captureRequested = true;
    }
}
