using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class ControllerSlotTests
{
    [Fact]
    public void SuccessfulDeploySwapsBehaviorAndIncrementsVersion()
    {
        using var slot = new ControllerSlot("A", Script("wait"));
        var observation = CreateObservation();

        Assert.IsType<WaitIntent>(Assert.Single(slot.Decide(observation).Intents));
        Assert.Equal(1, slot.Version);

        var result = slot.Deploy(Script("move"));

        Assert.True(result.Success);
        Assert.Equal(2, result.Version);
        Assert.Equal(2, slot.Version);
        Assert.IsType<MoveIntent>(Assert.Single(slot.Decide(observation).Intents));
    }

    [Fact]
    public void FailedDeployKeepsPreviousControllerAndVersion()
    {
        using var slot = new ControllerSlot("A", Script("wait"));
        var observation = CreateObservation();

        var result = slot.Deploy("var notADecideFunction = 1;");

        Assert.False(result.Success);
        Assert.Equal(1, result.Version);
        Assert.Equal(1, slot.Version);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.IsType<WaitIntent>(Assert.Single(slot.Decide(observation).Intents));
    }

    [Fact]
    public void DeployedScriptIsRetainedForSaveAndRedisplay()
    {
        const string source = "function decide(obs) { return [{ type: \"wait\", entityId: 1 }]; }";
        using var slot = new ControllerSlot("A", source);
        var deployment = slot.Deploy(Script("move"));

        Assert.True(deployment.Success);
        Assert.Contains("\"move\"", slot.DeployedScript);
    }

    private static string Script(string type) =>
        $$"""
        function decide(obs) {
            return [{ type: "{{type}}", entityId: 1, target: { x: 500, y: 500 } }];
        }
        """;

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
                Material: 0))
            .Build();

        return new SimulationEngine(world).BuildObservation("A");
    }
}
