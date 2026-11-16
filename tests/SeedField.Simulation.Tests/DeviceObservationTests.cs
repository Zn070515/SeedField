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
