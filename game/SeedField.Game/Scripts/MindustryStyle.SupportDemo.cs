using System.IO;
using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class MindustryStyle
{
    private bool _supportDemo;
    private WorldState? _supportWorld;
    private SimulationEngine? _supportEngine;
    private EntityId _supportA1;
    private EntityId _supportA2;
    private EntityId _supportB;
    private readonly Dictionary<long, string> _supportLabels = new();
    private readonly List<SimulationEvent> _supportEvents = new();
    private double _supportAccumulator;
    private int _supportTicks;
    private int _supportCaptureFrames;

    private bool StartSupportDemo()
    {
        if (!OS.GetCmdlineUserArgs().Contains("--support-demo"))
        {
            return false;
        }

        var world = new WorldBuilder()
            .AddRegion("demo", 2_000, 1_200)
            .AddStock("demo", "A", 500)
            .AddStock("demo", "B", 500)
            .Build();

        var repairBlueprint = world.Workshop.Publish(new DeviceBlueprint(
            "repair_bay_demo",
            0,
            "Repair Bay",
            MaxHealth: 160,
            EnergyCapacity: 1_000,
            Power: new PowerModule(EnergyPerTick: 2, StorageCapacity: 1_000),
            Repair: new RepairModule(1_500, 5, 1)));
        var shieldBlueprint = world.Workshop.Publish(new DeviceBlueprint(
            "shield_demo",
            0,
            "Shield Generator",
            MaxHealth: 140,
            EnergyCapacity: 1_000,
            Power: new PowerModule(EnergyPerTick: 2, StorageCapacity: 1_000),
            Shield: new ShieldModule(60, 3, 4, 1),
            ShieldProjector: new ShieldProjectorModule(1_800, 5, 1)));
        var unitBlueprint = world.Workshop.Publish(new DeviceBlueprint(
            "unit_demo",
            0,
            "Shielded Frame",
            MaxHealth: 100,
            EnergyCapacity: 0,
            Sensor: new SensorModule(100),
            Shield: new ShieldModule(50, 0, 100_000, 0)));
        var attackerBlueprint = world.Workshop.Publish(new DeviceBlueprint(
            "attacker_demo",
            0,
            "Raider",
            MaxHealth: 100,
            EnergyCapacity: 0,
            Sensor: new SensorModule(5_000),
            Weapon: new WeaponModule(5_000, Damage: 20, EnergyCost: 0, CooldownTicks: 0)));

        var repair = DeploymentService
            .Deploy(world, repairBlueprint, "A", "demo", new Vec2(400, 600));
        var shield = DeploymentService
            .Deploy(world, shieldBlueprint, "A", "demo", new Vec2(800, 600));
        var first = DeploymentService
            .Deploy(world, unitBlueprint, "A", "demo", new Vec2(700, 450));
        var second = DeploymentService
            .Deploy(world, unitBlueprint, "A", "demo", new Vec2(900, 750));
        var raider = DeploymentService
            .Deploy(world, attackerBlueprint, "B", "demo", new Vec2(1_700, 600));

        _supportA1 = first.Id;
        _supportA2 = second.Id;
        _supportB = raider.Id;
        _supportLabels[repair.Id.Value] = "REPAIR";
        _supportLabels[shield.Id.Value] = "SHIELD";
        _supportLabels[first.Id.Value] = "A1";
        _supportLabels[second.Id.Value] = "A2";
        _supportLabels[raider.Id.Value] = "B";

        _supportWorld = world;
        _supportEngine = new SimulationEngine(world);
        _supportDemo = true;
        _capture = true;
        return true;
    }

    private void StepSupportDemo(double delta)
    {
        if (_supportWorld is null || _supportEngine is null)
        {
            return;
        }

        if (_supportTicks >= 24)
        {
            _supportCaptureFrames++;
            if (_supportCaptureFrames < 3)
            {
                QueueRedraw();
                return;
            }

            CaptureSupportDemo();
            return;
        }

        _supportAccumulator += delta;
        if (_supportAccumulator < 0.25)
        {
            return;
        }

        _supportAccumulator = 0;
        var target = _supportTicks % 2 == 0 ? _supportA1 : _supportA2;
        var result = _supportEngine.Step(
            new Intent[] { new AttackIntent(_supportB, target) });
        _supportTicks++;
        _supportEvents.AddRange(result.Events);
        if (_supportEvents.Count > 8)
        {
            _supportEvents.RemoveRange(0, _supportEvents.Count - 8);
        }

        QueueRedraw();
    }

    private void DrawSupportDemo()
    {
        DrawRect(new Rect2(0, 0, 1280, 720), new Color("100e0b"));
        DrawSupportTerrain();
        DrawSupportProps();
        DrawSupportRange(new Vec2(400, 600), 1_500, new Color("f2a33a"));
        DrawSupportRange(new Vec2(800, 600), 1_800, new Color("7fe0cf"));
        DrawSupportBeams();

        if (_supportWorld is not null)
        {
            foreach (var entity in _supportWorld.EnumerateEntities())
            {
                DrawSupportEntity(entity);
            }
        }

        DrawSupportHud();
        DrawEventLog();
    }

    private void DrawSupportTerrain()
    {
        for (var y = -64; y < 720; y += 64)
        {
            for (var x = -64; x < 1280; x += 64)
            {
                var tileX = x / 64;
                var tileY = y / 64;
                var asset = ((tileX * 7) + (tileY * 5)) % 13 == 0
                    ? "terrain_rust"
                    : ((tileX * 3) + (tileY * 11)) % 17 == 0
                        ? "terrain_sand"
                        : "terrain_dirt";
                var terrain = Tex(asset);
                if (terrain is not null)
                {
                    DrawTextureRect(terrain, new Rect2(x, y, 64, 64), false);
                }
            }
        }

        for (var x = 0; x <= 1280; x += 64)
        {
            DrawLine(
                new Vector2(x, 0),
                new Vector2(x, 720),
                new Color(0, 0, 0, 0.10f),
                1f);
        }

        for (var y = 0; y <= 720; y += 64)
        {
            DrawLine(
                new Vector2(0, y),
                new Vector2(1280, y),
                new Color(0, 0, 0, 0.10f),
                1f);
        }
    }

    private void DrawSupportProps()
    {
        var props = new (string Asset, float X, float Y, float Size)[]
        {
            ("prop_scrap", 205, 292, 52),
            ("prop_barrels", 292, 468, 48),
            ("prop_sandbags", 690, 536, 56),
            ("ruin_block", 1_006, 246, 70),
            ("pipe_junction", 438, 248, 48),
            ("prop_scrap", 1_082, 522, 46),
            ("ore_copper", 1_078, 214, 52),
            ("ore_lead", 962, 566, 44),
        };
        foreach (var (asset, x, y, size) in props)
        {
            DrawWorldSprite(asset, new Vector2(x, y), size);
        }
    }

    private void DrawSupportRange(Vec2 center, int rangeMm, Color color)
    {
        var screen = ToScreen(center);
        var radius = rangeMm * 0.62f;
        DrawCircle(screen, radius, new Color(color, 0.030f));
        for (var index = 0; index < 48; index += 2)
        {
            DrawArc(
                screen,
                radius,
                Mathf.Tau * index / 48f,
                Mathf.Tau * (index + 1) / 48f,
                4,
                new Color(color, 0.34f),
                1.5f,
                true);
        }
    }

    private void DrawSupportBeams()
    {
        if (_supportWorld is null)
        {
            return;
        }

        foreach (var simulationEvent in _supportEvents)
        {
            if (simulationEvent.Tick < _supportTicks - 1)
            {
                continue;
            }

            switch (simulationEvent)
            {
                case EntityRepaired repaired:
                    DrawBeam(repaired.RepairerId, repaired.TargetId, new Color("f2a33a"));
                    break;
                case ShieldProjected projected:
                    DrawBeam(projected.ProjectorId, projected.TargetId, new Color("7fe0cf"));
                    break;
            }
        }
    }

    private void DrawBeam(EntityId fromId, EntityId toId, Color color)
    {
        var from = _supportWorld?.FindEntity(fromId);
        var to = _supportWorld?.FindEntity(toId);
        if (from is null || to is null)
        {
            return;
        }

        var start = ToScreen(from.Position);
        var end = ToScreen(to.Position);
        DrawLine(start, end, new Color(color, 0.62f), 2f, true);
        DrawCircle(end, 7, new Color(color, 0.22f));
    }

    private void DrawSupportEntity(EntityState entity)
    {
        var (asset, size) = entity.BlueprintId switch
        {
            "repair_bay_demo" => ("waste_repair_bay", 190f),
            "shield_demo" => ("waste_shield_generator", 190f),
            "attacker_demo" => ("waste_sentry", 80f),
            _ => ("waste_scout", 80f),
        };
        var center = ToScreen(entity.Position);
        DrawWorldSprite(asset, center, size);

        var barWidth = size * 0.55f;
        var barX = center.X - (barWidth * 0.5f);
        var barY = center.Y - (size * 0.5f) - 16;
        DrawRect(new Rect2(barX - 1, barY - 1, barWidth + 2, 7), new Color("05070a"));
        DrawRect(
            new Rect2(
                barX,
                barY,
                barWidth * Mathf.Clamp(entity.Health / (float)entity.MaxHealth, 0, 1),
                5),
            new Color("b5543f"));
        if (entity.Shield is not null)
        {
            DrawRect(new Rect2(barX - 1, barY - 10, barWidth + 2, 6), new Color("05070a"));
            DrawRect(
                new Rect2(
                    barX,
                    barY - 9,
                    barWidth * Mathf.Clamp(
                        entity.ShieldEnergy / (float)entity.Shield.Capacity,
                        0,
                        1),
                    4),
                new Color("7fe0cf"));
        }

        DrawLabelPill(
            new Vector2(center.X, center.Y + (size * 0.5f) + 8),
            Label(entity.Id),
            entity.CivilizationId == "A" ? new Color("7fe0cf") : new Color("f2a33a"));
    }

    private void DrawWorldSprite(string asset, Vector2 center, float size)
    {
        var texture = Tex(asset);
        if (texture is null)
        {
            return;
        }

        var rect = new Rect2(
            center - new Vector2(size * 0.5f, size * 0.5f),
            new Vector2(size, size));
        DrawTextureRect(
            texture,
            new Rect2(rect.Position + new Vector2(4, 6), rect.Size),
            false,
            new Color(0, 0, 0, 0.35f));
        DrawTextureRect(texture, rect, false);
    }

    private void DrawLabelPill(Vector2 center, string text, Color color)
    {
        var width = (text.Length * 6.5f) + 16;
        var rect = new Rect2(center.X - (width * 0.5f), center.Y, width, 17);
        DrawRect(rect, new Color(0.02f, 0.03f, 0.05f, 0.78f));
        DrawRect(rect, new Color(color, 0.35f), false, 1f);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(rect.Position.X + 8, rect.Position.Y + 13),
            text,
            HorizontalAlignment.Left,
            -1,
            10,
            color);
    }

    private void DrawSupportHud()
    {
        DrawRect(new Rect2(0, 0, 1280, 56), new Color("0b0f12"));
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(24, 22),
            "SEED FIELD · SUPPORT BUILDINGS · LIVE SIMULATION",
            HorizontalAlignment.Left,
            -1,
            18,
            new Color("d7e6f5"));
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(24, 43),
            $"TICK {_supportTicks:00}  ·  REPAIR 5 HP/t  ·  SHIELD +5/target  ·  B RAIDER DMG 20",
            HorizontalAlignment.Left,
            -1,
            11,
            new Color("9fb3c8"));
        DrawRect(new Rect2(1_030, 15, 10, 10), new Color("7fe0cf"));
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(1_046, 24),
            "A SUPPORT",
            HorizontalAlignment.Left,
            -1,
            10,
            new Color("7fe0cf"));
        DrawRect(new Rect2(1_146, 15, 10, 10), new Color("f2a33a"));
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(1_162, 24),
            "B RAIDER",
            HorizontalAlignment.Left,
            -1,
            10,
            new Color("f2a33a"));
    }

    private void DrawEventLog()
    {
        DrawRect(new Rect2(18, 548, 560, 160), new Color(0.02f, 0.03f, 0.05f, 0.78f));
        DrawRect(new Rect2(18, 548, 560, 160), new Color("2f4a63"), false, 1f);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(30, 568),
            "SUPPORT EVENTS",
            HorizontalAlignment.Left,
            -1,
            11,
            new Color("9fb3c8"));
        var y = 586;
        foreach (var simulationEvent in _supportEvents)
        {
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(30, y),
                DescribeSupportEvent(simulationEvent),
                HorizontalAlignment.Left,
                -1,
                10,
                new Color("d7e6f5"));
            y += 14;
        }
    }

    private string DescribeSupportEvent(SimulationEvent simulationEvent) =>
        simulationEvent switch
        {
            ShotFired shot =>
                $"T{shot.Tick,2}  {Label(shot.AttackerId)} → {Label(shot.TargetId)} " +
                $"HIT -{shot.Damage}",
            EntityDamaged damaged =>
                $"T{damaged.Tick,2}  HIT {Label(damaged.EntityId)} -{damaged.Damage} HP " +
                $"(shield -{damaged.ShieldAbsorbed})",
            EntityRepaired repaired =>
                $"T{repaired.Tick,2}  {Label(repaired.RepairerId)} → " +
                $"{Label(repaired.TargetId)} +{repaired.Amount} HP",
            ShieldProjected projected =>
                $"T{projected.Tick,2}  {Label(projected.ProjectorId)} → " +
                $"{Label(projected.TargetId)} +{projected.Amount} SH",
            _ => $"T{simulationEvent.Tick,2}  {simulationEvent.GetType().Name}",
        };

    private string Label(EntityId id) =>
        _supportLabels.TryGetValue(id.Value, out var label) ? label : id.ToString();

    private static Vector2 ToScreen(Vec2 position) =>
        new(80 + (position.X * 0.62f), 30 + (position.Y * 0.62f));

    private void CaptureSupportDemo()
    {
        var path = ProjectSettings.GlobalizePath("res://screenshots/support-demo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(path);
        GD.Print(error == Error.Ok
            ? $"SUPPORT_DEMO_SCREENSHOT_SAVED {path}"
            : $"SUPPORT_DEMO_SCREENSHOT_FAILED {error}: {path}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}
