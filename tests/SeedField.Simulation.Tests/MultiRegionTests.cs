using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class MultiRegionTests
{
    [Fact]
    public void CrossRegionMessageUsesLinkLatencyAndTargetRegion()
    {
        var world = NewWorld()
            .AddLink("l01", "r0", "r1", latencyTicks: 3)
            .AddEntity(Sender(new Vec2(100, 100)))
            .AddEntity(Receiver(new Vec2(100, 100), "r1"))
            .Build();
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "contact", "payload", "r1"),
        });

        var queued = Assert.Single(result.Events.OfType<MessageQueued>());
        Assert.Equal(4, queued.DeliverTick); // comm 1 + link 3
        Assert.Equal("r1", queued.TargetRegionId);
        Assert.Equal("l01", queued.Route);
        Assert.Empty(world.GetInbox("A"));

        Step(engine, 3);

        var message = Assert.Single(world.GetInbox("A"));
        Assert.Equal("r1", message.TargetRegionId);
        Assert.Equal(4, message.DeliveredTick);
        Assert.Equal("r1", Assert.Single(engine.BuildObservation("A").Messages).TargetRegionId);
    }

    [Fact]
    public void CrossCivilizationRemoteMessageRequiresTargetEndpointAndLink()
    {
        var world = NewWorld()
            .AddLink("l01", "r0", "r1", latencyTicks: 2)
            .AddEntity(Sender(new Vec2(100, 100)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r1",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Comm: new CommModule(1, EnergyCostPerMessage: 0)))
            .Build();
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "B", "diplomacy", "hello", "r1"),
        });

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        Assert.Equal(3, Assert.Single(world.InFlightMessages).DeliverTick);
        Step(engine, 2);
        var message = Assert.Single(world.GetInbox("B"));
        Assert.Equal("r1", message.TargetRegionId);
        Assert.Empty(world.GetInbox("A"));
    }

    [Fact]
    public void CrossRegionMessageWithoutLinkIsRejected()
    {
        var world = NewWorld()
            .AddEntity(Sender(new Vec2(100, 100)))
            .AddEntity(Receiver(new Vec2(100, 100), "r1"))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "contact", "payload", "r1"),
        });

        Assert.Equal(SimulationErrorCode.NoRoute, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Empty(world.InFlightMessages);
    }

    [Fact]
    public void CrossRegionMessageRequiresOwnCommEndpointInTargetRegion()
    {
        var world = NewWorld()
            .AddLink("l01", "r0", "r1", latencyTicks: 3)
            .AddEntity(Sender(new Vec2(100, 100)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "r1",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 10,
                EnergyCapacity: 10,
                Material: 0))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "contact", "payload", "r1"),
        });

        Assert.Equal(SimulationErrorCode.NoRoute, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Empty(world.InFlightMessages);
    }

    [Fact]
    public void MultiHopLatencyIsTheSumOfLinks()
    {
        var world = NewWorld()
            .AddRegion("r2", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 2)
            .AddLink("l12", "r1", "r2", latencyTicks: 3)
            .AddEntity(Sender(new Vec2(100, 100)))
            .AddEntity(Receiver(new Vec2(100, 100), "r1"))
            .AddEntity(Receiver(new Vec2(100, 100), "r2"))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "contact", "payload", "r2"),
        });

        var queued = Assert.Single(result.Events.OfType<MessageQueued>());
        Assert.Equal(6, queued.DeliverTick); // comm 1 + 2 + 3
        Assert.Equal("r2", queued.TargetRegionId);
        Assert.Equal("l01>l12", queued.Route);
    }

    [Fact]
    public void ReverseRouteReconstructsLinksInTravelOrder()
    {
        var world = NewWorld()
            .AddRegion("r2", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 2)
            .AddLink("l12", "r1", "r2", latencyTicks: 3)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r2",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Comm: new CommModule(1, EnergyCostPerMessage: 0)))
            .AddEntity(Receiver(new Vec2(100, 100), "r1"))
            .AddEntity(Receiver(new Vec2(100, 100), "r0"))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "contact", "payload", "r0"),
        });

        var queued = Assert.Single(result.Events.OfType<MessageQueued>());
        Assert.Equal(6, queued.DeliverTick); // comm 1 + 3 + 2
        Assert.Equal("r0", queued.TargetRegionId);
        Assert.Equal("l12>l01", queued.Route);
    }

    [Fact]
    public void LinkBandwidthRejectsTheSecondMessageInTheSameTick()
    {
        var world = NewWorld()
            .AddLink("l01", "r0", "r1", latencyTicks: 3, messagesPerTick: 1)
            .AddEntity(Sender(new Vec2(100, 100)))
            .AddEntity(Receiver(new Vec2(100, 100), "r1"))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "one", "first", "r1"),
            new SendMessageIntent(new EntityId(1), "A", "two", "second", "r1"),
        });

        Assert.True(result.IntentResults[0].Accepted);
        Assert.Equal(SimulationErrorCode.NoBandwidth, result.IntentResults[1].ErrorCode);
        Assert.Single(world.InFlightMessages);
    }

    [Fact]
    public void SavePreservesLinksAndCrossRegionMessage()
    {
        var world = NewWorld()
            .AddLink("l01", "r0", "r1", latencyTicks: 3)
            .AddEntity(Sender(new Vec2(100, 100)))
            .AddEntity(Receiver(new Vec2(100, 100), "r1"))
            .Build();
        var engine = new SimulationEngine(world);

        engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "contact", "payload", "r1"),
        });

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var envelope = Assert.Single(loaded.InFlightMessages);

        Assert.Equal("r1", envelope.TargetRegionId);
        Assert.Single(loaded.Links);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));

        var loadedEngine = new SimulationEngine(loaded);
        Step(loadedEngine, 3);
        var message = Assert.Single(loaded.GetInbox("A"));
        Assert.Equal("r1", message.TargetRegionId);
        Assert.Equal(4, message.DeliveredTick);
    }

    private static void Step(SimulationEngine engine, int count)
    {
        for (var i = 0; i < count; i++)
        {
            engine.Step(Array.Empty<Intent>());
        }
    }

    private static WorldBuilder NewWorld() =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000);

    private static EntityDefinition Sender(Vec2 position) =>
        new(
            new EntityId(1),
            "A",
            "r0",
            position,
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Comm: new CommModule(1, EnergyCostPerMessage: 0));

    private static EntityDefinition Receiver(Vec2 position, string regionId) =>
        new(
            new EntityId(regionId == "r1" ? 2 : 3),
            "A",
            regionId,
            position,
            MaxHealth: 10,
            Energy: 10,
            EnergyCapacity: 10,
            Material: 0,
            Comm: new CommModule(1, EnergyCostPerMessage: 0));
}
