using Godot;
using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main : Node2D
{
    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, ViewWidth, ViewHeight), Background);
        if (!_classicVisuals)
        {
            DrawWorldBackdrop();
        }

        DrawGrid();
        if (!_classicVisuals)
        {
            DrawWorldProps();
        }

        DrawStagingZones();
        if (!_classicVisuals)
        {
            DrawPresentationNetworks();
        }

        DrawControlPoint();
        DrawDeposits();
        if (_classicVisuals)
        {
            DrawRanges();
            DrawTracers();
            DrawMessages();
            DrawEntities();
        }
        else
        {
            DrawPresentationCore();
            DrawPresentationEntities();
            DrawTracers();
            DrawMessages();
            DrawPresentationEffects();
            if (_debugVisible)
            {
                DrawPresentationDebug();
            }
        }

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

        if (_zoom <= 1.1f)
        {
            DrawString(
                HudFont,
                MapOrigin + new Vector2(0, -10),
                "FRONTIER  ·  16 000 × 8 000 mm  ·  authoritative C# SimulationCore",
                HorizontalAlignment.Left,
                -1,
                12,
                Muted);
        }
    }

    private void DrawStagingZones()
    {
        DrawArc(Map(Scenarios.AStagingPosition), 1_500 * MapScale, 0, Mathf.Tau, 72, new Color(Cyan, 0.22f), 2f, true);
        DrawArc(Map(Scenarios.BStagingPosition), 1_500 * MapScale, 0, Mathf.Tau, 72, new Color(Orange, 0.22f), 2f, true);

        DrawString(
            HudFont,
            Map(Scenarios.AStagingPosition) + new Vector2(-62, 74),
            "A · STAGING",
            HorizontalAlignment.Left,
            -1,
            12,
            new Color(Cyan, 0.75f));
        DrawString(
            HudFont,
            Map(Scenarios.BStagingPosition) + new Vector2(-62, 74),
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

            if (_classicVisuals)
            {
                DrawCircle(center, radius, new Color(color, 0.05f));
                DrawArc(center, radius, 0, Mathf.Tau, 80, new Color(color, 0.6f), 2f, true);
            }

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
        foreach (var entity in _world.EnumerateEntities()
                     .Where(entity => entity.RegionId == "frontier"))
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
            if (!IsPlayerVisible(item.Event) ||
                item.Event is not (ShotFired or ShotMissed))
            {
                continue;
            }

            var age = _world.Tick - item.Tick;
            if (age < 0 || age > 8)
            {
                continue;
            }

            var alpha = 1f - (age / 8f);
            var (fromPoint, targetPoint, hit) = item.Event switch
            {
                ShotFired shot => (shot.From, shot.Target, true),
                ShotMissed missed => (missed.From, missed.Target, false),
                _ => default,
            };
            var from = Map(fromPoint);
            var target = Map(targetPoint);

            DrawLine(
                from,
                target,
                hit
                    ? new Color(1f, 0.45f, 0.20f, alpha)
                    : new Color(0.75f, 0.62f, 0.45f, alpha * 0.65f),
                hit ? 2.6f : 1.6f,
                true);
            if (hit)
            {
                DrawCircle(
                    target,
                    4f + (age * 0.9f),
                    new Color(1f, 0.58f, 0.24f, alpha * 0.85f));
            }

            DrawCircle(from, 2.4f, new Color(1f, 0.86f, 0.55f, alpha));
        }
    }

    private void DrawMessages()
    {
        foreach (var item in _history)
        {
            if (item.Event is not MessageQueued queued ||
                !IsPlayerVisible(item.Event) ||
                (!_classicVisuals && !_visibleEntityIds.Contains(queued.FromEntityId.Value)))
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
                entity.RegionId == "frontier" &&
                entity.Id != sender.Id &&
                (_classicVisuals || _visibleEntityIds.Contains(entity.Id.Value)) &&
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
        foreach (var entity in _world.EnumerateEntities()
                     .Where(entity => entity.RegionId == "frontier"))
        {
            var position = Map(entity.Position);
            var color = CivilizationColor(entity.CivilizationId);

            DrawCircle(position, 12f, new Color(color, 0.14f));
            DrawCircle(position, 6.5f, color);
            DrawArc(position, 10.5f, 0, Mathf.Tau, 36, color, 1.4f, true);

            DrawBar(position + new Vector2(-13f, -21f), 26f, 3f, (float)entity.Health / entity.MaxHealth, Green);
            if (entity.Shield is { Capacity: > 0 } shield)
            {
                DrawBar(
                    position + new Vector2(-13f, -25f),
                    26f,
                    2f,
                    (float)entity.ShieldEnergy / shield.Capacity,
                    Cyan);
            }

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
            "Gate 3 world slice — Godot 4.7.2 Mono · .NET 10 · Jint 4.18.0",
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
            $"faults {_faultCount}   A program v{_slotA.Version}   " +
            $"view {_zoom:0.00} ({DetailTier})",
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

        DrawTutorialSummary();
        DrawTutorialPanel();

        if (_tacticalVisible || _debugVisible)
        {
            DrawLegend();
        }

        if (_debugVisible)
        {
            DrawEventLog();
        }

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
            .Where(item => IsLogworthy(item.Event) && IsPlayerVisible(item.Event))
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
            $"OPPONENT {_opponent.Manifest.PackageId} " +
            $"v{_opponent.Manifest.Version} · " +
            "SPACE pause · N step · F1 program · F2 workshop · F3 diplomacy · F4 power · F6 recovery · F5 save · F9 load · F12 screenshot",
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
            or ShotMissed
            or CombatLootGranted
            or OreMined
            or OreSmelted
            or ComponentsFabricated
            or EntityDeployed
            or EntityUpgraded
            or ConstructionQueued
            or ConstructionCancelled
            or ConstructionFailed
            or CoreMaintained
            or RuinImported
            or MaterialArrived
            or EnergyArrived
            or IntentRejected
            or EntityDamaged
            or EntityRepaired
            or EntityDestroyed
            or MessageDelivered
            or MessageQueued
            or CoreRegenerated
            or EntityBootstrapped
            or ControlPointCaptured
            or MatchEnded
            or TreatyProposed
            or TreatyAccepted
            or TreatyRejected
            or TreatyBroken
            or TreatyProposalExpired
            or TradeProposed
            or TradeAccepted
            or TradeRejected
            or TradeCancelled
            or TradeExpired
            or TradeCountered
            or PowerFlowed
            or PowerShipped
            or PowerArrived
            or PowerShortfall
            or EnergyAutoDrawn
            or EnergyWasted
            or PowerBlackout
            or PowerRestored
            or BreakerLockedOut
            or BreakerMaintained
            or RecoveryQuotaDenied
            or WreckCreated
            or WreckOccupied
            or WreckSalvaged;

    private static Color EventColor(SimulationEvent simulationEvent) =>
        simulationEvent switch
        {
            ShotFired => new Color(1f, 0.58f, 0.30f),
            ShotMissed => new Color(0.72f, 0.62f, 0.48f),
            EntityDamaged => new Color(0.95f, 0.55f, 0.45f),
            CombatLootGranted => new Color(0.85f, 0.78f, 0.40f),
            OreMined => new Color(0.70f, 0.78f, 0.45f),
            OreSmelted => new Color(0.85f, 0.70f, 0.40f),
            ComponentsFabricated => new Color(0.55f, 0.78f, 0.85f),
            EntityDeployed or EntityUpgraded => Cyan,
            ConstructionQueued => new Color(0.55f, 0.72f, 0.82f),
            ConstructionCancelled => Muted,
            ConstructionFailed => Red,
            CoreMaintained => Green,
            RuinImported => new Color(0.60f, 0.68f, 0.78f),
            MaterialArrived => new Color(0.70f, 0.78f, 0.45f),
            EnergyArrived => Cyan,
            IntentRejected => new Color(0.85f, 0.55f, 0.42f),
            EntityDestroyed => Red,
            MessageDelivered => Amber,
            MessageQueued => new Color(0.75f, 0.66f, 0.40f),
            TreatyAccepted or TradeAccepted => Green,
            TreatyRejected or TreatyBroken or TradeRejected => Red,
            TreatyProposed or TreatyProposalExpired or TradeProposed or TradeCountered =>
                new Color(0.75f, 0.66f, 0.40f),
            TradeCancelled or TradeExpired => Muted,
            PowerFlowed => Cyan,
            PowerShipped => new Color(0.45f, 0.75f, 1.00f),
            PowerArrived => Green,
            EnergyAutoDrawn => Blue,
            PowerShortfall => Red,
            EnergyWasted => new Color(0.75f, 0.50f, 0.35f),
            PowerBlackout => Red,
            PowerRestored => Green,
            BreakerLockedOut => Red,
            BreakerMaintained => Green,
            RecoveryQuotaDenied => new Color(0.75f, 0.66f, 0.40f),
            WreckCreated => new Color(0.75f, 0.50f, 0.35f),
            WreckOccupied => new Color(0.85f, 0.78f, 0.40f),
            WreckSalvaged => Green,
            _ => Text,
        };

}
