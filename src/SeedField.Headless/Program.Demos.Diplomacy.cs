using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunDiplomacyDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(DiplomacyUnit(1, "A", 100))
            .AddEntity(DiplomacyUnit(2, "B", 200))
            .AddStock("r0", "A", 100)
            .AddStock("r0", "B", 50)
            .Build();
        world.Regions["r0"].AddEnergy("A", 50);
        world.Regions["r0"].AddEnergy("B", 100);
        var engine = new SimulationEngine(world);

        var napProposal = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 20));
        var nap = engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", napProposal.ProposalId!.Value, Accept: true));

        var blocked = engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });
        var napBlocked = blocked.IntentResults[0].ErrorCode == SimulationErrorCode.TreatyDenied &&
                         world.FindEntity(new EntityId(2))!.Health == 10 &&
                         world.FindEntity(new EntityId(1))!.Energy == 100;

        engine.ExecuteDiplomacyCommand(
            new BreakTreatyCommand("A", nap.TreatyId!.Value));
        var afterBreak = engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });
        var napBreakAttacked = afterBreak.IntentResults[0].Accepted &&
                               world.FindEntity(new EntityId(2))!.Health < 10;

        var tradeProposal = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 20));
        engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", tradeProposal.ProposalId!.Value, Accept: true));

        var offer = engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 30, 10, 20, 5, ExpiresInTicks: 20));
        var accepted = engine.ExecuteDiplomacyCommand(
            new RespondTradeCommand("B", offer.TradeOfferId!.Value, Accept: true));

        var tradeSwapped = accepted.Success &&
                           world.Regions["r0"].GetMaterial("A") == 90 &&
                           world.Regions["r0"].GetEnergy("A") == 45 &&
                           world.Regions["r0"].GetMaterial("B") == 60 &&
                           world.Regions["r0"].GetEnergy("B") == 105;

        engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 10, 0, 0, 5, ExpiresInTicks: 1));
        engine.Step(Array.Empty<Intent>());
        var refundOk = world.Regions["r0"].GetMaterial("A") == 90 &&
                       world.TradeOffers.Count == 0;

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var hashOk = WorldStateHasher.Compute(world) == WorldStateHasher.Compute(loaded);

        var ok = napBlocked && napBreakAttacked && tradeSwapped && refundOk && hashOk;
        Console.WriteLine(
            $"DIPLOMACY napBlocked={napBlocked} napBreakAttacked={napBreakAttacked} " +
            $"tradeSwapped={tradeSwapped} refundOk={refundOk} hashOk={hashOk} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunDiplomacyAiDemo()
    {
        var turtleWorld = AiWorld();
        var turtleEngine = new SimulationEngine(turtleWorld);
        turtleEngine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 50));
        var turtleResponse = AssertSinglePolicyCommand(
            OpponentDiplomacyPolicy.Decide(
                turtleWorld,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.ForPackage("builtin.turtle")));
        turtleEngine.ExecuteDiplomacyCommand(turtleResponse);
        var turtleNap = turtleWorld.Treaties.Count == 1;

        var aggressiveWorld = AiWorld();
        var aggressiveEngine = new SimulationEngine(aggressiveWorld);
        aggressiveEngine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 50));
        var aggressiveResponse = AssertSinglePolicyCommand(
            OpponentDiplomacyPolicy.Decide(
                aggressiveWorld,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.ForPackage("builtin.skirmisher")));
        aggressiveEngine.ExecuteDiplomacyCommand(aggressiveResponse);
        var aggressiveNapRejected =
            aggressiveWorld.Treaties.Count == 0 &&
            aggressiveResponse is RespondTreatyCommand { Accept: false };

        var tradeWorld = AiWorld();
        var tradeEngine = new SimulationEngine(tradeWorld);
        var tradeProposal = tradeEngine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 50));
        tradeEngine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", tradeProposal.ProposalId!.Value, Accept: true));

        tradeEngine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 20, 0, 10, 0, ExpiresInTicks: 50));
        var fairResponse = AssertSinglePolicyCommand(
            OpponentDiplomacyPolicy.Decide(
                tradeWorld,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Cautious));
        tradeEngine.ExecuteDiplomacyCommand(fairResponse);
        var fairAccepted =
            fairResponse is RespondTradeCommand { Accept: true } &&
            tradeWorld.TradeOffers.Count == 0 &&
            tradeWorld.Regions["r0"].GetMaterial("A") == 90 &&
            tradeWorld.Regions["r0"].GetMaterial("B") == 110;

        tradeEngine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "r0", 10, 0, 20, 0, ExpiresInTicks: 50));
        var counterResponse = AssertSinglePolicyCommand(
            OpponentDiplomacyPolicy.Decide(
                tradeWorld,
                "B",
                "A",
                canCommunicate: true,
                OpponentDiplomacyProfiles.Cautious));
        var counterRaised = counterResponse is CounterTradeCommand
        {
            GiveMaterial: 20,
            WantMaterial: 20,
        };
        tradeEngine.ExecuteDiplomacyCommand(counterResponse);
        var counterOffer = tradeWorld.TradeOffers.Values.Single();
        var acceptCounter = tradeEngine.ExecuteDiplomacyCommand(
            new RespondTradeCommand("A", counterOffer.Id, Accept: true));
        var counterAccepted =
            counterRaised &&
            acceptCounter.Success &&
            tradeWorld.TradeOffers.Count == 0 &&
            tradeWorld.Regions["r0"].GetMaterial("A") == 90 &&
            tradeWorld.Regions["r0"].GetMaterial("B") == 110;

        var ok = turtleNap &&
                 aggressiveNapRejected &&
                 fairAccepted &&
                 counterRaised &&
                 counterAccepted;
        Console.WriteLine(
            $"DIPLOMACY_AI turtleNap={turtleNap} " +
            $"aggressiveNapRejected={aggressiveNapRejected} " +
            $"fairAccepted={fairAccepted} counterRaised={counterRaised} " +
            $"counterAccepted={counterAccepted} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunDiplomacyNegotiationDemo()
    {
        var world = AiWorld();
        var engine = new SimulationEngine(world);
        var profile = OpponentDiplomacyProfiles.Cautious;

        AdvanceToTick(engine, world, 240);
        var napProposal = AssertSinglePolicyCommand(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                profile));
        var proactiveNap = napProposal is ProposeTreatyCommand
        {
            Kind: TreatyKind.NonAggression,
        };
        engine.ExecuteDiplomacyCommand(napProposal);
        engine.ExecuteDiplomacyCommand(new RespondTreatyCommand(
            "A",
            world.TreatyProposals.Values.Single().Id,
            Accept: true));
        var napActive = world.Treaties.Values.Any(treaty =>
            treaty.Kind == TreatyKind.NonAggression);

        AdvanceToTick(engine, world, 480);
        var tradeProposal = AssertSinglePolicyCommand(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                profile));
        var proactiveTrade = tradeProposal is ProposeTreatyCommand
        {
            Kind: TreatyKind.Trade,
        };
        engine.ExecuteDiplomacyCommand(tradeProposal);
        engine.ExecuteDiplomacyCommand(new RespondTreatyCommand(
            "A",
            world.TreatyProposals.Values.Single().Id,
            Accept: true));
        var tradeActive = world.Treaties.Values.Any(treaty =>
            treaty.Kind == TreatyKind.Trade);

        var proactiveOfferCommand = AssertSinglePolicyCommand(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                profile));
        var proactiveOffer = proactiveOfferCommand is ProposeTradeCommand
        {
            GiveMaterial: 20,
            WantMaterial: 20,
        };
        var offerResult = engine.ExecuteDiplomacyCommand(proactiveOfferCommand);

        var counter = engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand(
                "A",
                offerResult.TradeOfferId!.Value,
                21,
                0,
                20,
                0,
                50));
        var counterOffer = world.TradeOffers.Values.Single();
        var counterRounds = counterOffer.Round;
        var acceptCounter = AssertSinglePolicyCommand(
            OpponentDiplomacyPolicy.Decide(
                world,
                "B",
                "A",
                canCommunicate: true,
                profile));
        var counterAccepted = acceptCounter is RespondTradeCommand { Accept: true } &&
                              engine.ExecuteDiplomacyCommand(acceptCounter).Success;

        var ok = proactiveNap &&
                 napActive &&
                 proactiveTrade &&
                 tradeActive &&
                 proactiveOffer &&
                 counter.Success &&
                 counterRounds == 2 &&
                 counterAccepted;
        Console.WriteLine(
            $"DIPLOMACY_NEGOTIATION proactiveNap={proactiveNap} napActive={napActive} " +
            $"proactiveTrade={proactiveTrade} tradeActive={tradeActive} " +
            $"proactiveOffer={proactiveOffer} rounds={counterRounds} " +
            $"counterAccepted={counterAccepted} ok={ok}");

        return ok ? 0 : 1;
    }

}
