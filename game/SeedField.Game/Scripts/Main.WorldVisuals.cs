using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main
{
    private static readonly (string Name, Vec2 Position, float Scale)[] WorldProps =
    {
        ("ruin_block", new Vec2(2_200, 1_600), 0.38f),
        ("pipe_junction", new Vec2(6_200, 2_000), 0.36f),
        ("wreck_hauler", new Vec2(10_200, 1_400), 0.38f),
        ("ruin_block", new Vec2(13_200, 2_400), 0.34f),
        ("wreck_hauler", new Vec2(2_600, 6_200), 0.32f),
        ("pipe_junction", new Vec2(7_200, 6_300), 0.35f),
        ("ruin_block", new Vec2(11_200, 6_200), 0.38f),
        ("pipe_junction", new Vec2(13_800, 6_800), 0.33f),
    };

    private void DrawWorldProps()
    {
        if (_classicVisuals)
        {
            return;
        }

        foreach (var prop in WorldProps)
        {
            var frame = _worldCatalog.Get(prop.Name);
            if (frame is null)
            {
                continue;
            }

            var size = frame.Footprint * prop.Scale * _zoom;
            var position = Map(prop.Position);
            var rect = new Rect2(
                position - frame.Pivot * size,
                new Vector2(size, size));
            VisualDraw.Ellipse(
                this,
                position + new Vector2(-size * 0.08f, size * 0.04f),
                size * 0.42f,
                size * 0.16f,
                new Color(0f, 0f, 0f, 0.22f));
            DrawTextureRect(
                frame.Diffuse,
                rect,
                false,
                new Color(0.72f, 0.76f, 0.80f, 0.88f));
        }
    }

    private void DrawWorldBackdrop()
    {
        WorldBackdrop.Draw(
            this,
            Map,
            MapScale,
            _world.ControlPoints.Values
                .Select(point => (point.Position, point.RadiusMm, point.OwnerCivilizationId)),
            _worldCatalog.Get("terrain_concrete")?.Diffuse,
            _worldCatalog.Get("terrain_road")?.Diffuse);
    }

    private bool GridClosed(string civilizationId) =>
        !_world.PowerGridStates.TryGetValue(
            ("frontier", civilizationId),
            out var grid) ||
        !grid.BreakerOpen;

    private void DrawPresentationNetworks()
    {
        var frontier = _world.Regions["frontier"];
        NetworkVisual.Draw(
            this,
            Map,
            _world.ControlPoints.Values
                .Select(point => (point.Position, point.OwnerCivilizationId)),
            frontier.GetEnergy("A") > 0 && GridClosed("A"),
            frontier.GetEnergy("B") > 0 && GridClosed("B"),
            _world.Tick,
            _tacticalVisible);
    }

    private void DrawPresentationEntities()
    {
        var snapshots = _visibleEntities;

        if (_tacticalVisible)
        {
            TacticalOverlay.DrawEntityRanges(this, snapshots, Map, MapScale);
            TacticalOverlay.DrawControlPointRadii(
                this,
                _world.ControlPoints.Values.Select(point => (
                    point.Position,
                    point.RadiusMm)),
                Map,
                MapScale);
        }

        var crowdLod = snapshots.Count >= 400 || _zoom < 0.9f;
        var viewport = new Rect2(
            Vector2.Zero,
            new Vector2(ViewWidth, ViewHeight)).Grow(64f);
        if (crowdLod && !_debugVisible && !_tacticalVisible)
        {
            _lodBatch.Draw(this, snapshots, Map, FacingFor, viewport);
            return;
        }

        foreach (var entity in snapshots)
        {
            var position = Map(entity.Position);
            if (!viewport.HasPoint(position))
            {
                continue;
            }

            _entityVisual.Draw(
                this,
                entity,
                position,
                FacingFor(entity.EntityId),
                _debugVisible,
                _debugVisible,
                _world.Tick,
                MovingFor(entity.EntityId),
                HitFlashFor(entity.EntityId),
                _zoom,
                DetailTier != "far");
        }

        if (_tacticalVisible)
        {
            TacticalOverlay.DrawIds(this, snapshots, Map);
        }
    }

    private void DrawCaptureSupportLabels()
    {
        foreach (var entity in _visibleEntities)
        {
            var label = entity.BlueprintId switch
            {
                "repair_bay" => "REPAIR",
                "shield_generator" => "SHIELD",
                _ => null,
            };
            if (label is null)
            {
                continue;
            }

            var position = Map(entity.Position) + new Vector2(-30, -56);
            var color = entity.BlueprintId == "repair_bay" ? Amber : Cyan;
            DrawRect(
                new Rect2(position - new Vector2(7, 13), new Vector2(74, 19)),
                new Color(0.02f, 0.03f, 0.05f, 0.82f));
            DrawRect(
                new Rect2(position - new Vector2(7, 13), new Vector2(74, 19)),
                new Color(color, 0.35f),
                false,
                1f);
            DrawString(
                ThemeDB.FallbackFont,
                position,
                label,
                HorizontalAlignment.Left,
                -1,
                11,
                color);
        }
    }

    private void DrawPresentationCore()
    {
        var snapshot = CoreRecoveryDiagnosticsBuilder.Build(_world, "A");
        var state = ClassifyCoreState(snapshot);
        if (state == CoreVisualState.Absent)
        {
            return;
        }

        // The core lives in its own region (core-a). Show it in a separate
        // region panel and connect it to the frontier staging exit; never place
        // the core body itself on frontier coordinates.
        var origin = new Vector2(24, 214);
        DrawRect(new Rect2(origin, new Vector2(156, 230)), new Color(0.03f, 0.06f, 0.10f, 0.94f));
        DrawRect(new Rect2(origin, new Vector2(156, 230)), new Color(0.55f, 0.42f, 0.20f, 0.85f), false, 1f);
        DrawString(
            ThemeDB.FallbackFont,
            origin + new Vector2(12, 20),
            "CORE-A REGION",
            HorizontalAlignment.Left,
            -1,
            12,
            new Color(0.95f, 0.78f, 0.40f));
        DrawString(
            ThemeDB.FallbackFont,
            origin + new Vector2(12, 36),
            "separate from frontier",
            HorizontalAlignment.Left,
            -1,
            9,
            new Color(0.55f, 0.66f, 0.74f));

        var corePosition = origin + new Vector2(78, 136);
        var exit = Map(Scenarios.AStagingPosition);
        NetworkVisual.DrawCoreExit(
            this,
            origin + new Vector2(150, 136),
            exit,
            snapshot.EnergyReserve > 0,
            _world.Tick);
        DrawString(
            ThemeDB.FallbackFont,
            origin + new Vector2(154, 130),
            "EXIT",
            HorizontalAlignment.Left,
            -1,
            9,
            new Color(0.95f, 0.78f, 0.40f, 0.80f));
        _coreVisual.Draw(
            this,
            corePosition,
            state,
            _world.Tick,
            _debugVisible,
            1f);
    }
}
