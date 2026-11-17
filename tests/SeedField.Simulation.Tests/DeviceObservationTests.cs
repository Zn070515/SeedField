using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class DeviceObservationTests
{
    [Fact]
    public void DeviceOnlySeesEnemiesItsOwnSensorDetects()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), sensor: 1_000))
            .AddEntity(Unit(2, "A", new Vec2(200, 100), sensor: null))
            .AddEntity(Unit(3, "B", new Vec2(500, 100), sensor: null))
            .Build();
        var engine = new SimulationEngine(world);

        var scout = engine.BuildDeviceObservation(new EntityId(1));
        var gunner = engine.BuildDeviceObservation(new EntityId(2));

        Assert.Equal(1, scout.ViewerEntityId);
        Assert.Equal(new long[] { 1, 2, 3 }, scout.Entities.Select(entity => entity.EntityId));
        Assert.True(scout.Entities.Single(entity => entity.EntityId == 1).IsSelf);
        Assert.True(scout.Entities.Single(entity => entity.EntityId == 2).IsSelf);
        Assert.False(scout.Entities.Single(entity => entity.EntityId == 3).IsSelf);

        Assert.Equal(2, gunner.ViewerEntityId);
        Assert.Equal(new long[] { 1, 2 }, gunner.Entities.Select(entity => entity.EntityId));
    }

    [Fact]
    public void ControllerViewOmitsAlliesButKeepsSensedEnemies()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), sensor: 1_000))
            .AddEntity(Unit(2, "A", new Vec2(200, 100), sensor: null))
            .AddEntity(Unit(3, "B", new Vec2(500, 100), sensor: null))
            .Build();
        var engine = new SimulationEngine(world);

        var controller = engine.BuildControllerObservation(new EntityId(1));

        Assert.Equal(new long[] { 1, 3 }, controller.Entities.Select(entity => entity.EntityId));
        Assert.True(controller.Entities.Single(entity => entity.EntityId == 1).IsSelf);
        Assert.False(controller.Entities.Single(entity => entity.EntityId == 3).IsSelf);
        Assert.Equal(
            new long[] { 1, 2, 3 },
            engine.BuildDeviceObservation(new EntityId(1))
                .Entities.Select(entity => entity.EntityId));
    }

    [Fact]
    public void WideSensorStillSeesEnemiesInFarCells()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 20_000, 20_000)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), sensor: int.MaxValue))
            .AddEntity(Unit(2, "B", new Vec2(15_000, 15_000), sensor: null))
            .Build();
        var engine = new SimulationEngine(world);

        var observation = engine.BuildDeviceObservation(new EntityId(1));

        Assert.Equal(
            new long[] { 1, 2 },
            observation.Entities.Select(entity => entity.EntityId));
    }

    [Fact]
    public void ParallelObservationBatchMatchesSerial()
    {
        var builder = new WorldBuilder().AddRegion("r0", 20_000, 20_000);
        for (var id = 1L; id <= 16; id++)
        {
            builder.AddEntity(Unit(id, "A", new Vec2(1_000 + (int)id * 100, 1_000), sensor: 6_000));
            builder.AddEntity(Unit(100 + id, "B", new Vec2(2_000 + (int)id * 100, 1_000), sensor: null));
        }

        var world = builder.Build();
        var engine = new SimulationEngine(world);
        var ids = world.EnumerateEntities()
            .Where(entity => entity.CivilizationId == "A")
            .Select(entity => entity.Id)
            .ToList();

        var serial = ids
            .Select(id => engine.BuildControllerObservation(id))
            .ToList();
        var parallel = RunOnDedicatedThread(
            () => engine.BuildControllerObservations(ids, maxDegreeOfParallelism: 2));

        Assert.Equal(serial.Count, parallel.Length);
        for (var index = 0; index < serial.Count; index++)
        {
            Assert.Equal(serial[index].ViewerEntityId, parallel[index].ViewerEntityId);
            Assert.Equal(
                serial[index].Entities.Select(entity => entity.EntityId),
                parallel[index].Entities.Select(entity => entity.EntityId));
            Assert.Equal(
                serial[index].Messages.Select(message => message.MessageId),
                parallel[index].Messages.Select(message => message.MessageId));
            Assert.Equal(
                serial[index].ControlPoints?.Count,
                parallel[index].ControlPoints?.Count);
        }
    }

    [Fact]
    public void DeploymentAfterAnObservationRefreshesTheCachedView()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), sensor: 1_000))
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "probe",
            0,
            "Probe",
            20,
            10,
            Sensor: new SensorModule(1_000),
            Power: new PowerModule(1, 10),
            MaterialCost: 10));
        var engine = new SimulationEngine(world);

        // Populate the per-tick cache before mutating the world at the same tick.
        Assert.Equal(
            new long[] { 1 },
            engine.BuildControllerObservation(new EntityId(1))
                .Entities.Select(entity => entity.EntityId));

        var deployed = engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            blueprint,
            "r0",
            new Vec2(200, 100)));
        Assert.True(deployed.Success);

        var deployedId = deployed.EntityId!.Value.Value;
        Assert.Contains(
            engine.BuildDeviceObservation(new EntityId(1)).Entities,
            entity => entity.EntityId == deployedId);
    }

    [Fact]
    public void DeviceMessagesKeepOnlyTheMostRecentHistory()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), sensor: null))
            .Build();
        world.Inboxes["A"] = Enumerable.Range(1, 40)
            .Select(id => new DeliveredMessage(
                id,
                new EntityId(1),
                "A",
                "r0",
                "contact",
                $"payload-{id}",
                0,
                id))
            .ToList();

        var engine = new SimulationEngine(world);
        var messages = engine.BuildDeviceObservation(new EntityId(1)).Messages;

        Assert.Equal(ObservationLimits.MaxMessages, messages.Count);
        Assert.Equal(9, messages[0].MessageId);
        Assert.Equal(40, messages[^1].MessageId);
        Assert.Equal(
            messages.OrderBy(message => message.MessageId).Select(message => message.MessageId),
            messages.Select(message => message.MessageId));
    }

    [Fact]
    public void DeviceMessagesAreFilteredToItsRegion()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddEntity(Unit(1, "A", new Vec2(100, 100), sensor: null))
            .AddEntity(Unit(2, "A", new Vec2(100, 100), sensor: null, regionId: "r1"))
            .Build();

        world.Inboxes["A"] = new List<DeliveredMessage>
        {
            new(1, new EntityId(1), "A", "r1", "contact", "payload", 0, 1),
        };

        var engine = new SimulationEngine(world);

        Assert.Empty(engine.BuildDeviceObservation(new EntityId(1)).Messages);
        var remote = engine.BuildDeviceObservation(new EntityId(2));
        Assert.Equal("contact", Assert.Single(remote.Messages).Topic);
    }

    private static T RunOnDedicatedThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            throw error;
        }

        return result;
    }

    private static EntityDefinition Unit(
        long id,
        string civilizationId,
        Vec2 position,
        int? sensor,
        string regionId = "r0") =>
        new(
            new EntityId(id),
            civilizationId,
            regionId,
            position,
            MaxHealth: 10,
            Energy: 10,
            EnergyCapacity: 10,
            Material: 0,
            Sensor: sensor is null ? null : new SensorModule(sensor.Value));
}
