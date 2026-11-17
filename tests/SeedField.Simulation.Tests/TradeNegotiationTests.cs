using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class TradeNegotiationTests
{
    [Fact]
    public void TargetCanCounterAndOriginalOfferIsRefunded()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var original = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 20, 0, ExpiresInTicks: 10));
        Assert.Equal(80, world.Regions["r0"].GetMaterial("A"));

        var countered = engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("B", original.TradeOfferId!.Value, 20, 0, 24, 0, 10));

        Assert.True(countered.Success);
        var offer = Assert.Single(world.TradeOffers.Values);
        Assert.Equal(countered.TradeOfferId, offer.Id);
        Assert.Equal(original.TradeOfferId, offer.NegotiationId);
        Assert.Equal(2, offer.Round);
        Assert.Equal("B", offer.ProposerCivilizationId);
        Assert.Equal("A", offer.TargetCivilizationId);
        Assert.Equal(20, offer.GiveMaterial);
        Assert.Equal(24, offer.WantMaterial);
        Assert.Equal(100, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(80, world.Regions["r0"].GetMaterial("B"));
    }

    [Fact]
    public void OnlyTheTargetCanCounter()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var original = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 20, 0, ExpiresInTicks: 10));

        var countered = engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("A", original.TradeOfferId!.Value, 20, 0, 24, 0, 10));

        Assert.False(countered.Success);
        Assert.Equal(SimulationErrorCode.TradeNotFound, countered.ErrorCode);
        Assert.Single(world.TradeOffers);
        Assert.Equal(80, world.Regions["r0"].GetMaterial("A"));
    }

    [Fact]
    public void CounterRequiresTheGiveGoods()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddStock("r0", "A", 100)
            .AddStock("r0", "B", 10)
            .Build();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var original = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 20, 0, ExpiresInTicks: 10));

        var countered = engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("B", original.TradeOfferId!.Value, 20, 0, 24, 0, 10));

        Assert.False(countered.Success);
        Assert.Equal(SimulationErrorCode.InsufficientMaterial, countered.ErrorCode);
        Assert.Single(world.TradeOffers);
        Assert.Equal(10, world.Regions["r0"].GetMaterial("B"));
    }

    [Fact]
    public void CounterIsRejectedAtRoundThree()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var first = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 20, 0, ExpiresInTicks: 10));
        var second = engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("B", first.TradeOfferId!.Value, 20, 0, 24, 0, 10));
        var third = engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("A", second.TradeOfferId!.Value, 24, 0, 28, 0, 10));
        Assert.Equal(3, world.TradeOffers.Values.Single().Round);

        var fourth = engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("B", third.TradeOfferId!.Value, 28, 0, 30, 0, 10));

        Assert.False(fourth.Success);
        Assert.Equal(SimulationErrorCode.NegotiationExhausted, fourth.ErrorCode);
        Assert.Equal(3, world.TradeOffers.Values.Single().Round);
    }

    [Fact]
    public void NegotiationSurvivesSaveLoad()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var first = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 20, 0, ExpiresInTicks: 10));
        engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("B", first.TradeOfferId!.Value, 20, 0, 24, 0, 10));

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var offer = Assert.Single(loaded.TradeOffers.Values);

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(2, offer.Round);
        Assert.Equal(first.TradeOfferId, offer.NegotiationId);
    }

    [Fact]
    public void V7OfferMigratesToRoundOne()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var offer = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 20, 0, ExpiresInTicks: 10));
        var data = WorldSaveSerializer.ToData(world);
        var v7Data = data with
        {
            SchemaVersion = 7,
            TradeOffers = new[]
            {
                data.TradeOffers![0] with { NegotiationId = 0, Round = 0 },
            },
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(v7Data));
        var migrated = Assert.Single(loaded.TradeOffers.Values);

        Assert.Equal(offer.TradeOfferId, migrated.NegotiationId);
        Assert.Equal(1, migrated.Round);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    [Fact]
    public void ObservationExposesNegotiationRound()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var first = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 20, 0, ExpiresInTicks: 10));
        engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("B", first.TradeOfferId!.Value, 20, 0, 24, 0, 10));

        var observation = ObservationBuilder.Build(world, "A");
        var observed = Assert.Single(observation.TradeOffers!);

        Assert.Equal(2, observed.Round);
        Assert.Equal(first.TradeOfferId, observed.NegotiationId);
    }

    private static void CreateTradeTreaty(SimulationEngine engine)
    {
        var proposal = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 10));
        engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", proposal.ProposalId!.Value, Accept: true));
    }

    private static WorldState TradeWorld()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddStock("r0", "A", 100)
            .AddStock("r0", "B", 100)
            .Build();
        return world;
    }

    private static EntityDefinition Unit(long id, string civilizationId) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Comm: new CommModule(1, EnergyCostPerMessage: 0));
}
