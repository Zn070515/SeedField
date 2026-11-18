using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunMiningDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddDeposit(
                OreKind.Copper,
                "r0",
                new Vec2(500, 500),
                radiusMm: 200,
                remaining: 12,
                yieldPerTick: 5)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(400, 500),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Drill: new DrillModule(RatePerTick: 5, RangeMm: 600, EnergyCostPerTick: 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "r0",
                new Vec2(450, 550),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Smelter: new SmelterModule(OrePerTick: 4, EnergyCostPerTick: 1)))
            .Build();
        var engine = new SimulationEngine(world);
        var mined = 0L;
        var smelted = 0L;
        for (var tick = 0; tick < 3; tick++)
        {
            var result = engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });
            mined += result.Events.OfType<OreMined>().Sum(mine => mine.Amount);
            smelted += result.Events.OfType<OreSmelted>().Sum(smelt => smelt.Amount);
        }

        var deposit = world.Deposits.Values.Single();
        var ore = world.Regions["r0"].GetOre("A");
        var material = world.Regions["r0"].GetMaterial("A");
        var ok = mined == 12 && smelted == 8 && ore == 4 && material == 8 &&
                 deposit.Remaining == 0;
        Console.WriteLine(
            $"MINING ore={mined} smelted={smelted} oreStock={ore} material={material} " +
            $"remaining={deposit.Remaining} depleted={deposit.IsDepleted} ok={ok}");
        return ok ? 0 : 1;
    }
}
