using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class ObservationTests
{
    [Fact]
    public void ObservationContainsOwnEntitiesAndOnlySensedForeignEntities()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 50_000, 50_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(1_000, 1_000),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 10,
                Material: 0,
                Sensor: new SensorModule(2_000)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(1_500, 1_000),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 10,
                Material: 0))
            .AddEntity(new EntityDefinition(
                new EntityId(3),
                "B",
                "r0",
                new Vec2(40_000, 40_000),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 10,
                Material: 0,
                Sensor: new SensorModule(100)))
            .Build();

        var engine = new SimulationEngine(world);
        var observation = engine.BuildObservation("A");

        Assert.Equal(new long[] { 1, 2 }, observation.Entities.Select(entity => entity.EntityId));
        Assert.True(observation.Entities.Single(entity => entity.EntityId == 1).IsSelf);
        Assert.False(observation.Entities.Single(entity => entity.EntityId == 2).IsSelf);

        var farCivilization = engine.BuildObservation("B");
        Assert.Contains(farCivilization.Entities, entity => entity.EntityId == 3);
        Assert.DoesNotContain(farCivilization.Entities, entity => entity.EntityId == 1);
    }

    [Fact]
    public void OwnEntityInAnotherRegionIsStillKnown()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 10,
                Material: 0))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "r1",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 10,
                Material: 0))
            .Build();

        var observation = new SimulationEngine(world).BuildObservation("A");

        Assert.Equal(2, observation.Entities.Count);
        Assert.Contains(observation.Entities, entity => entity.RegionId == "r1");
    }
}
