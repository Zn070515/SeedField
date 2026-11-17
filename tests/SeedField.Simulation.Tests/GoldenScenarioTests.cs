using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// Fixed scenario + expected state hash. This is a deliberate golden regression: any
/// behavior change must update the constant in the same commit as the change.
/// </summary>
public sealed class GoldenScenarioTests
{
    // Updated intentionally with behavior changes; see docs/engineering/TESTING.md.
    private const ulong ExpectedHash = 0x4624154b5bc8c8d6UL;

    [Fact]
    public void GoldenScenarioHashIsStable()
    {
        var world = BuildWorld();
        var engine = new SimulationEngine(world);

        for (var tick = 0; tick < 40; tick++)
        {
            engine.Step(IntentsForTick(tick));
        }

        Assert.Equal(ExpectedHash, WorldStateHasher.Compute(world));
    }

    [Fact]
    public void GoldenScenarioReplaysIdentically()
    {
        var first = RunOnce();
        var second = RunOnce();

        Assert.Equal(first, second);
    }

    private static ulong RunOnce()
    {
        var world = BuildWorld();
        var engine = new SimulationEngine(world);
        for (var tick = 0; tick < 40; tick++)
        {
            engine.Step(IntentsForTick(tick));
        }

        return WorldStateHasher.Compute(world);
    }

    private static Intent[] IntentsForTick(int tick)
    {
        Intent scout = tick % 5 == 0
            ? new WaitIntent(new EntityId(1))
            : new MoveIntent(new EntityId(1), new Vec2(1_000, 1_000));

        return new Intent[]
        {
            scout,
            new MoveIntent(new EntityId(2), new Vec2(2_000, 500)),
            new MoveIntent(new EntityId(3), new Vec2(0, 100)),
            new AttackIntent(new EntityId(2), new EntityId(3)),
            new AttackIntent(new EntityId(3), new EntityId(2)),
        };
    }

    private static WorldState BuildWorld() =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 25)
            .AddStock("r0", "B", 25)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 200,
                Material: 0,
                Sensor: new SensorModule(6_000),
                Drive: new DriveModule(100, EnergyCostPerMove: 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "r0",
                new Vec2(100, 500),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 200,
                Material: 0,
                Sensor: new SensorModule(6_000),
                Weapon: new WeaponModule(6_000, Damage: 3, EnergyCost: 1, CooldownTicks: 2),
                Drive: new DriveModule(100, EnergyCostPerMove: 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(3),
                "B",
                "r0",
                new Vec2(2_000, 100),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 200,
                Material: 0,
                Sensor: new SensorModule(6_000),
                Weapon: new WeaponModule(6_000, Damage: 3, EnergyCost: 1, CooldownTicks: 2),
                Drive: new DriveModule(100, EnergyCostPerMove: 1)))
            .Build();
}
