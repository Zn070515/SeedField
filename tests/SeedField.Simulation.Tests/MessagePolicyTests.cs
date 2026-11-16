using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class MessagePolicyTests
{
    [Fact]
    public void TargetCanBlockASender()
    {
        var world = WorldWithPolicy(
            allowAll: true,
            blockedCivilizations: new[] { "A" });
        var result = Send(world);

        Assert.Equal(SimulationErrorCode.MessageDenied, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Empty(world.GetInbox("B"));
    }

    [Fact]
    public void TargetCanBlockATopic()
    {
        var world = WorldWithPolicy(
            allowAll: true,
            blockedTopics: new[] { "diplomacy" });
        var result = Send(world);

        Assert.Equal(SimulationErrorCode.MessageDenied, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Empty(world.GetInbox("B"));
    }

    [Fact]
    public void TargetCanCloseAllInbound()
    {
        var world = WorldWithPolicy(allowAll: false);
        var result = Send(world);

        Assert.Equal(SimulationErrorCode.MessageDenied, Assert.Single(result.IntentResults).ErrorCode);
    }

    [Fact]
    public void MessagePolicySurvivesSaveLoad()
    {
        var world = WorldWithPolicy(
            allowAll: true,
            blockedTopics: new[] { "diplomacy" });
        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.True(loaded.MessagePolicies.ContainsKey("B"));
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(
            SimulationErrorCode.MessageDenied,
            Assert.Single(Send(loaded).IntentResults).ErrorCode);
    }

    private static TickResult Send(WorldState world) =>
        new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "B", "diplomacy", "hello"),
        });

    private static WorldState WorldWithPolicy(
        bool allowAll,
        string[]? blockedCivilizations = null,
        string[]? blockedTopics = null) =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddMessagePolicy("B", allowAll, blockedCivilizations, blockedTopics)
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
