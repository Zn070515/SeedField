using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunRecoveryQuotaDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("wilds", 10_000, 10_000)
            .AddCore(
                "A",
                "core",
                new SeedCoreConfig(
                    EnergyPerTick: 100,
                    MaterialPerTick: 100,
                    MaxEnergyReserve: 100,
                    MaxMaterialReserve: 100,
                    RecoveryWindowTicks: 10,
                    RecoveryQuotaPerWindow: 2,
                    RecoveryCostGrowthPercent: 0,
                    RecoveryStreakDecayTicks: 0,
                    RecoveryOverdraftCostPercent: 200))
            .Build();
        var engine = new SimulationEngine(world);
        var blueprint = new RecoveryBlueprint("minimum", 10, 10, 10, 0, 10, 0);

        engine.Step(Array.Empty<Intent>());
        var first = engine.ExecuteRecoveryCommand(
            new BootstrapCoreCommand("A", blueprint, new Vec2(500, 500)));
        var second = engine.ExecuteRecoveryCommand(
            new BootstrapCoreCommand("A", blueprint, new Vec2(600, 500)));
        var overdraft = engine.ExecuteRecoveryCommand(
            new BootstrapCoreCommand("A", blueprint, new Vec2(700, 500)));
        var overdraftEvent = overdraft.Event as EntityBootstrapped;
        var slowMode = overdraft.Success && overdraftEvent?.EnergySpent == 30;

        for (var tick = 0; tick < 10; tick++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var afterWindow = engine.ExecuteRecoveryCommand(
            new BootstrapCoreCommand("A", blueprint, new Vec2(800, 500)));
        var afterWindowEvent = afterWindow.Event as EntityBootstrapped;
        var core = world.Cores["A"];

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var hashOk = WorldStateHasher.Compute(world) == WorldStateHasher.Compute(loaded);

        var ok = first.Success &&
                 second.Success &&
                 slowMode &&
                 afterWindow.Success &&
                 afterWindowEvent?.EnergySpent == 10 &&
                 core.RecoveriesInWindow == 1 &&
                 hashOk;
        Console.WriteLine(
            $"RECOVERY_QUOTA first={first.Success} second={second.Success} " +
            $"slowMode={slowMode} afterWindow={afterWindow.Success} " +
            $"windowStart={core.RecoveryWindowStartTick} used={core.RecoveriesInWindow} " +
            $"streak={core.RecoveryStreak} hashOk={hashOk} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunCoreSiegeDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("shell", 10_000, 10_000)
            .AddLink("l0", "core", "shell", latencyTicks: 1)
            .AddCore(
                "A",
                "core",
                new SeedCoreConfig(
                    EnergyPerTick: 300,
                    MaterialPerTick: 300,
                    MaxEnergyReserve: 300,
                    MaxMaterialReserve: 300,
                    RecoveryWindowTicks: 100,
                    RecoveryQuotaPerWindow: 3,
                    AnchorRecoveryCostPercent: 50,
                    SiegeRecoveryCostPercent: 100,
                    SiegeQuotaPenalty: 2))
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "shell",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                IsCoreAnchor: true))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "shell",
                new Vec2(500, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                Sensor: new SensorModule(1_000),
                Weapon: new WeaponModule(1_000, Damage: 10, EnergyCost: 0, CooldownTicks: 0)))
            .Build();
        var engine = new SimulationEngine(world);
        var blueprint = new RecoveryBlueprint("minimum", 40, 30, 10, 0, 10, 0);
        engine.Step(Array.Empty<Intent>());

        var first = engine.ExecuteRecoveryCommand(
            new BootstrapCoreCommand("A", blueprint, new Vec2(500, 500)));
        var firstEvent = first.Event as EntityBootstrapped;
        var siegeCost = firstEvent?.EnergySpent == 80;

        var denied = engine.ExecuteRecoveryCommand(
            new BootstrapCoreCommand("A", blueprint, new Vec2(600, 500)));
        var quotaDenied = denied.ErrorCode == SimulationErrorCode.RecoveryQuotaExhausted;

        engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(2), new EntityId(1)),
        });
        var anchorLost = !world.EnumerateEntities().Any(entity => entity.IsCoreAnchor);
        world.Regions["shell"].Entities.Remove(new EntityId(2));

        var anchor = world.Workshop.Publish(new DeviceBlueprint(
            "anchor",
            0,
            "Shell Anchor",
            10,
            10,
            IsCoreAnchor: true));
        engine.Step(Array.Empty<Intent>());
        var withGap = engine.ExecuteRecoveryCommand(
            new BootstrapCoreCommand("A", blueprint, new Vec2(700, 500)));
        var withGapEvent = withGap.Event as EntityBootstrapped;
        var gapCost = withGapEvent?.EnergySpent == 60;

        var deployed = engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            anchor,
            "shell",
            new Vec2(900, 900)));
        engine.Step(Array.Empty<Intent>());
        var rebuilt = engine.ExecuteRecoveryCommand(
            new BootstrapCoreCommand("A", blueprint, new Vec2(800, 500)));
        var rebuiltEvent = rebuilt.Event as EntityBootstrapped;
        var rebuiltCost = rebuiltEvent?.EnergySpent == 40;

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var hashOk = WorldStateHasher.Compute(world) == WorldStateHasher.Compute(loaded);
        var targetOk = world.Cores["A"].AnchorTarget == 1;

        var ok = first.Success &&
                 siegeCost &&
                 quotaDenied &&
                 anchorLost &&
                 withGap.Success &&
                 gapCost &&
                 deployed.Success &&
                 rebuilt.Success &&
                 rebuiltCost &&
                 targetOk &&
                 hashOk;
        Console.WriteLine(
            $"CORE_SIEGE siegeCost={siegeCost} quotaDenied={quotaDenied} " +
            $"anchorLost={anchorLost} gapCost={gapCost} " +
            $"rebuiltCost={rebuiltCost} target={world.Cores["A"].AnchorTarget} " +
            $"hashOk={hashOk} ok={ok}");

        return ok ? 0 : 1;
    }
}
