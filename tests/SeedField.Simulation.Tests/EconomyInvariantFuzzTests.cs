using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// Economy fuzz: fixed seeds drive mining, smelting, fabrication and production
/// toggles. Invariants cover non-negative stocks, deposit monotonicity, ore
/// conservation (mined = smelted + stock) and save/load determinism.
/// </summary>
public sealed class EconomyInvariantFuzzTests
{
    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomEconomyKeepsConservationAndDeterminism(int seed)
    {
        var random = new Random(seed);
        var initial = new Dictionary<OreKind, long>();
        var builder = new WorldBuilder().AddRegion("r0", 10_000, 10_000);
        var index = 0;
        foreach (var kind in Enum.GetValues<OreKind>())
        {
            var remaining = random.Next(40, 121);
            initial[kind] = remaining;
            builder.AddDeposit(
                kind,
                "r0",
                new Vec2(1_000 + (index * 300), 1_000),
                radiusMm: 200,
                remaining: remaining,
                yieldPerTick: 5);
            index++;
        }

        var world = builder
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(1_000, 1_000),
                MaxHealth: 100,
                Energy: 100_000,
                EnergyCapacity: 100_000,
                Material: 0,
                Drill: new DrillModule(5, 2_000, 0),
                Smelter: new SmelterModule(4, 0),
                Fabricator: new FabricatorModule(1, 2, 0, 50)))
            .Build();
        var engine = new SimulationEngine(world);
        var minedByEvents = 0L;

        for (var tick = 0; tick < 40; tick++)
        {
            var intents = new List<Intent> { new MineIntent(new EntityId(1)) };
            if (tick % 7 == 0)
            {
                intents.Add(new SetProductionIntent(
                    new EntityId(1),
                    Enabled: (tick / 7) % 2 == 0));
            }

            var result = engine.Step(intents);
            minedByEvents += result.Events.OfType<OreMined>().Sum(mine => mine.Amount);
            AssertEconomyInvariants(world, initial, minedByEvents);
        }

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    private static void AssertEconomyInvariants(
        WorldState world,
        IReadOnlyDictionary<OreKind, long> initial,
        long minedByEvents)
    {
        var region = world.Regions["r0"];
        var stock = region.Stocks["A"];
        Assert.True(stock.OreCopper >= 0, "copper ore went negative");
        Assert.True(stock.OreLead >= 0, "lead ore went negative");
        Assert.True(stock.OreTitanium >= 0, "titanium ore went negative");
        Assert.True(stock.OreCoal >= 0, "coal ore went negative");
        Assert.True(stock.Material >= 0, "material went negative");
        Assert.True(stock.Components >= 0, "components went negative");
        Assert.True(stock.Energy >= 0, "energy went negative");

        foreach (var deposit in world.Deposits.Values)
        {
            Assert.InRange(deposit.Remaining, 0, initial[deposit.Kind]);
        }

        Assert.Equal(world.Statistics.OreMinedTotal, minedByEvents);
        Assert.Equal(
            world.Statistics.OreMinedTotal,
            world.Statistics.OreSmeltedTotal + region.GetOre("A"));
    }

    public static IEnumerable<object[]> Seeds() =>
        Enumerable.Range(1, 20).Select(seed => new object[] { seed });
}
