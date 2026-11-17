using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Headless;

/// <summary>
/// M0 headless vertical slice:
/// two civilizations, real Jint controllers, local energy, sensing, messaging,
/// direct-fire combat and a reproducible state hash. Nothing here is a balance
/// decision; all numbers are scenario-local demo values.
/// </summary>
internal static class Program
{
    private const int Ticks = 600;

    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Contains("--multi-region"))
        {
            return RunMultiRegionDemo();
        }

        if (args.Contains("--logistics"))
        {
            return RunLogisticsDemo();
        }

        if (args.Contains("--energy"))
        {
            return RunEnergyDemo();
        }

        if (args.Contains("--region-load"))
        {
            return RunRegionLoadDemo();
        }

        if (args.Contains("--construction"))
        {
            return RunConstructionDemo();
        }

        if (args.Contains("--construction-cancel"))
        {
            return RunConstructionCancelDemo();
        }

        if (args.Contains("--message-acl"))
        {
            return RunMessageAclDemo();
        }

        if (args.Contains("--diplomacy"))
        {
            return RunDiplomacyDemo();
        }

        if (args.Contains("--diplomacy-ai"))
        {
            return RunDiplomacyAiDemo();
        }

        if (args.Contains("--diplomacy-negotiation"))
        {
            return RunDiplomacyNegotiationDemo();
        }

        if (args.Contains("--power-grid"))
        {
            return RunPowerGridDemo();
        }

        if (args.Contains("--power-recovery"))
        {
            return RunPowerRecoveryDemo();
        }

        var first = RunScenario();
        var second = RunScenario();

        PrintReport(first);

        Console.WriteLine();
        Console.WriteLine($"Run 1 final hash: {first.FinalHash:x16}");
        Console.WriteLine($"Run 2 final hash: {second.FinalHash:x16}");
        Console.WriteLine(first.FinalHash == second.FinalHash
            ? "REPRODUCIBLE: identical state hash across both runs."
            : "MISMATCH: state hashes differ.");

        return first.FinalHash == second.FinalHash ? 0 : 1;
    }

    private static int RunMultiRegionDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 1_000, 1_000)
            .AddRegion("r1", 1_000, 1_000)
            .AddRegion("r2", 1_000, 1_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 2)
            .AddLink("l12", "r1", "r2", latencyTicks: 3)
            .AddEntity(CommsEndpoint(1, "r0"))
            .AddEntity(CommsEndpoint(2, "r1"))
            .AddEntity(CommsEndpoint(3, "r2"))
            .Build();

        var engine = new SimulationEngine(world);
        var send = engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "route", "payload", "r2"),
        });

        var queued = send.Events.OfType<MessageQueued>().Single();
        Console.WriteLine(
            $"MULTI_REGION queued id={queued.MessageId} sentTick={queued.Tick} " +
            $"deliverTick={queued.DeliverTick} target={queued.TargetRegionId} route={queued.Route}");

        while (world.Tick < queued.DeliverTick)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var message = world.GetInbox("A").Single();
        var ok = message.TargetRegionId == "r2" &&
                 message.DeliveredTick == 6 &&
                 queued.Route == "l01>l12";
        Console.WriteLine(
            $"MULTI_REGION delivered id={message.Id} tick={message.DeliveredTick} " +
            $"target={message.TargetRegionId} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunLogisticsDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 1_000, 1_000)
            .AddRegion("r1", 1_000, 1_000)
            .AddRegion("r2", 1_000, 1_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 2)
            .AddLink("l12", "r1", "r2", latencyTicks: 3)
            .AddStock("r0", "A", 100)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Drive: new DriveModule(50, EnergyCostPerMove: 1)))
            .Build();

        var engine = new SimulationEngine(world);
        var result = engine.Step(new Intent[]
        {
            new TransferMaterialIntent(new EntityId(1), "r2", 40),
        });

        var shipped = result.Events.OfType<MaterialShipped>().Single();
        Console.WriteLine(
            $"LOGISTICS shipped amount={shipped.Amount} arriveTick={shipped.ArriveTick} " +
            $"route={shipped.Route} source={world.Regions["r0"].GetMaterial("A")} " +
            $"target={world.Regions["r2"].GetMaterial("A")}");

        while (world.Tick < shipped.ArriveTick)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var source = world.Regions["r0"].GetMaterial("A");
        var target = world.Regions["r2"].GetMaterial("A");
        var ok = source == 60 &&
                 target == 40 &&
                 shipped.Route == "l01>l12" &&
                 world.InTransitMaterial.Count == 0;
        Console.WriteLine(
            $"LOGISTICS arrived tick={world.Tick} source={source} target={target} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunEnergyDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 1_000, 1_000)
            .AddRegion("r1", 1_000, 1_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 3)
            .AddEntity(EnergyNode(1, "r0", energy: 100))
            .AddEntity(EnergyNode(2, "r1", energy: 0))
            .Build();

        var engine = new SimulationEngine(world);
        var shipped = engine.Step(new Intent[]
        {
            new SendEnergyIntent(new EntityId(1), 40, "r1"),
        }).Events.OfType<EnergyShipped>().Single();

        Console.WriteLine(
            $"ENERGY shipped amount={shipped.Amount} arriveTick={shipped.ArriveTick} " +
            $"route={shipped.Route} sourceLocal={world.FindEntity(new EntityId(1))!.Energy} " +
            $"targetGrid={world.Regions["r1"].GetEnergy("A")}");

        while (world.Tick < shipped.ArriveTick)
        {
            engine.Step(Array.Empty<Intent>());
        }

        engine.Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(2), 25),
        });

        var sourceLocal = world.FindEntity(new EntityId(1))!.Energy;
        var targetLocal = world.FindEntity(new EntityId(2))!.Energy;
        var targetGrid = world.Regions["r1"].GetEnergy("A");
        var ok = sourceLocal == 60 && targetLocal == 25 && targetGrid == 15;
        Console.WriteLine(
            $"ENERGY drew targetLocal={targetLocal} targetGrid={targetGrid} " +
            $"sourceLocal={sourceLocal} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunRegionLoadDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Drive: new DriveModule(1_000, EnergyCostPerMove: 1)))
            .Build();

        var engine = new SimulationEngine(world);
        engine.DeactivateRegion("r0");

        var rejected = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });
        var inactiveCode = rejected.IntentResults[0].ErrorCode;

        engine.ActivateRegion("r0");
        var accepted = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });

        var position = world.FindEntity(new EntityId(1))!.Position;
        var ok = inactiveCode == SimulationErrorCode.RegionInactive &&
                 accepted.IntentResults[0].Accepted &&
                 position == new Vec2(500, 500);

        Console.WriteLine(
            $"REGION_LOAD inactive={inactiveCode} activeAccepted={accepted.IntentResults[0].Accepted} " +
            $"position={position} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunConstructionDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "fabricator",
            0,
            "Fabricator",
            20,
            10,
            Power: new PowerModule(1, 10),
            MaterialCost: 20,
            BuildTicks: 3));
        var engine = new SimulationEngine(world);

        var result = engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            blueprint,
            "r0",
            new Vec2(100, 100)));

        Console.WriteLine(
            $"CONSTRUCTION queued={result.Success} entity={result.EntityId} " +
            $"orders={world.ConstructionOrders.Count} material={world.Regions["r0"].GetMaterial("A")}");

        while (world.ConstructionOrders.Count > 0)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var entity = world.EnumerateEntities().SingleOrDefault();
        var ok = result.Success &&
                 result.EntityId is null &&
                 entity is not null &&
                 world.Regions["r0"].GetMaterial("A") == 80;
        Console.WriteLine(
            $"CONSTRUCTION completed tick={world.Tick} entity={entity?.Id} " +
            $"material={world.Regions["r0"].GetMaterial("A")} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunConstructionCancelDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "fabricator",
            0,
            "Fabricator",
            20,
            10,
            Power: new PowerModule(1, 10),
            MaterialCost: 20,
            BuildTicks: 3));
        var engine = new SimulationEngine(world);
        engine.ExecuteDeploymentCommand(new DeployCommand("A", blueprint, "r0", new Vec2(100, 100)));
        var orderId = world.ConstructionOrders.Single().Id;

        var result = engine.ExecuteDeploymentCommand(new CancelConstructionCommand("A", orderId));

        var ok = result.Success &&
                 world.ConstructionOrders.Count == 0 &&
                 world.Regions["r0"].GetMaterial("A") == 100 &&
                 world.Statistics.MaterialRefundedTotal == 20;
        Console.WriteLine(
            $"CONSTRUCTION_CANCEL ok={ok} orders={world.ConstructionOrders.Count} " +
            $"material={world.Regions["r0"].GetMaterial("A")} " +
            $"refunded={world.Statistics.MaterialRefundedTotal}");

        return ok ? 0 : 1;
    }

    private static int RunMessageAclDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddEntity(Sender(1, "A", "r0"))
            .AddEntity(Sender(2, "B", "r0"))
            .AddEntity(Sender(3, "C", "r0"))
            .AddEntity(Sender(4, "D", "r1"))
            .AddMessagePolicy(
                "B",
                allowAllInbound: false,
                allowedCivilizations: new[] { "A" },
                allowAllTopics: false,
                allowedTopics: new[] { "diplomacy" })
            .AddMessagePolicy(
                "C",
                allowAllInbound: false,
                allowedCivilizations: new[] { "D" })
            .Build();
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "B", "diplomacy", "hello"),
            new SendMessageIntent(new EntityId(1), "B", "trade", "offer"),
            new SendMessageIntent(new EntityId(1), "C", "diplomacy", "hello"),
            new SendMessageIntent(new EntityId(1), "D", "diplomacy", "hello", "r1"),
        });

        var codes = result.IntentResults.Select(r => r.ErrorCode).ToArray();
        var senderAllowed = codes[0] == SimulationErrorCode.None;
        var senderDenied = codes[2] == SimulationErrorCode.MessageDenied;
        var topicAllowed = codes[0] == SimulationErrorCode.None;
        var topicDenied = codes[1] == SimulationErrorCode.MessageDenied;
        var noRoute = codes[3] == SimulationErrorCode.NoRoute;
        var energy = world.FindEntity(new EntityId(1))!.Energy;

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var hashOk = WorldStateHasher.Compute(world) == WorldStateHasher.Compute(loaded);

        var ok = senderAllowed &&
                 senderDenied &&
                 topicAllowed &&
                 topicDenied &&
                 noRoute &&
                 energy == 99 &&
                 world.GetInbox("B").Count == 1 &&
                 world.GetInbox("C").Count == 0 &&
                 hashOk;

        Console.WriteLine(
            $"MESSAGE_ACL senderAllowed={senderAllowed} senderDenied={senderDenied} " +
            $"topicAllowed={topicAllowed} topicDenied={topicDenied} noRoute={noRoute} " +
            $"energy={energy} inboxB={world.GetInbox("B").Count} " +
            $"inboxC={world.GetInbox("C").Count} hashOk={hashOk} ok={ok}");

        return ok ? 0 : 1;
    }

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

    private static int RunPowerGridDemo()
    {
        var flowWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddRegion("r2", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: 100)
            .AddLink("l12", "r1", "r2", latencyTicks: 1, powerPerTick: 100)
            .Build();
        flowWorld.Regions["r0"].AddEnergy("A", 90);
        new SimulationEngine(flowWorld).Step(Array.Empty<Intent>());

        var balanced =
            flowWorld.Regions["r0"].GetEnergy("A") == 30 &&
            flowWorld.Regions["r1"].GetEnergy("A") == 30 &&
            flowWorld.Regions["r2"].GetEnergy("A") == 30 &&
            flowWorld.Statistics.EnergyFlowedTotal == 60;
        var flowDiagnostics = PowerGridDiagnosticsBuilder.Build(flowWorld, "A");

        var blackoutWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: 100)
            .AddEntity(EnergyNode(1, "r0", 0))
            .Build();
        var blackoutDiagnostics = PowerGridDiagnosticsBuilder.Build(blackoutWorld, "A");
        var blackout = blackoutDiagnostics.Regions.All(region => region.IsBlackedOut);
        var shortfallStep = new SimulationEngine(blackoutWorld).Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 10),
        });
        var shortfall = shortfallStep.Events
            .OfType<PowerShortfall>()
            .Any(item => item.Source == "grid");

        var ok = balanced && blackout && shortfall;
        Console.WriteLine(
            $"POWER_GRID balanced={balanced} flowed={flowWorld.Statistics.EnergyFlowedTotal} " +
            $"grid={flowDiagnostics.TotalGridEnergy} " +
            $"regions={flowDiagnostics.Regions.Count} " +
            $"components={PowerGridTopology.BuildComponents(flowWorld).Count} " +
            $"blackout={blackout} shortfall={shortfall} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunPowerRecoveryDemo()
    {
        var overflowWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(PowerNode(1, "r0", energy: 9, capacity: 10, generation: 2))
            .Build();
        new SimulationEngine(overflowWorld).Step(Array.Empty<Intent>());
        var overflow = overflowWorld.Statistics.EnergyOverflowedTotal == 1 &&
                       overflowWorld.Regions["r0"].GetEnergy("A") == 1;

        var drawWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(PowerNode(
                1,
                "r0",
                energy: 0,
                capacity: 100,
                generation: 0,
                driveCost: 5))
            .Build();
        drawWorld.Regions["r0"].AddEnergy("A", 20);
        var drawStep = new SimulationEngine(drawWorld).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });
        var autoDraw = drawStep.IntentResults[0].Accepted &&
                       drawWorld.Statistics.EnergyAutoDrawnTotal == 5 &&
                       drawWorld.Regions["r0"].GetEnergy("A") == 15;

        var reserveWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 10)
            .AddEntity(PowerNode(
                1,
                "r0",
                energy: 0,
                capacity: 100,
                generation: 0,
                driveCost: 5))
            .Build();
        reserveWorld.Regions["r0"].AddEnergy("A", 12);
        var reserveStep = new SimulationEngine(reserveWorld).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });
        var reserveShed =
            reserveStep.IntentResults[0].ErrorCode == SimulationErrorCode.NoEnergy &&
            reserveStep.Events.OfType<PowerShortfall>()
                .Any(shortfall => shortfall.Source == "reserve");

        var transitWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink(
                "l01",
                "r0",
                "r1",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLatencyTicks: 2)
            .Build();
        transitWorld.Regions["r0"].AddEnergy("A", 90);
        var transitEngine = new SimulationEngine(transitWorld);
        transitEngine.Step(Array.Empty<Intent>());
        var latency = transitWorld.InTransitPower.Count == 1 &&
                      transitWorld.Regions["r0"].GetEnergy("A") == 45 &&
                      transitWorld.Regions["r1"].GetEnergy("A") == 0;
        transitEngine.Step(Array.Empty<Intent>());
        var latencyArrived = transitWorld.InTransitPower.Count == 0 &&
                             transitWorld.Regions["r1"].GetEnergy("A") == 45;

        var lossWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink(
                "l01",
                "r0",
                "r1",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLossPercent: 10)
            .Build();
        lossWorld.Regions["r0"].AddEnergy("A", 90);
        new SimulationEngine(lossWorld).Step(Array.Empty<Intent>());
        var lossArrived = lossWorld.Regions["r0"].GetEnergy("A") == 45 &&
                          lossWorld.Regions["r1"].GetEnergy("A") == 40 &&
                          lossWorld.Statistics.EnergyFlowedTotal == 40 &&
                          lossWorld.Statistics.EnergyFlowLossTotal == 5;

        var ok = overflow &&
                 autoDraw &&
                 reserveShed &&
                 latency &&
                 latencyArrived &&
                 lossArrived;
        Console.WriteLine(
            $"POWER_RECOVERY overflow={overflow} autoDraw={autoDraw} " +
            $"reserveShed={reserveShed} latency={latency} " +
            $"latencyArrived={latencyArrived} lossArrived={lossArrived} ok={ok}");

        return ok ? 0 : 1;
    }

    private static EntityDefinition PowerNode(
        long id,
        string regionId,
        long energy,
        long capacity,
        int generation,
        long driveCost = 0) =>
        new(
            new EntityId(id),
            "A",
            regionId,
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: energy,
            EnergyCapacity: capacity,
            Material: 0,
            Drive: driveCost > 0 ? new DriveModule(100, driveCost) : null,
            Power: new PowerModule(generation, capacity));

    private static void AdvanceToTick(
        SimulationEngine engine,
        WorldState world,
        long tick)
    {
        while (world.Tick < tick)
        {
            engine.Step(Array.Empty<Intent>());
        }
    }

    private static DiplomacyCommand AssertSinglePolicyCommand(
        IReadOnlyList<DiplomacyCommand> commands) =>
        commands.Count == 1
            ? commands[0]
            : throw new InvalidOperationException(
                $"Expected one diplomacy command, got {commands.Count}.");

    private static WorldState AiWorld() =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(DiplomacyUnit(1, "A", 100))
            .AddEntity(DiplomacyUnit(2, "B", 200))
            .AddStock("r0", "A", 100)
            .AddStock("r0", "B", 100)
            .Build();

    private static EntityDefinition DiplomacyUnit(
        long id,
        string civilizationId,
        int x) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            new Vec2(x, 100),
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Sensor: new SensorModule(5_000),
            Weapon: new WeaponModule(1_000, Damage: 3, EnergyCost: 5, CooldownTicks: 2),
            Comm: new CommModule(1, EnergyCostPerMessage: 0));

    private static EntityDefinition Sender(long id, string civilizationId, string regionId) =>
        new(
            new EntityId(id),
            civilizationId,
            regionId,
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Comm: new CommModule(1, EnergyCostPerMessage: 1));

    private static EntityDefinition EnergyNode(long id, string regionId, long energy) =>
        new(
            new EntityId(id),
            "A",
            regionId,
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: energy,
            EnergyCapacity: 100,
            Material: 0,
            Power: new PowerModule(EnergyPerTick: 0, StorageCapacity: 100));

    private static EntityDefinition CommsEndpoint(long id, string regionId) =>
        new(
            new EntityId(id),
            "A",
            regionId,
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 10,
            EnergyCapacity: 10,
            Material: 0,
            Comm: new CommModule(1, EnergyCostPerMessage: 0));

    private static RunReport RunScenario()
    {
        var world = BuildWorld();
        var engine = new SimulationEngine(world);

        using var controllerA = new JsController("A", ScriptA);
        using var controllerB = new JsController("B", ScriptB);

        var controllers = new (string Civilization, JsController Controller)[]
        {
            ("A", controllerA),
            ("B", controllerB),
        };

        var eventCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var rejectionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var sampleTimeline = new List<string>();
        var faults = new List<string>();

        for (var tick = 0; tick < Ticks; tick++)
        {
            var intents = new List<Intent>();

            foreach (var (civilization, controller) in controllers)
            {
                var observation = engine.BuildObservation(civilization);
                var decision = controller.Decide(observation);
                intents.AddRange(decision.Intents);

                foreach (var fault in decision.Faults)
                {
                    faults.Add($"tick {tick} civ {civilization}: {fault.Code} {fault.Message}");
                }
            }

            var result = engine.Step(intents);

            foreach (var simulationEvent in result.Events)
            {
                var name = simulationEvent.GetType().Name;
                eventCounts[name] = eventCounts.GetValueOrDefault(name) + 1;

                if (simulationEvent is IntentRejected rejected)
                {
                    var code = rejected.ErrorCode.ToString();
                    rejectionCounts[code] = rejectionCounts.GetValueOrDefault(code) + 1;
                }

                if (sampleTimeline.Count < 24 && simulationEvent is not IntentRejected)
                {
                    sampleTimeline.Add($"  t{simulationEvent.Tick,4}  {Describe(simulationEvent)}");
                }
            }
        }

        return new RunReport(
            WorldStateHasher.Compute(world),
            world,
            eventCounts,
            rejectionCounts,
            sampleTimeline,
            faults);
    }

    private static WorldState BuildWorld()
    {
        return new WorldBuilder()
            .AddRegion("frontier", 20_000, 20_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "frontier",
                new Vec2(1_000, 1_000),
                MaxHealth: 120,
                Energy: 5_000,
                EnergyCapacity: 5_000,
                Material: 0,
                Sensor: new SensorModule(7_000),
                Drive: new DriveModule(120, EnergyCostPerMove: 1),
                Comm: new CommModule(1, EnergyCostPerMessage: 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "frontier",
                new Vec2(2_000, 1_000),
                MaxHealth: 150,
                Energy: 5_000,
                EnergyCapacity: 5_000,
                Material: 0,
                Sensor: new SensorModule(6_000),
                Weapon: new WeaponModule(5_000, Damage: 8, EnergyCost: 2, CooldownTicks: 2),
                Drive: new DriveModule(90, EnergyCostPerMove: 1),
                Comm: new CommModule(1, EnergyCostPerMessage: 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(3),
                "B",
                "frontier",
                new Vec2(14_000, 10_000),
                MaxHealth: 150,
                Energy: 5_000,
                EnergyCapacity: 5_000,
                Material: 0,
                Sensor: new SensorModule(6_000),
                Weapon: new WeaponModule(5_000, Damage: 8, EnergyCost: 2, CooldownTicks: 2),
                Drive: new DriveModule(90, EnergyCostPerMove: 1),
                Comm: new CommModule(1, EnergyCostPerMessage: 1)))
            .Build();
    }

    private static string Describe(SimulationEvent simulationEvent) =>
        simulationEvent switch
        {
            EntityMoved moved => $"entity {moved.EntityId} moved {moved.From} -> {moved.To} (-{moved.EnergySpent}E)",
            ShotFired shot => $"entity {shot.AttackerId} hit {shot.TargetId} for {shot.Damage} (-{shot.EnergySpent}E)",
            EntityDamaged damaged => $"entity {damaged.EntityId} health {damaged.HealthRemaining}",
            EntityDestroyed destroyed => $"entity {destroyed.EntityId} destroyed",
            MessageQueued queued => $"message {queued.MessageId} queued by {queued.FromEntityId} for t{queued.DeliverTick}",
            MessageDelivered delivered => $"message {delivered.MessageId} delivered to {delivered.ToCivilizationId}",
            CoreRegenerated core => $"core {core.CivilizationId} +{core.EnergyAdded}E +{core.MaterialAdded}M",
            _ => simulationEvent.GetType().Name,
        };

    private static void PrintReport(RunReport report)
    {
        Console.WriteLine("SeedField M0 headless slice");
        Console.WriteLine($"ticks: {Ticks}");
        Console.WriteLine(
            $"final: tick={report.World.Tick} entities={report.World.EnumerateEntities().Count()} " +
            $"energySpent={report.World.Statistics.EnergySpentTotal} " +
            $"shots={report.World.Statistics.ShotsFired} " +
            $"damage={report.World.Statistics.DamageDealt} " +
            $"destroyed={report.World.Statistics.EntitiesDestroyed} " +
            $"messagesDelivered={report.World.Statistics.MessagesDelivered}");
        Console.WriteLine($"controller faults: {report.Faults.Count}");

        Console.WriteLine("event counts:");
        foreach (var pair in report.EventCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {pair.Key}: {pair.Value}");
        }

        Console.WriteLine("intent rejection reasons:");
        foreach (var pair in report.RejectionCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {pair.Key}: {pair.Value}");
        }

        Console.WriteLine("sample timeline:");
        foreach (var line in report.SampleTimeline)
        {
            Console.WriteLine(line);
        }

        if (report.Faults.Count > 0)
        {
            Console.WriteLine("faults:");
            foreach (var fault in report.Faults.Take(10))
            {
                Console.WriteLine($"  {fault}");
            }
        }
    }

    private sealed record RunReport(
        ulong FinalHash,
        WorldState World,
        Dictionary<string, int> EventCounts,
        Dictionary<string, int> RejectionCounts,
        List<string> SampleTimeline,
        List<string> Faults);

    // Deliberately plain scenario scripts. They use only the documented observation
    // and intent schema; they receive no CLR objects and no host APIs.
    private const string ScriptA = """
        function decide(obs) {
            var intents = [];
            var self = obs.entities.filter(function (e) { return e.isSelf; });
            var enemies = obs.entities.filter(function (e) { return !e.isSelf; });

            var scout = self.filter(function (e) { return e.entityId === 1; })[0];
            var gunner = self.filter(function (e) { return e.entityId === 2; })[0];

            if (scout) {
                if (enemies.length > 0) {
                    var seen = enemies[0];
                    intents.push({
                        type: "send",
                        entityId: 1,
                        toCivilizationId: "A",
                        topic: "contact",
                        payload: JSON.stringify({ x: seen.x, y: seen.y })
                    });
                    intents.push({
                        type: "move",
                        entityId: 1,
                        target: { x: seen.x, y: seen.y }
                    });
                } else {
                    intents.push({
                        type: "move",
                        entityId: 1,
                        target: { x: 12000, y: 10000 }
                    });
                }
            }

            if (gunner) {
                var target = nearest(gunner, enemies);
                if (target && distance(gunner, target) <= 5_000) {
                    intents.push({ type: "attack", entityId: 2, targetId: target.entityId });
                } else if (target) {
                    intents.push({
                        type: "move",
                        entityId: 2,
                        target: { x: target.x, y: target.y }
                    });
                } else {
                    var last = null;
                    for (var i = 0; i < obs.messages.length; i++) {
                        if (obs.messages[i].topic === "contact") {
                            last = obs.messages[i];
                        }
                    }
                    if (last) {
                        var p = JSON.parse(last.payload);
                        intents.push({ type: "move", entityId: 2, target: p });
                    }
                }
            }

            return intents;
        }

        function distance(a, b) {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return Math.sqrt(dx * dx + dy * dy);
        }

        function nearest(from, list) {
            var best = null;
            var bestDistance = 1e18;
            for (var i = 0; i < list.length; i++) {
                var d = distance(from, list[i]);
                if (d < bestDistance) {
                    bestDistance = d;
                    best = list[i];
                }
            }
            return best;
        }
        """;

    private const string ScriptB = """
        function decide(obs) {
            var self = obs.entities.filter(function (e) { return e.isSelf; })[0];
            if (!self) {
                return [];
            }

            var enemies = obs.entities.filter(function (e) { return !e.isSelf; });
            if (enemies.length === 0) {
                return [{ type: "move", entityId: 3, target: { x: 9000, y: 10000 } }];
            }

            var target = enemies[0];
            var dx = target.x - self.x;
            var dy = target.y - self.y;
            var distance = Math.sqrt(dx * dx + dy * dy);

            if (distance <= 5_000) {
                return [{ type: "attack", entityId: 3, targetId: target.entityId }];
            }

            return [{
                type: "move",
                entityId: 3,
                target: { x: target.x, y: target.y }
            }];
        }
        """;
}
