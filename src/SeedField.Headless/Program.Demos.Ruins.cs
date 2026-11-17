using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunRuinDemo()
    {
        var source = new WorldBuilder()
            .AddRegion("home", 1, 1)
            .AddStock("home", "offline-A", 100)
            .Build();
        var blueprint = source.Workshop.Publish(new DeviceBlueprint(
            "device",
            0,
            "Device",
            MaxHealth: 10,
            EnergyCapacity: 10,
            MaterialCost: 100));
        new SimulationEngine(source).ExecuteDeploymentCommand(new DeployCommand(
            "offline-A",
            blueprint,
            "home",
            new Vec2(0, 0)));
        var snapshot = RuinSnapshotSerializer.Capture(source, "offline-A", "home");
        var json = RuinSnapshotSerializer.Save(snapshot);

        var target = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .Build();
        var engine = new SimulationEngine(target);
        var result = engine.ExecuteRuinCommand(new ImportRuinCommand(
            "A",
            "r0",
            json,
            CenterX: 500,
            CenterY: 500,
            RadiusMm: 1_000,
            AgingPercent: 20,
            LootPercent: 50));

        var imported = result.Events.OfType<RuinImported>().SingleOrDefault();
        var wreck = target.Wrecks.Values.SingleOrDefault();
        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(target));
        var hashOk = WorldStateHasher.Compute(target) == WorldStateHasher.Compute(loaded);
        var ok = result.Success &&
                 imported is { EntriesInSlice: 1, WrecksCreated: 1, MaterialTotal: 40 } &&
                 wreck is { MaterialRecoverable: 40, OwnerCivilizationId: "offline-A" } &&
                 hashOk;
        Console.WriteLine(
            $"RUINS captured={snapshot.Entries.Length} imported={imported?.WrecksCreated} " +
            $"material={wreck?.MaterialRecoverable} owner={wreck?.OwnerCivilizationId} " +
            $"hashOk={hashOk} ok={ok}");

        return ok ? 0 : 1;
    }
}
