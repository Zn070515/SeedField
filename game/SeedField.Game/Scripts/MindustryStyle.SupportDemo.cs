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

        DeploymentService.Deploy(world, repairBlueprint, "A", "demo", new Vec2(400, 600));
        DeploymentService.Deploy(world, shieldBlueprint, "A", "demo", new Vec2(800, 600));
        _supportA1 = DeploymentService
            .Deploy(world, unitBlueprint, "A", "demo", new Vec2(700, 450)).Id;
        _supportA2 = DeploymentService
            .Deploy(world, unitBlueprint, "A", "demo", new Vec2(900, 750)).Id;
        _supportB = DeploymentService
            .Deploy(world, attackerBlueprint, "B", "demo", new Vec2(1_700, 600)).Id;

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

        var terrain = Tex("terrain_dirt");
        if (terrain is not null)
        {
            for (var y = 0; y < 720; y += 64)
            {
                for (var x = 0; x < 1280; x += 64)
                {
                    DrawTextureRect(terrain, new Rect2(x, y, 64, 64), false);
                }
            }
        }

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

        DrawArc(
            ToScreen(new Vec2(400, 600)),
            1_500 * 0.55f,
            0,
            Mathf.Tau,
            96,
            new Color("f2a33a", 0.16f),
            2f,
            true);
        DrawArc(
            ToScreen(new Vec2(800, 600)),
            1_800 * 0.55f,
            0,
            Mathf.Tau,
            96,
            new Color("7fe0cf", 0.16f),
            2f,
            true);

        if (_supportWorld is not null)
        {
            foreach (var entity in _supportWorld.EnumerateEntities())
            {
                DrawSupportEntity(entity);
            }
        }

        DrawEventLog();
    }

    private void DrawSupportEntity(EntityState entity)
    {
        var (asset, size, label) = entity.BlueprintId switch
        {
            "repair_bay_demo" => ("waste_repair_bay", 170f, "REPAIR"),
            "shield_demo" => ("waste_shield_generator", 170f, "SHIELD"),
            "attacker_demo" => ("waste_sentry", 70f, "B"),
            _ => ("waste_scout", 70f, entity.Id == _supportA1 ? "A1" : "A2"),
        };
        var texture = Tex(asset);
        var center = ToScreen(entity.Position);
        var rect = new Rect2(
            center - new Vector2(size * 0.5f, size * 0.5f),
            new Vector2(size, size));
        if (texture is not null)
        {
            DrawTextureRect(
                texture,
                new Rect2(rect.Position + new Vector2(4, 6), rect.Size),
                false,
                new Color(0, 0, 0, 0.35f));
            DrawTextureRect(texture, rect, false);
        }

        var barWidth = size * 0.55f;
        var barX = center.X - (barWidth * 0.5f);
        var barY = center.Y - (size * 0.5f) - 14;
        DrawRect(new Rect2(barX, barY, barWidth, 5), new Color("1a1a1a"));
        DrawRect(
            new Rect2(barX, barY, barWidth * Mathf.Clamp(
                entity.Health / (float)entity.MaxHealth,
                0,
                1),
                5),
            new Color("b5543f"));
        if (entity.Shield is not null)
        {
            DrawRect(new Rect2(barX, barY - 7, barWidth, 4), new Color("1a1a1a"));
            DrawRect(
                new Rect2(barX, barY - 7, barWidth * Mathf.Clamp(
                    entity.ShieldEnergy / (float)entity.Shield.Capacity,
                    0,
                    1),
                    4),
                new Color("7fe0cf"));
        }

        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(center.X - 26, center.Y + (size * 0.5f) + 14),
            label,
            HorizontalAlignment.Left,
            -1,
            10,
            entity.CivilizationId == "A" ? new Color("7fe0cf") : new Color("f2a33a"));
    }

    private void DrawEventLog()
    {
        var y = 600;
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(24, y - 20),
            "EVENTS",
            HorizontalAlignment.Left,
            -1,
            11,
            new Color("9fb3c8"));
        foreach (var simulationEvent in _supportEvents)
        {
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(24, y),
                DescribeSupportEvent(simulationEvent),
                HorizontalAlignment.Left,
                -1,
                11,
                new Color("d7e6f5"));
            y += 16;
        }
    }

    private static string DescribeSupportEvent(SimulationEvent simulationEvent) =>
        simulationEvent switch
        {
            EntityDamaged damaged =>
                $"T{damaged.Tick,2}  HIT {damaged.EntityId} -{damaged.Damage} HP " +
                $"(shield -{damaged.ShieldAbsorbed})",
            EntityRepaired repaired =>
                $"T{repaired.Tick,2}  REPAIR {repaired.TargetId} +{repaired.Amount} HP",
            ShieldProjected projected =>
                $"T{projected.Tick,2}  SHIELD {projected.TargetId} +{projected.Amount} " +
                $"({projected.TargetShield} total)",
            _ => $"T{simulationEvent.Tick,2}  {simulationEvent.GetType().Name}",
        };

    private static Vector2 ToScreen(Vec2 position) =>
        new(90 + (position.X * 0.55f), 40 + (position.Y * 0.55f));

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
