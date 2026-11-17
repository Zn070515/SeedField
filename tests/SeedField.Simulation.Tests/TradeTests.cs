using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class TradeTests
{
    [Fact]
    public void TradeRequiresATradeTreaty()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);

        var result = engine.ExecuteDiplomacyCommand(TradeOffer());

        Assert.False(result.Success);
        Assert.Equal(SimulationErrorCode.TreatyDenied, result.ErrorCode);
        Assert.Empty(world.TradeOffers);
        Assert.Equal(100, world.Regions["r0"].GetMaterial("A"));
    }

    [Fact]
    public void ProposingTradeEscrowsTheOffer()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);

        var result = engine.ExecuteDiplomacyCommand(TradeOffer());

        Assert.True(result.Success);
        Assert.Single(world.TradeOffers);
        Assert.Equal(70, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(40, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(0, world.Statistics.TradeMaterialSettledTotal);
        Assert.Equal(0, world.Statistics.TradeEnergySettledTotal);
    }

    [Fact]
    public void AcceptingTradeSwapsBothSidesAtomically()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var proposed = engine.ExecuteDiplomacyCommand(TradeOffer());

        var accepted = engine.ExecuteDiplomacyCommand(
            new RespondTradeCommand("B", proposed.TradeOfferId!.Value, Accept: true));

        Assert.True(accepted.Success);
        Assert.Empty(world.TradeOffers);
        Assert.Equal(90, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(45, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(60, world.Regions["r0"].GetMaterial("B"));
        Assert.Equal(105, world.Regions["r0"].GetEnergy("B"));
        Assert.Equal(50, world.Statistics.TradeMaterialSettledTotal);
        Assert.Equal(15, world.Statistics.TradeEnergySettledTotal);
    }

    [Fact]
    public void AcceptingWithoutTargetGoodsDoesNotMutateOrConsumeTheOffer()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddStock("r0", "A", 100)
            .AddStock("r0", "B", 0)
            .Build();
        world.Regions["r0"].AddEnergy("A", 50);
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var proposed = engine.ExecuteDiplomacyCommand(TradeOffer());

        var accepted = engine.ExecuteDiplomacyCommand(
            new RespondTradeCommand("B", proposed.TradeOfferId!.Value, Accept: true));

        Assert.False(accepted.Success);
        Assert.Equal(SimulationErrorCode.InsufficientMaterial, accepted.ErrorCode);
        Assert.Single(world.TradeOffers);
        Assert.Equal(70, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(40, world.Regions["r0"].GetEnergy("A"));
        Assert.Equal(0, world.Regions["r0"].GetMaterial("B"));
    }

    [Fact]
    public void RejectingTradeRefundsTheEscrow()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var proposed = engine.ExecuteDiplomacyCommand(TradeOffer());

        var rejected = engine.ExecuteDiplomacyCommand(
            new RespondTradeCommand("B", proposed.TradeOfferId!.Value, Accept: false));

        Assert.True(rejected.Success);
        Assert.Empty(world.TradeOffers);
        Assert.Equal(100, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(50, world.Regions["r0"].GetEnergy("A"));
        Assert.Contains(
            rejected.Events,
            e => e is TradeRejected { RefundedMaterial: 30, RefundedEnergy: 10 });
    }

    [Fact]
    public void CancellingTradeRefundsTheEscrow()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var proposed = engine.ExecuteDiplomacyCommand(TradeOffer());

        var cancelled = engine.ExecuteDiplomacyCommand(
            new CancelTradeCommand("A", proposed.TradeOfferId!.Value));

        Assert.True(cancelled.Success);
        Assert.Empty(world.TradeOffers);
        Assert.Equal(100, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(50, world.Regions["r0"].GetEnergy("A"));
    }

    [Fact]
    public void ExpiredTradeRefundsTheEscrow()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        engine.ExecuteDiplomacyCommand(TradeOffer() with { ExpiresInTicks = 1 });
        Assert.Equal(70, world.Regions["r0"].GetMaterial("A"));

        var step = engine.Step(Array.Empty<Intent>());

        Assert.Empty(world.TradeOffers);
        Assert.Equal(100, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(50, world.Regions["r0"].GetEnergy("A"));
        Assert.Contains(step.Events, e => e is TradeExpired);
    }

    [Fact]
    public void BreakingTradeTreatyCancelsAndRefundsOpenOffers()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        var proposed = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 10));
        var accepted = engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", proposed.ProposalId!.Value, Accept: true));
        engine.ExecuteDiplomacyCommand(TradeOffer());

        var broken = engine.ExecuteDiplomacyCommand(
            new BreakTreatyCommand("B", accepted.TreatyId!.Value));

        Assert.True(broken.Success);
        Assert.Empty(world.TradeOffers);
        Assert.Equal(100, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(50, world.Regions["r0"].GetEnergy("A"));
        Assert.Contains(broken.Events, e => e is TradeCancelled);
    }

    [Fact]
    public void DiplomacyStateSurvivesSaveLoad()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));
        engine.ExecuteDiplomacyCommand(TradeOffer());

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Single(loaded.Treaties);
        Assert.Single(loaded.TreatyProposals);
        Assert.Single(loaded.TradeOffers);
        Assert.Equal(70, loaded.Regions["r0"].GetMaterial("A"));
    }

    [Fact]
    public void SettledTradeStatsSurviveSaveLoad()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var proposed = engine.ExecuteDiplomacyCommand(TradeOffer());
        engine.ExecuteDiplomacyCommand(
            new RespondTradeCommand("B", proposed.TradeOfferId!.Value, Accept: true));

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(50, world.Statistics.TradeMaterialSettledTotal);
        Assert.Equal(15, world.Statistics.TradeEnergySettledTotal);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(50, loaded.Statistics.TradeMaterialSettledTotal);
        Assert.Equal(15, loaded.Statistics.TradeEnergySettledTotal);
    }

    [Fact]
    public void V6SaveMigratesToEmptyDiplomacyState()
    {
        var world = TradeWorld();
        var v6Data = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 6,
            TreatyProposals = null,
            Treaties = null,
            TradeOffers = null,
            NextTreatyProposalId = 0,
            NextTreatyId = 0,
            NextTradeOfferId = 0,
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(v6Data));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Empty(loaded.Treaties);
        Assert.Empty(loaded.TreatyProposals);
        Assert.Empty(loaded.TradeOffers);
    }

    [Fact]
    public void DeviceObservationFiltersTradeOffersByRegion()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddEntity(Unit(3, "A", regionId: "r1"))
            .AddStock("r0", "A", 100)
            .AddStock("r0", "B", 50)
            .Build();
        world.Regions["r0"].AddEnergy("A", 50);
        world.Regions["r0"].AddEnergy("B", 100);
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        engine.ExecuteDiplomacyCommand(TradeOffer());

        var civilizationView = ObservationBuilder.Build(world, "A");
        var deviceView = ObservationBuilder.BuildForEntity(world, new EntityId(3));

        Assert.Single(civilizationView.TradeOffers!);
        Assert.Empty(deviceView.TradeOffers!);
    }

    private static void CreateTradeTreaty(SimulationEngine engine)
    {
        var proposed = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 10));
        var accepted = engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", proposed.ProposalId!.Value, Accept: true));
        Assert.True(accepted.Success);
    }

    private static ProposeTradeCommand TradeOffer() =>
        new(
            "A",
            "B",
            "r0",
            GiveMaterial: 30,
            GiveEnergy: 10,
            WantMaterial: 20,
            WantEnergy: 5,
            ExpiresInTicks: 10);

    private static WorldState TradeWorld()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddStock("r0", "A", 100)
            .AddStock("r0", "B", 50)
            .Build();
        world.Regions["r0"].AddEnergy("A", 50);
        world.Regions["r0"].AddEnergy("B", 100);
        return world;
    }

    private static EntityDefinition Unit(
        long id,
        string civilizationId,
        string regionId = "r0") =>
        new(
            new EntityId(id),
            civilizationId,
            regionId,
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Comm: new CommModule(1, EnergyCostPerMessage: 0));
}
