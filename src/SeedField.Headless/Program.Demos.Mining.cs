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
            .Build();
        var engine = new SimulationEngine(world);
        var mined = 0L;
        for (var tick = 0; tick < 3; tick++)
        {
            var result = engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });
            mined += result.Events.OfType<MaterialMined>().Sum(mine => mine.Amount);
        }

        var deposit = world.Deposits.Values.Single();
        var stock = world.Regions["r0"].GetMaterial("A");
        var ok = mined == 12 && stock == 12 && deposit.Remaining == 0;
        Console.WriteLine(
            $"MINING mined={mined} stock={stock} remaining={deposit.Remaining} " +
            $"depleted={deposit.IsDepleted} ok={ok}");
        return ok ? 0 : 1;
    }
}
