using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// Multi-ore smelting recipes: each deposit kind feeds its own stock, recipes
/// produce material plus optional components/energy byproducts, and selection is
/// deterministic.
/// </summary>
public sealed class OreRecipeTests
{
    [Theory]
    [InlineData(OreKind.Copper)]
    [InlineData(OreKind.Lead)]
    [InlineData(OreKind.Titanium)]
    [InlineData(OreKind.Coal)]
    public void DrillsStoreOreUnderTheDepositKind(OreKind kind)
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddDeposit(kind, "r0", new Vec2(500, 500), 200, 20, 5)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(400, 500),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Drill: new DrillModule(5, 600, 1)))
            .Build();
        var engine = new SimulationEngine(world);

        engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });

        Assert.Equal(5, world.Regions["r0"].GetOre("A", kind));
        Assert.Equal(5, world.Regions["r0"].GetOre("A"));
    }

    [Fact]
    public void CopperRecipeIsOneToOne()
    {
        var world = SmelterWorld(
            copper: 10,
            smelter: new SmelterModule(OrePerTick: 5, EnergyCostPerTick: 1));
        var engine = new SimulationEngine(world);

        var result = engine.Step();

        var smelted = Assert.IsType<OreSmelted>(Assert.Single(result.Events));
        Assert.Equal(OreKind.Copper, smelted.Kind);
        Assert.Equal(5, smelted.Amount);
        Assert.Equal(5, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(5, world.Regions["r0"].GetOre("A", OreKind.Copper));
    }

    [Fact]
    public void LeadRecipeProducesComponentsAsAByproduct()
    {
        var world = SmelterWorld(
            lead: 10,
            smelter: new SmelterModule(OrePerTick: 4, EnergyCostPerTick: 1));
        var engine = new SimulationEngine(world);

        var result = engine.Step();

        var smelted = Assert.IsType<OreSmelted>(Assert.Single(result.Events));
        Assert.Equal(OreKind.Lead, smelted.Kind);
        Assert.Equal(4, smelted.Amount);
        Assert.Equal(SmeltByproduct.Components, smelted.Byproduct);
        Assert.Equal(2, smelted.ByproductAmount);
        Assert.Equal(2, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(2, world.Regions["r0"].GetComponents("A"));
    }

    [Fact]
    public void CoalRecipeProducesEnergyAsAByproduct()
    {
        var world = SmelterWorld(
            coal: 10,
            smelter: new SmelterModule(OrePerTick: 4, EnergyCostPerTick: 1));
        var engine = new SimulationEngine(world);

        var result = engine.Step();

        var smelted = Assert.IsType<OreSmelted>(Assert.Single(result.Events));
        Assert.Equal(OreKind.Coal, smelted.Kind);
        Assert.Equal(SmeltByproduct.Energy, smelted.Byproduct);
        Assert.Equal(8, smelted.ByproductAmount);
        Assert.Equal(2, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(8, world.Regions["r0"].GetEnergy("A"));
    }

    [Fact]
    public void TitaniumRecipeYieldsThreeMaterialPerTwoOre()
    {
        var world = SmelterWorld(
            titanium: 10,
            smelter: new SmelterModule(OrePerTick: 4, EnergyCostPerTick: 1));
        var engine = new SimulationEngine(world);

        engine.Step();

        Assert.Equal(6, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(6, world.Regions["r0"].GetOre("A", OreKind.Titanium));
    }

    [Fact]
    public void PreferredOreOverridesAbundanceSelection()
    {
        var world = SmelterWorld(
            copper: 20,
            lead: 4,
            smelter: new SmelterModule(4, 1, PreferredOre: OreKind.Lead));
        var engine = new SimulationEngine(world);

        engine.Step();

        Assert.Equal(0, world.Regions["r0"].GetOre("A", OreKind.Lead));
        Assert.Equal(20, world.Regions["r0"].GetOre("A", OreKind.Copper));
    }

    [Fact]
    public void MostAbundantKindIsChosenWhenNoPreferenceIsSet()
    {
        var world = SmelterWorld(
            copper: 4,
            coal: 20,
            smelter: new SmelterModule(4, 1));
        var engine = new SimulationEngine(world);

        var result = engine.Step();

        var smelted = Assert.IsType<OreSmelted>(Assert.Single(result.Events));
        Assert.Equal(OreKind.Coal, smelted.Kind);
        Assert.Equal(4, world.Regions["r0"].GetOre("A", OreKind.Copper));
    }

    [Fact]
    public void SaveRoundTripKeepsPerKindOreAndRecipePreference()
    {
        var world = SmelterWorld(
            copper: 3,
            lead: 4,
            coal: 5,
            smelter: new SmelterModule(4, 1, PreferredOre: OreKind.Lead));

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(3, loaded.Regions["r0"].GetOre("A", OreKind.Copper));
        Assert.Equal(4, loaded.Regions["r0"].GetOre("A", OreKind.Lead));
        Assert.Equal(5, loaded.Regions["r0"].GetOre("A", OreKind.Coal));
        Assert.Equal(
            OreKind.Lead,
            loaded.FindEntity(new EntityId(1))!.Smelter!.PreferredOre);
    }

    [Fact]
    public void V24SingleOreSaveMigratesToCopper()
    {
        var world = SmelterWorld(copper: 3);
        var data = WorldSaveSerializer.ToData(world);
        var v24 = data with
        {
            SchemaVersion = 24,
            Regions = data.Regions.Select(region => region with
            {
                Stocks = region.Stocks?.Select(stock => stock with
                {
                    Ore = stock.OreCopper + stock.OreLead +
                          stock.OreTitanium + stock.OreCoal,
                    OreCopper = 0,
                    OreLead = 0,
                    OreTitanium = 0,
                    OreCoal = 0,
                }).ToArray(),
            }).ToArray(),
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(v24));

        Assert.Equal(3, loaded.Regions["r0"].GetOre("A", OreKind.Copper));
    }

    [Fact]
    public void ObservationExposesPerKindOre()
    {
        var world = SmelterWorld(copper: 3, lead: 4, coal: 5);

        var observation = new SimulationEngine(world)
            .BuildObservation("A");

        Assert.Equal(12, observation.Ore);
        Assert.Equal(3, observation.OreCopper);
        Assert.Equal(4, observation.OreLead);
        Assert.Equal(0, observation.OreTitanium);
        Assert.Equal(5, observation.OreCoal);
    }

    private static WorldState SmelterWorld(
        long copper = 0,
        long lead = 0,
        long titanium = 0,
        long coal = 0,
        SmelterModule? smelter = null)
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock(
                "r0",
                "A",
                material: 0,
                ore: 0,
                components: 0)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Smelter: smelter ?? new SmelterModule(1, 0)))
            .Build();
        var region = world.Regions["r0"];
        region.AddOre("A", OreKind.Copper, copper);
        region.AddOre("A", OreKind.Lead, lead);
        region.AddOre("A", OreKind.Titanium, titanium);
        region.AddOre("A", OreKind.Coal, coal);
        return world;
    }
}
