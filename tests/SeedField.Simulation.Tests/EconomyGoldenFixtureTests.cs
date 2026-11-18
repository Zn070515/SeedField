using System.Text.Json;
using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// Golden economy fixtures: implementation-independent JSON examples that pin the
/// rules, not the implementation. A failing case means either the rules changed
/// (update the fixture and explain) or the implementation regressed.
/// </summary>
public sealed class EconomyGoldenFixtureTests
{
    [Fact]
    public void SmeltingRecipesMatchTheGoldenFixture()
    {
        var fixture = LoadFixture();

        Assert.Equal(
            fixture.Cases.Count,
            SmeltingRecipes.All.Count);
        Assert.Equal(
            fixture.Cases
                .Select(item => Enum.Parse<OreKind>(item.Kind))
                .OrderBy(kind => kind),
            SmeltingRecipes.All.Select(recipe => recipe.Kind).OrderBy(kind => kind));

        foreach (var item in fixture.Cases)
        {
            var kind = Enum.Parse<OreKind>(item.Kind);
            var recipe = SmeltingRecipes.For(kind);
            Assert.Equal(item.OrePerBatch, recipe.OrePerBatch);
            Assert.Equal(item.MaterialPerBatch, recipe.MaterialPerBatch);
            Assert.Equal(Enum.Parse<SmeltByproduct>(item.Byproduct), recipe.Byproduct);
            Assert.Equal(item.ByproductPerBatch, recipe.ByproductPerBatch);
        }
    }

    [Theory]
    [MemberData(nameof(FixtureCases))]
    public void OneTickProducesTheGoldenMaterialAndByproduct(string kind)
    {
        var fixture = LoadFixture();
        var item = fixture.Cases.Single(entry =>
            string.Equals(entry.Kind, kind, StringComparison.Ordinal));
        var oreKind = Enum.Parse<OreKind>(item.Kind);
        var byproduct = Enum.Parse<SmeltByproduct>(item.Byproduct);

        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 1_000,
                EnergyCapacity: 1_000,
                Material: 0,
                Smelter: new SmelterModule(
                    fixture.SmelterOrePerTick,
                    EnergyCostPerTick: 0)))
            .Build();
        world.Regions["r0"].AddOre("A", oreKind, item.InputOre);
        var engine = new SimulationEngine(world);

        engine.Step();

        var region = world.Regions["r0"];
        Assert.Equal(item.ExpectedMaterial, region.GetMaterial("A"));
        Assert.Equal(
            item.ExpectedByproduct,
            byproduct switch
            {
                SmeltByproduct.Components => region.GetComponents("A"),
                SmeltByproduct.Energy => region.GetEnergy("A"),
                _ => 0,
            });
        Assert.Equal(
            item.InputOre - (item.ExpectedBatches * item.OrePerBatch),
            region.GetOre("A", oreKind));
    }

    public static IEnumerable<object[]> FixtureCases()
    {
        foreach (var item in LoadFixture().Cases)
        {
            yield return new object[] { item.Kind };
        }
    }

    private static Fixture LoadFixture()
    {
        var path = Path.Combine(
            FindRepoRoot(),
            "tests",
            "fixtures",
            "economy",
            "smelting-recipes.v1.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Fixture>(
                   json,
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException("Economy fixture is empty.");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SeedField.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed record Fixture(
        int SchemaVersion,
        long SmelterOrePerTick,
        List<Case> Cases);

    private sealed record Case(
        string Kind,
        long OrePerBatch,
        long MaterialPerBatch,
        string Byproduct,
        long ByproductPerBatch,
        long InputOre,
        long ExpectedBatches,
        long ExpectedMaterial,
        long ExpectedByproduct);
}
