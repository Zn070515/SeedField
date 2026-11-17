using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class OpponentDiplomacyTests
{
    [Fact]
    public void CautiousProfileAcceptsIncomingNonAggression()
    {
        var world = World();
        var engine = new SimulationEngine(world);
        engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));

        var commands = OpponentDiplomacyPolicy.Decide(
            world,
            "B",
            "A",
            canCommunicate: true,
            OpponentDiplomacyProfiles.Cautious);

        var response = Assert.IsType<RespondTreatyCommand>(Assert.Single(commands));
        Assert.True(response.Accept);
        Assert.Equal("B", response.CivilizationId);
        engine.ExecuteDiplomacyCommand(response);
        Assert.Single(world.Treaties);
    }

    [Fact]
    public void AggressiveProfileRejectsIncomingNonAggression()
    {
        var world = World();
        var engine = new SimulationEngine(world);
        engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));

        var response = Assert.IsType<RespondTreatyCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Aggressive)));

        Assert.False(response.Accept);
    }

    [Fact]
    public void TradeProposalUsesTheTradeAgreementFlag()
    {
        var world = World();
        var engine = new SimulationEngine(world);
        engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 10));
        var rejectingProfile = OpponentDiplomacyProfiles.Cautious with
        {
            AcceptsTradeAgreement = false,
        };

        var response = Assert.IsType<RespondTreatyCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                rejectingProfile)));

        Assert.False(response.Accept);
    }

    [Fact]
    public void NoCommunicationMeansNoResponse()
    {
        var world = World();
        var engine = new SimulationEngine(world);
        engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));

        var commands = OpponentDiplomacyPolicy.Decide(
            world,
            "B",
            "A",
            canCommunicate: false,
            OpponentDiplomacyProfiles.Cautious);

        Assert.Empty(commands);
    }

    [Fact]
    public void OutgoingItemsAreIgnored()
    {
        var world = World();
        var engine = new SimulationEngine(world);
        engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("B", "A", TreatyKind.NonAggression, ExpiresInTicks: 10));

        var commands = OpponentDiplomacyPolicy.Decide(
            world,
            "B",
            "A",
            canCommunicate: true,
            OpponentDiplomacyProfiles.Cautious);

        Assert.Empty(commands);
    }

    [Fact]
    public void ResponsesAreOrderedByStableProposalId()
    {
        var world = World();
        var engine = new SimulationEngine(world);
        var first = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));
        var second = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 10));

        var commands = OpponentDiplomacyPolicy.Decide(
            world,
            "B",
            "A",
            canCommunicate: true,
            OpponentDiplomacyProfiles.Cautious);

        Assert.Collection(
            commands,
            command => Assert.Equal(first.ProposalId, ((RespondTreatyCommand)command).ProposalId),
            command => Assert.Equal(second.ProposalId, ((RespondTreatyCommand)command).ProposalId));
    }

    [Fact]
    public void CautiousProfileAcceptsAFairTradeOffer()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var offer = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 10, 0, ExpiresInTicks: 10));

        var response = Assert.IsType<RespondTradeCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Cautious)));

        Assert.True(response.Accept);
        Assert.Equal(offer.TradeOfferId, response.TradeOfferId);
    }

    [Fact]
    public void AggressiveProfileCountersToDemandAProfitMargin()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 20, 0, ExpiresInTicks: 10));

        var response = Assert.IsType<CounterTradeCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Aggressive)));

        Assert.Equal(20, response.GiveMaterial);
        Assert.Equal(24, response.WantMaterial);
    }

    [Fact]
    public void UnaffordableTradeOfferIsRejected()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddStock("r0", "A", 100)
            .AddStock("r0", "B", 0)
            .Build();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 50, 0, 10, 0, ExpiresInTicks: 10));

        var response = Assert.IsType<RespondTradeCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Cautious)));

        Assert.False(response.Accept);
    }

    [Fact]
    public void ProfilesMapBuiltInPackages()
    {
        Assert.Same(
            OpponentDiplomacyProfiles.Aggressive,
            OpponentDiplomacyProfiles.ForPackage("builtin.skirmisher"));
        Assert.Same(
            OpponentDiplomacyProfiles.Cautious,
            OpponentDiplomacyProfiles.ForPackage("builtin.turtle"));
        Assert.Same(
            OpponentDiplomacyProfiles.Default,
            OpponentDiplomacyProfiles.ForPackage("unknown.package"));
    }

    [Fact]
    public void ProfileCanRefuseAllTradeOffers()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 50, 0, 10, 0, ExpiresInTicks: 10));
        var refusingProfile = OpponentDiplomacyProfiles.Cautious with
        {
            AcceptsTradeOffers = false,
        };

        var response = Assert.IsType<RespondTradeCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                refusingProfile)));

        Assert.False(response.Accept);
    }

    [Fact]
    public void TurtleProactivelyProposesNonAggressionAtCadence()
    {
        var world = World();
        AdvanceToTick(world, 240);

        var command = Assert.IsType<ProposeTreatyCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Cautious)));

        Assert.Equal(TreatyKind.NonAggression, command.Kind);
        Assert.Equal("B", command.CivilizationId);
        Assert.Equal("A", command.TargetCivilizationId);
    }

    [Fact]
    public void AggressiveProposesTradeInsteadOfNonAggression()
    {
        var world = World();
        AdvanceToTick(world, 240);

        var command = Assert.IsType<ProposeTreatyCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Aggressive)));

        Assert.Equal(TreatyKind.Trade, command.Kind);
    }

    [Fact]
    public void NoProactiveCommandsOffCadence()
    {
        var world = World();
        AdvanceToTick(world, 239);

        var commands = OpponentDiplomacyPolicy.Decide(
            world,
            "B",
            "A",
            canCommunicate: true,
            OpponentDiplomacyProfiles.Cautious);

        Assert.Empty(commands);
    }

    [Fact]
    public void ProactiveProposalSkipsWhenTheSameKindIsPending()
    {
        var world = World();
        var engine = new SimulationEngine(world);
        engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("B", "A", TreatyKind.NonAggression, ExpiresInTicks: 500));
        AdvanceToTick(world, 240);
        var profile = OpponentDiplomacyProfiles.Cautious with
        {
            ProposesTradeAgreement = false,
            ProposesTradeOffers = false,
        };

        var commands = OpponentDiplomacyPolicy.Decide(
            world,
            "B",
            "A",
            canCommunicate: true,
            profile);

        Assert.Empty(commands);
    }

    [Fact]
    public void ProactiveOfferRequiresActiveTradeTreaty()
    {
        var world = TradeWorld();
        AdvanceToTick(world, 240);
        var profile = OpponentDiplomacyProfiles.Cautious with
        {
            ProposesNonAggression = false,
            ProposesTradeAgreement = false,
            ProposesTradeOffers = true,
        };

        var commands = OpponentDiplomacyPolicy.Decide(
            world,
            "B",
            "A",
            canCommunicate: true,
            profile);

        Assert.Empty(commands);
    }

    [Fact]
    public void ProactiveOfferUsesProfileGoods()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        AdvanceToTick(world, 240);
        var profile = OpponentDiplomacyProfiles.Cautious with
        {
            ProposesNonAggression = false,
            ProposesTradeAgreement = false,
            ProposesTradeOffers = true,
        };

        var command = Assert.IsType<ProposeTradeCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                profile)));

        Assert.Equal("B", command.CivilizationId);
        Assert.Equal("A", command.TargetCivilizationId);
        Assert.Equal(20, command.GiveMaterial);
        Assert.Equal(20, command.WantMaterial);
    }

    [Fact]
    public void ProactiveOfferSkipsWithoutStock()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddStock("r0", "A", 100)
            .AddStock("r0", "B", 0)
            .Build();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        AdvanceToTick(world, 240);
        var profile = OpponentDiplomacyProfiles.Cautious with
        {
            ProposesNonAggression = false,
            ProposesTradeAgreement = false,
            ProposesTradeOffers = true,
        };

        var commands = OpponentDiplomacyPolicy.Decide(
            world,
            "B",
            "A",
            canCommunicate: true,
            profile);

        Assert.Empty(commands);
    }

    [Fact]
    public void AggressiveCountersAnUnfairOfferWithAProfitMargin()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var offer = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 10, 0, 20, 0, ExpiresInTicks: 10));

        var response = Assert.IsType<CounterTradeCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Aggressive)));

        Assert.Equal("B", response.CivilizationId);
        Assert.Equal(offer.TradeOfferId, response.TradeOfferId);
        Assert.Equal(20, response.GiveMaterial);
        Assert.Equal(24, response.WantMaterial);
    }

    [Fact]
    public void CautiousCountersAtFairTerms()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 10, 0, 20, 0, ExpiresInTicks: 10));

        var response = Assert.IsType<CounterTradeCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Cautious)));

        Assert.Equal(20, response.GiveMaterial);
        Assert.Equal(20, response.WantMaterial);
    }

    [Fact]
    public void MixedResourceOfferIsRejectedInsteadOfCountered()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        world.Regions["r0"].AddEnergy("A", 100);
        world.Regions["r0"].AddEnergy("B", 100);
        engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 10, 5, 20, 5, ExpiresInTicks: 10));

        var response = Assert.IsType<RespondTradeCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Aggressive)));

        Assert.False(response.Accept);
    }

    [Fact]
    public void ConcessionAcceptsAtLaterRound()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var bOffer = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("B", "A", "r0", 20, 0, 20, 0, ExpiresInTicks: 10));
        engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("A", bOffer.TradeOfferId!.Value, 22, 0, 20, 0, 10));

        var response = Assert.IsType<RespondTradeCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Aggressive)));

        Assert.True(response.Accept);
    }

    [Fact]
    public void RoundThreeOfferIsRejectedInsteadOfCountered()
    {
        var world = TradeWorld();
        var engine = new SimulationEngine(world);
        CreateTradeTreaty(engine);
        var first = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 20, 0, ExpiresInTicks: 10));
        var second = engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("B", first.TradeOfferId!.Value, 20, 0, 24, 0, 10));
        engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand("A", second.TradeOfferId!.Value, 24, 0, 28, 0, 10));

        var response = Assert.IsType<RespondTradeCommand>(Assert.Single(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Aggressive)));

        Assert.False(response.Accept);
    }

    private static void AdvanceToTick(WorldState world, long tick)
    {
        var engine = new SimulationEngine(world);
        while (world.Tick < tick)
        {
            engine.Step(Array.Empty<Intent>());
        }
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

    private static WorldState World() =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .Build();

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
