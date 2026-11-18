using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// Malformed-save matrix: corrupt one field at a time and require a typed
/// SaveFormatException code instead of silent acceptance. This complements the
/// happy-path roundtrip tests and the golden hashes.
/// </summary>
public sealed class SaveValidationMatrixTests
{
    [Fact]
    public void NegativePerKindOreIsRejected()
    {
        var data = ValidData();
        var corrupted = data with
        {
            Regions = data.Regions.Select(region => region with
            {
                Stocks = region.Stocks!.Select(stock => stock with
                {
                    OreCopper = -1,
                }).ToArray(),
            }).ToArray(),
        };

        AssertCode("INVALID_STOCK", corrupted);
    }

    [Fact]
    public void DuplicateDiscoveryCivilizationIsRejected()
    {
        var data = ValidData();
        var corrupted = data with
        {
            DiscoveredDeposits = new[]
            {
                new DiscoverySaveData("A", new long[] { 1 }),
                new DiscoverySaveData("A", new long[] { 1 }),
            },
        };

        AssertCode("INVALID_DISCOVERY", corrupted);
    }

    [Fact]
    public void UnknownDiscoveredDepositIsRejected()
    {
        var data = ValidData();
        var corrupted = data with
        {
            DiscoveredDeposits = new[]
            {
                new DiscoverySaveData("A", new long[] { 999 }),
            },
        };

        AssertCode("INVALID_DISCOVERY", corrupted);
    }

    [Fact]
    public void InvalidSmelterPreferenceIsRejected()
    {
        var data = ValidData();
        var corrupted = MutateFirstEntity(data, entity => entity with
        {
            Smelter = new SmelterSaveData(4, 1, (OreKind)99),
        });

        AssertCode("INVALID_SMELTER", corrupted);
    }

    [Fact]
    public void InvalidFabricatorRatioIsRejected()
    {
        var data = ValidData();
        var corrupted = MutateFirstEntity(data, entity => entity with
        {
            Fabricator = new FabricatorSaveData(1, 0, 1, 10),
        });

        AssertCode("INVALID_FABRICATOR", corrupted);
    }

    [Fact]
    public void NegativeArmorIsRejected()
    {
        var data = ValidData();
        var corrupted = MutateFirstEntity(data, entity => entity with
        {
            Armor = new ArmorSaveData(-1),
        });

        AssertCode("INVALID_ARMOR", corrupted);
    }

    private static void AssertCode(string expectedCode, WorldSaveData data)
    {
        var exception = Assert.Throws<SaveFormatException>(() =>
            WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(data)));
        Assert.Equal(expectedCode, exception.Code);
    }

    private static WorldSaveData MutateFirstEntity(
        WorldSaveData data,
        Func<EntitySaveData, EntitySaveData> mutate) =>
        data with
        {
            Regions = data.Regions.Select(region => region with
            {
                Entities = region.Entities.Select((entity, index) =>
                    index == 0 ? mutate(entity) : entity).ToArray(),
            }).ToArray(),
        };

    private static WorldSaveData ValidData()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", material: 5, ore: 0, components: 1)
            .AddDeposit(OreKind.Copper, "r0", new Vec2(500, 500), 200, 50, 5)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Smelter: new SmelterModule(4, 1),
                Fabricator: new FabricatorModule(1, 2, 1, 10),
                Armor: new ArmorModule(2),
                Shield: new ShieldModule(10, 1, 1, 0)))
            .Build();
        return WorldSaveSerializer.ToData(world) with
        {
            DiscoveredDeposits = new[] { new DiscoverySaveData("A", new long[] { 1 }) },
        };
    }
}
