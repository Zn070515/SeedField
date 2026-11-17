using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class IntentOwnershipTests
{
    [Fact]
    public void ControllerCannotCommandAnotherCivilizationsEntity()
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
                Material: 0))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(200, 200),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Drive: new DriveModule(100, EnergyCostPerMove: 1)))
            .Build();

        var engine = new SimulationEngine(world);
        using var controller = new JsController(
            "A",
            """
            function decide(obs) {
                return [{ type: "move", entityId: 2, target: { x: 5000, y: 5000 } }];
            }
            """);

        var decision = controller.Decide(engine.BuildObservation("A"));

        Assert.Equal("A", Assert.Single(decision.Intents).IssuerCivilizationId);

        var result = engine.Step(decision.Intents);

        Assert.Equal(
            SimulationErrorCode.UnknownEntity,
            Assert.Single(result.IntentResults).ErrorCode);
        Assert.Equal(new Vec2(200, 200), world.FindEntity(new EntityId(2))!.Position);
    }

    [Fact]
    public void ScriptCannotForgeTheMessageSender()
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
        using var controller = new JsController(
            "A",
            """
            function decide(obs) {
                return [{
                    type: "send",
                    entityId: 1,
                    toCivilizationId: "B",
                    topic: "contact",
                    payload: "hello",
                    fromCivilizationId: "B"
                }];
            }
            """);

        var decision = controller.Decide(engine.BuildObservation("A"));
        var intent = Assert.IsType<SendMessageIntent>(Assert.Single(decision.Intents));

        Assert.Equal("A", intent.IssuerCivilizationId);
        Assert.Null(typeof(SendMessageIntent).GetProperty("FromCivilizationId"));

        engine.Step(decision.Intents);

        var delivered = Assert.Single(world.GetInbox("B"));
        Assert.Equal("A", delivered.FromCivilizationId);
    }
}
