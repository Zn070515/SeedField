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
}
