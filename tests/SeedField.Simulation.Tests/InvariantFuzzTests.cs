using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// Seeded randomized scenarios. The goal is not balance coverage but invariant
/// coverage: resources never go negative, capacities hold, ids stay monotonic, and
/// save/load preserves the state hash.
/// </summary>
public sealed class InvariantFuzzTests
{
    [Fact]
    public void SeededRandomScenariosPreserveResourceInvariants()
    {
        for (var seed = 1; seed <= 30; seed++)
        {
            var random = new Random(seed);
            var world = BuildWorld(random);
            var engine = new SimulationEngine(world);

            for (var tick = 0; tick < 20; tick++)
            {
                engine.Step(RandomIntents(random));

                foreach (var region in world.Regions.Values)
                {
                    Assert.True(
                        region.GetMaterial("A") >= 0 &&
                        region.GetEnergy("A") >= 0,
                        $"seed={seed} tick={tick} region {region.Id} has a negative stock");
                }

                var core = world.Cores["A"];
                Assert.InRange(core.EnergyReserve, 0, core.Config.MaxEnergyReserve);
                Assert.InRange(core.MaterialReserve, 0, core.Config.MaxMaterialReserve);

                foreach (var entity in world.EnumerateEntities())
                {
                    Assert.InRange(entity.Energy, 0, entity.EnergyCapacity);
                    Assert.InRange(entity.Health, 0, entity.MaxHealth);
                }
            }

            var maxEntityId = world.EnumerateEntities()
                .Select(entity => entity.Id.Value)
                .DefaultIfEmpty(0)
                .Max();
            Assert.True(world.NextEntityId > maxEntityId);

            var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
            Assert.True(
                WorldStateHasher.Compute(world) == WorldStateHasher.Compute(loaded),
                $"seed={seed} save/load hash diverged");
        }
    }

    private static WorldState BuildWorld(Random random)
    {
        var capacity = random.Next(10, 101);
        var generation = random.Next(0, 4);
        return new WorldBuilder()
            .AddRegion("r0", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("r1", 1_000, 1_000)
            .AddLink("l0", "r0", "r1", latencyTicks: 1, powerPerTick: 20)
            .AddStock("r0", "A", random.Next(0, 51))
            .AddStock("r1", "A", random.Next(0, 51))
            .AddCore(
                "A",
                "r0",
                new SeedCoreConfig(
                    EnergyPerTick: random.Next(0, 5),
                    MaterialPerTick: random.Next(0, 5),
                    MaxEnergyReserve: 50,
                    MaxMaterialReserve: 50))
            .AddEntity(Entity(1, random, capacity, generation))
            .AddEntity(Entity(2, random, capacity, generation))
            .Build();
    }

    private static EntityDefinition Entity(
        long id,
        Random random,
        long capacity,
        int generation)
    {
        var energy = random.Next(0, (int)capacity + 1);
        return new EntityDefinition(
            new EntityId(id),
            "A",
            "r0",
            new Vec2(random.Next(0, 1_000), random.Next(0, 1_000)),
            MaxHealth: 50,
            Energy: energy,
            EnergyCapacity: capacity,
            Material: random.Next(0, 20),
            Drive: new DriveModule(100, EnergyCostPerMove: 1),
            Power: new PowerModule(generation, capacity));
    }

    private static Intent[] RandomIntents(Random random)
    {
        var intents = new List<Intent>();
        foreach (var id in new[] { 1L, 2L })
        {
            switch (random.Next(4))
            {
                case 0:
                    intents.Add(new MoveIntent(
                        new EntityId(id),
                        new Vec2(random.Next(0, 1_000), random.Next(0, 1_000))));
                    break;
                case 1:
                    intents.Add(new DrawEnergyIntent(
                        new EntityId(id),
                        random.Next(1, 6)));
                    break;
                case 2:
                    intents.Add(new SendEnergyIntent(
                        new EntityId(id),
                        random.Next(1, 6),
                        random.Next(2) == 0 ? "r0" : "r1"));
                    break;
                case 3:
                    intents.Add(new TransferMaterialIntent(
                        new EntityId(id),
                        "r1",
                        random.Next(1, 6)));
                    break;
            }
        }

        return intents.ToArray();
    }
}
