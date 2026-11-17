using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunWreckDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .SetWreckRecoveryPercent(50)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "B",
                "r0",
                new Vec2(500, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                Sensor: new SensorModule(1_000),
                Weapon: new WeaponModule(1_000, Damage: 10, EnergyCost: 0, CooldownTicks: 0)))
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "device",
            0,
            "Device",
            MaxHealth: 10,
            EnergyCapacity: 10,
            MaterialCost: 100));
        var engine = new SimulationEngine(world);
        engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            blueprint,
            "r0",
            new Vec2(100, 100)));

        var destroyed = engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });
        var wreck = world.Wrecks.Values.SingleOrDefault();
        if (wreck is null)
        {
            Console.WriteLine("WRECKS created=False ok=False");
            return 1;
        }

        var created = destroyed.Events.OfType<WreckCreated>().Any() &&
                      wreck.MaterialRecoverable == 50;

        var salvaged = engine.ExecuteWreckCommand(
            new RecoverWreckCommand("A", wreck.Id));
        var material = world.Regions["r0"].GetMaterial("A");

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var hashOk = WorldStateHasher.Compute(world) == WorldStateHasher.Compute(loaded);
        var ok = created &&
                 salvaged.Success &&
                 salvaged.Events.OfType<WreckSalvaged>().Any() &&
                 world.Wrecks.Count == 0 &&
                 material == 50 &&
                 world.Statistics.MaterialSalvagedTotal == 50 &&
                 hashOk;
        Console.WriteLine(
            $"WRECKS created={created} material={wreck.MaterialRecoverable} " +
            $"salvaged={salvaged.Success} stock={material} " +
            $"salvagedTotal={world.Statistics.MaterialSalvagedTotal} " +
            $"remaining={world.Wrecks.Count} hashOk={hashOk} ok={ok}");

        return ok ? 0 : 1;
    }
}
