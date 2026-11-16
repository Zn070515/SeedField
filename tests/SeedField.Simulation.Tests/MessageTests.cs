using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class MessageTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void SentMessageIsNeverVisibleInTheSameTick(int latencyTicks)
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
                Comm: new CommModule(latencyTicks, EnergyCostPerMessage: 5)))
            .Build();

        var engine = new SimulationEngine(world);

        var observationBefore = engine.BuildObservation("A");
        Assert.Empty(observationBefore.Messages);

        var send = new SendMessageIntent(new EntityId(1), "A", "contact", "{\"x\":900,\"y\":900}");
        var tickZero = engine.Step(new Intent[] { send });

        var queued = Assert.Single(tickZero.Events.OfType<MessageQueued>());
        Assert.Equal(0, queued.Tick);
        Assert.Equal(latencyTicks, queued.DeliverTick);

        if (latencyTicks == 1)
        {
            var delivered = Assert.Single(tickZero.Events.OfType<MessageDelivered>());
            Assert.Equal(1, delivered.Tick);
        }
        else
        {
            Assert.DoesNotContain(tickZero.Events, item => item is MessageDelivered);
            Assert.Empty(engine.BuildObservation("A").Messages);

            var tickOne = engine.Step(Array.Empty<Intent>());
            var delivered = Assert.Single(tickOne.Events.OfType<MessageDelivered>());
            Assert.Equal(latencyTicks, delivered.Tick);
        }

        var observationAfter = engine.BuildObservation("A");
        var message = Assert.Single(observationAfter.Messages);
        Assert.Equal(queued.MessageId, message.MessageId);
        Assert.Equal(0, message.SentTick);
        Assert.Equal(latencyTicks, message.DeliveredTick);
        Assert.Equal("{\"x\":900,\"y\":900}", message.Payload);
    }

    [Fact]
    public void MessageCostsExactlyTheConfiguredEnergy()
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
                Comm: new CommModule(1, EnergyCostPerMessage: 7)))
            .Build();

        var engine = new SimulationEngine(world);
        var result = engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "ping", "hello"),
        });

        Assert.True(result.IntentResults.Single().Accepted);
        Assert.Equal(93, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(7, world.Statistics.EnergySpentTotal);
    }

    [Fact]
    public void CrossCivilizationMessageRequiresATargetCommEndpoint()
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
                Comm: new CommModule(1, EnergyCostPerMessage: 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(500, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Comm: new CommModule(1, EnergyCostPerMessage: 1)))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "B", "hello", "world"),
        });

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        var delivered = Assert.Single(world.GetInbox("B"));
        Assert.Equal("hello", delivered.Topic);
        Assert.Empty(world.GetInbox("A"));
    }

    [Fact]
    public void CrossCivilizationMessageWithoutTargetCommIsRejected()
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
                Comm: new CommModule(1, EnergyCostPerMessage: 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(500, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "B", "hello", "world"),
        });

        Assert.Equal(SimulationErrorCode.NoRoute, Assert.Single(result.IntentResults).ErrorCode);
        Assert.Empty(world.InFlightMessages);
        Assert.Empty(world.GetInbox("B"));
    }
}
