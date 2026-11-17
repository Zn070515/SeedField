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
    public void ClosedInboundStillAcceptsAListedSender()
    {
        var world = WorldWithPolicy(
            allowAll: false,
            allowedCivilizations: new[] { "A" });
        var result = Send(world);

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        Assert.Single(world.GetInbox("B"));
    }

    [Fact]
    public void ClosedInboundDeniesAnUnlistedSender()
    {
        var world = WorldWithPolicy(
            allowAll: false,
            allowedCivilizations: new[] { "C" });
        var result = Send(world);

        Assert.Equal(SimulationErrorCode.MessageDenied, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Empty(world.GetInbox("B"));
    }

    [Fact]
    public void BlockedSenderWinsOverAllowList()
    {
        var world = WorldWithPolicy(
            allowAll: false,
            blockedCivilizations: new[] { "A" },
            allowedCivilizations: new[] { "A" });
        var result = Send(world);

        Assert.Equal(SimulationErrorCode.MessageDenied, Assert.Single(result.IntentResults).ErrorCode);
    }

    [Fact]
    public void TopicAllowListAcceptsAListedTopic()
    {
        var world = WorldWithPolicy(
            allowAll: true,
            allowAllTopics: false,
            allowedTopics: new[] { "diplomacy" });
        var result = Send(world);

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        Assert.Single(world.GetInbox("B"));
    }

    [Fact]
    public void TopicAllowListDeniesAnUnlistedTopic()
    {
        var world = WorldWithPolicy(
            allowAll: true,
            allowAllTopics: false,
            allowedTopics: new[] { "trade" });
        var result = Send(world);

        Assert.Equal(SimulationErrorCode.MessageDenied, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Empty(world.GetInbox("B"));
    }

    [Fact]
    public void BlockedTopicWinsOverAllowList()
    {
        var world = WorldWithPolicy(
            allowAll: true,
            allowAllTopics: false,
            blockedTopics: new[] { "diplomacy" },
            allowedTopics: new[] { "diplomacy" });
        var result = Send(world);

        Assert.Equal(SimulationErrorCode.MessageDenied, Assert.Single(result.IntentResults).ErrorCode);
    }

    [Fact]
    public void SameCivilizationMessagesBypassInboundPolicy()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "B"))
            .AddEntity(Unit(2, "B"))
            .AddMessagePolicy("B", allowAllInbound: false)
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "B", "internal", "hello"),
        });

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        Assert.Single(world.GetInbox("B"));
    }

    [Fact]
    public void AllowListsSurviveSaveLoad()
    {
        var world = WorldWithPolicy(
            allowAll: false,
            allowedCivilizations: new[] { "A" },
            allowAllTopics: false,
            allowedTopics: new[] { "diplomacy" });

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.True(Assert.Single(Send(loaded).IntentResults).Accepted);
    }

    [Fact]
    public void ForgedSenderIssuerIsRejectedAsUnknownEntity()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "B", "contact", "hello")
            {
                IssuerCivilizationId = "B",
            },
        });

        Assert.Equal(SimulationErrorCode.UnknownEntity, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Empty(world.GetInbox("B"));
    }

    [Fact]
    public void DisconnectedRegionRejectsSendWithoutSpendingEnergy()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddEntity(Unit(1, "A", "r0", commEnergyCost: 5))
            .AddEntity(Unit(2, "B", "r1", commEnergyCost: 5))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "B", "contact", "hello", "r1"),
        });

        Assert.Equal(SimulationErrorCode.NoRoute, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Empty(world.GetInbox("B"));
        Assert.Equal(100, world.FindEntity(new EntityId(1))!.Energy);
    }

    [Fact]
    public void MessageCommittedBeforeDisconnectStillArrives()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 2)
            .AddEntity(Unit(1, "A", "r0"))
            .AddEntity(Unit(2, "B", "r1"))
            .Build();
        var engine = new SimulationEngine(world);

        engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "B", "contact", "hello", "r1"),
        });

        world.Links.Remove("l01");
        engine.Step(Array.Empty<Intent>());
        engine.Step(Array.Empty<Intent>());

        Assert.Single(world.GetInbox("B"));
    }

    [Fact]
    public void DuplicateMessageIdsAreRejectedOnLoad()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .Build();
        var engine = new SimulationEngine(world);

        engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "contact", "one"),
        });

        var data = WorldSaveSerializer.ToData(world);
        var delivered = data.Inboxes[0].Messages[0];
        var duplicated = data with
        {
            InFlightMessages = new[]
            {
                new MessageSaveData(
                    delivered.Id,
                    delivered.FromEntityId,
                    delivered.FromCivilizationId,
                    "A",
                    delivered.Topic,
                    delivered.Payload,
                    delivered.SentTick,
                    delivered.SentTick + 1,
                    delivered.TargetRegionId),
            },
        };

        var exception = Assert.Throws<SaveFormatException>(
            () => WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(duplicated)));

        Assert.Equal("DUPLICATE_MESSAGE_ID", exception.Code);
    }

    [Fact]
    public void V5PolicyMigratesToOpenTopicDefaults()
    {
        var world = WorldWithPolicy(
            allowAll: false,
            blockedTopics: new[] { "diplomacy" });
        var v5Data = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 5,
            MessagePolicies = new[]
            {
                new MessagePolicySaveData(
                    "B",
                    AllowAllInbound: false,
                    BlockedCivilizations: Array.Empty<string>(),
                    BlockedTopics: new[] { "diplomacy" }),
            },
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(v5Data));
        var policy = loaded.MessagePolicies["B"];

        Assert.True(policy.AllowAllTopics);
        Assert.Empty(policy.AllowedCivilizations);
        Assert.Empty(policy.AllowedTopics);
        Assert.Equal(
            SimulationErrorCode.MessageDenied,
            Assert.Single(Send(loaded).IntentResults).ErrorCode);
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
        string[]? blockedTopics = null,
        string[]? allowedCivilizations = null,
        bool allowAllTopics = true,
        string[]? allowedTopics = null) =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddMessagePolicy(
                "B",
                allowAll,
                blockedCivilizations,
                blockedTopics,
                allowedCivilizations,
                allowAllTopics,
                allowedTopics)
            .Build();

    private static EntityDefinition Unit(
        long id,
        string civilizationId,
        string regionId = "r0",
        long commEnergyCost = 0) =>
        new(
            new EntityId(id),
            civilizationId,
            regionId,
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Comm: new CommModule(1, EnergyCostPerMessage: commEnergyCost));
}
