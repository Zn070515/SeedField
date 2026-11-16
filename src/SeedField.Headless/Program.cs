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
