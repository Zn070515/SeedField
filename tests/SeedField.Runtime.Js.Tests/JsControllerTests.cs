using System.Diagnostics;
using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class JsControllerTests
{
    [Fact]
    public void ScriptCanReturnAMoveIntent()
    {
        var observation = CreateObservation();
        using var controller = new JsController(
            "A",
            """
            function decide(obs) {
                return [{ type: "move", entityId: 1, target: { x: 900, y: 400 } }];
            }
            """);

        var decision = controller.Decide(observation);

        Assert.False(decision.HasFaults);
        var move = Assert.IsType<MoveIntent>(Assert.Single(decision.Intents));
        Assert.Equal(new EntityId(1), move.EntityId);
        Assert.Equal(new Vec2(900, 400), move.Target);
    }

    [Fact]
    public void ControllersWithTheSameScriptKeepSeparateGlobals()
    {
        const string script = """
            var calls = 0;
            function decide(obs) {
                calls++;
                return [{
                    type: "send",
                    entityId: 1,
                    toCivilizationId: "B",
                    topic: "count",
                    payload: String(calls)
                }];
            }
            """;
        var observation = CreateObservation();
        using var first = new JsController("A", script);
        using var second = new JsController("A", script);

        var firstRun = first.Decide(observation);
        var secondRun = second.Decide(observation);
        var firstAgain = first.Decide(observation);

        Assert.True(
            firstRun.Intents.Count == 1,
            string.Join(" | ", firstRun.Faults.Select(fault => $"{fault.Code}:{fault.Message}")));
        Assert.Equal(
            "1",
            Assert.IsType<SendMessageIntent>(Assert.Single(firstRun.Intents)).Payload);
        Assert.Equal(
            "1",
            Assert.IsType<SendMessageIntent>(Assert.Single(secondRun.Intents)).Payload);
        Assert.Equal(
            "2",
            Assert.IsType<SendMessageIntent>(Assert.Single(firstAgain.Intents)).Payload);
    }

    [Fact]
    public void RejectsObservationAboveByteLimitBeforeParsing()
    {
        using var controller = new JsController(
            "A",
            "function decide(obs) { return []; }");
        var oversized = new string('x', JsController.MaxObservationBytes + 1);

        var result = controller.RunJson(oversized);

        Assert.False(result.Success);
        Assert.Equal(ScriptFaultCodes.Fault, result.FaultCode);
    }

    [Fact]
    public void ScriptReceivesOnlyTheFilteredObservation()
    {
        var observation = CreateObservation();
        using var controller = new JsController(
            "A",
            """
            function decide(obs) {
                var self = obs.entities.filter(function (e) { return e.isSelf; })[0];
                return [{
                    type: (self && self.entityId === 1 && obs.civilizationId === "A") ? "wait" : "attack",
                    entityId: 1,
                    targetId: 2
                }];
            }
            """);

        var decision = controller.Decide(observation);

        Assert.False(decision.HasFaults);
        Assert.IsType<WaitIntent>(Assert.Single(decision.Intents));
    }

    [Fact]
    public void InfiniteLoopIsStoppedAndReturnsNoIntent()
    {
        var observation = CreateObservation();
        using var controller = new JsController(
            "A",
            "function decide(obs) { while (true) { } }",
            new JsControllerOptions { MaxStatements = 5_000 });

        var stopwatch = Stopwatch.StartNew();
        var decision = controller.Decide(observation);
        stopwatch.Stop();

        Assert.Empty(decision.Intents);
        Assert.True(decision.HasFaults);
        Assert.Contains(
            decision.Faults,
            fault => fault.Code is ScriptFaultCodes.StatementLimit or ScriptFaultCodes.Timeout);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ScriptCannotReachClrOrHostObjects()
    {
        var observation = CreateObservation();
        using var controller = new JsController(
            "A",
            """
            function decide(obs) {
                var leaked = (typeof System !== "undefined")
                    || (typeof require !== "undefined")
                    || (typeof process !== "undefined")
                    || (typeof module !== "undefined")
                    || (typeof globalThis.DotNet !== "undefined");
                return [{
                    type: leaked ? "attack" : "move",
                    entityId: 1,
                    target: { x: 200, y: 200 },
                    targetId: 2
                }];
            }
            """);

        var decision = controller.Decide(observation);

        Assert.False(decision.HasFaults);
        Assert.IsType<MoveIntent>(Assert.Single(decision.Intents));
    }

    [Fact]
    public void EvalIsRejectedByStringCompilationPolicy()
    {
        var observation = CreateObservation();
        using var controller = new JsController(
            "A",
            """
            function decide(obs) {
                var value = eval("1 + 1");
                return [{ type: "wait", entityId: 1 }];
            }
            """);

        var decision = controller.Decide(observation);

        Assert.Empty(decision.Intents);
        Assert.Contains(decision.Faults, fault => fault.Code == ScriptFaultCodes.Fault);
    }

    [Fact]
    public void MalformedIntentItemsAreReportedWithoutDroppingValidOnes()
    {
        var observation = CreateObservation();
        using var controller = new JsController(
            "A",
            """
            function decide(obs) {
                return [
                    { type: "teleport", entityId: 1 },
                    { type: "wait", entityId: 1 }
                ];
            }
            """);

        var decision = controller.Decide(observation);

        Assert.IsType<WaitIntent>(Assert.Single(decision.Intents));
        var fault = Assert.Single(decision.Faults);
        Assert.Equal(ScriptFaultCodes.InvalidIntentSchema, fault.Code);
    }

    [Fact]
    public void ObservationExposesDiplomacyStateToScripts()
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
                Drive: new DriveModule(100, EnergyCostPerMove: 1),
                Comm: new CommModule(1, EnergyCostPerMessage: 0)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(200, 200),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Comm: new CommModule(1, EnergyCostPerMessage: 0)))
            .Build();
        var engine = new SimulationEngine(world);
        var proposed = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 5));
        engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", proposed.ProposalId!.Value, Accept: true));

        using var controller = new JsController(
            "A",
            """
            function decide(obs) {
                var ok = obs.treaties.length === 1
                    && obs.treaties[0].kind === "NonAggression"
                    && obs.treaties[0].otherCivilizationId === "B";
                return [{
                    type: ok ? "wait" : "move",
                    entityId: 1,
                    target: { x: 900, y: 900 }
                }];
            }
            """);

        var decision = controller.Decide(engine.BuildObservation("A"));

        Assert.IsType<WaitIntent>(Assert.Single(decision.Intents));
    }

    [Fact]
    public void ScriptWithoutDecideFailsToLoad()
    {
        var exception = Assert.Throws<JsControllerLoadException>(() =>
            new JsController("A", "var x = 1;"));

        Assert.Contains("decide", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Observation CreateObservation()
    {
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
                Sensor: new SensorModule(5_000),
                Weapon: new WeaponModule(5_000, Damage: 5, EnergyCost: 1, CooldownTicks: 1),
                Drive: new DriveModule(100, EnergyCostPerMove: 1),
                Comm: new CommModule(1, EnergyCostPerMessage: 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(1_000, 1_000),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0))
            .Build();

        return new SimulationEngine(world).BuildObservation("A");
    }
}
