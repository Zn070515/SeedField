using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

internal static class TestWorlds
{
    public const int RegionSize = 20_000;

    public static WorldState SingleRegion()
    {
        return new WorldBuilder()
            .AddRegion("r0", RegionSize, RegionSize)
            .Build();
    }

    public static WorldState CombatPair()
    {
        return new WorldBuilder()
            .AddRegion("r0", RegionSize, RegionSize)
            .AddEntity(Duelist(1, "A", new Vec2(1_000, 1_000)))
            .AddEntity(Duelist(2, "B", new Vec2(9_000, 1_000)))
            .Build();
    }

    public static EntityDefinition Duelist(long id, string civilizationId, Vec2 position) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            position,
            MaxHealth: 100,
            Energy: 1_000_000,
            EnergyCapacity: 1_000_000,
            Material: 0,
            Sensor: new SensorModule(6_000),
            Weapon: new WeaponModule(RangeMm: 5_000, Damage: 7, EnergyCost: 1, CooldownTicks: 1),
            Drive: new DriveModule(SpeedMmPerTick: 100, EnergyCostPerMove: 1),
            Comm: new CommModule(LatencyTicks: 1, EnergyCostPerMessage: 1));
}
