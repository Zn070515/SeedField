using SeedField.PackageHost;
using SeedField.Runtime.Js;
using SeedField.Runtime.Js.Worker;
using SeedField.Simulation;

namespace SeedField.Headless;

/// <summary>
/// M0 headless vertical slice:
/// two civilizations, real Jint controllers, local energy, sensing, messaging,
/// direct-fire combat and a reproducible state hash. Nothing here is a balance
/// decision; all numbers are scenario-local demo values.
/// </summary>
internal static partial class Program
{
    private const int Ticks = 600;

    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var observationBenchmarkIndex = Array.IndexOf(args, "--benchmark-observation");
        if (observationBenchmarkIndex >= 0 &&
            observationBenchmarkIndex + 1 < args.Length &&
            int.TryParse(args[observationBenchmarkIndex + 1], out var observationDevices))
        {
            return RunObservationBenchmark(observationDevices);
        }

        var serializeBenchmarkIndex = Array.IndexOf(args, "--benchmark-serialize");
        if (serializeBenchmarkIndex >= 0 &&
            serializeBenchmarkIndex + 1 < args.Length &&
            int.TryParse(args[serializeBenchmarkIndex + 1], out var serializeDevices))
        {
            return RunSerializeBenchmark(serializeDevices);
        }

        var jintBenchmarkIndex = Array.IndexOf(args, "--benchmark-jint");
        if (jintBenchmarkIndex >= 0 &&
            jintBenchmarkIndex + 1 < args.Length &&
            int.TryParse(args[jintBenchmarkIndex + 1], out var jintDevices))
        {
            return RunJintBenchmark(jintDevices);
        }

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

        if (args.Contains("--power-limits"))
        {
            return RunPowerLimitsDemo();
        }

        if (args.Contains("--power-breaker"))
        {
            return RunPowerBreakerDemo();
        }

        if (args.Contains("--power-maintenance"))
        {
            return RunPowerMaintenanceDemo();
        }

        if (args.Contains("--power-fault"))
        {
            return RunPowerFaultDemo();
        }

        if (args.Contains("--recovery-quota"))
        {
            return RunRecoveryQuotaDemo();
        }

        if (args.Contains("--core-siege"))
        {
            return RunCoreSiegeDemo();
        }

        if (args.Contains("--wrecks"))
        {
            return RunWreckDemo();
        }

        if (args.Contains("--worker-opponent"))
        {
            return RunWorkerOpponentDemo();
        }

        var packageCheckIndex = Array.IndexOf(args, "--package-check");
        if (packageCheckIndex >= 0)
        {
            return RunPackageCheck(args, packageCheckIndex);
        }

        if (args.Contains("--ruins"))
        {
            return RunRuinDemo();
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

    private static RunReport RunScenario(ControllerFactory? opponentFactory = null)
    {
        var world = BuildWorld();
        var engine = new SimulationEngine(world);

        using var controllerA = new JsController("A", ScriptA);
        using var controllerB = opponentFactory is null
            ? (IController)new JsController("B", ScriptB)
            : opponentFactory("B", ScriptB, null);

        var controllers = new (string Civilization, IController Controller)[]
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
