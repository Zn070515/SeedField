using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class SaveLoadTests
{
    [Fact]
    public void RoundTripPreservesHashAfterSimulation()
    {
        var world = TestWorlds.CombatPair();
        var engine = new SimulationEngine(world);

        for (var tick = 0; tick < 80; tick++)
        {
            engine.Step(Policy(world));
        }

        var json = WorldSaveSerializer.Save(world);
        var loaded = WorldSaveSerializer.Load(json);

        Assert.Equal(world.Tick, loaded.Tick);
        Assert.Equal(world.Statistics.ShotsFired, loaded.Statistics.ShotsFired);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    [Fact]
    public void InFlightMessageSurvivesSaveAndDeliversAfterLoad()
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
                Comm: new CommModule(LatencyTicks: 5, EnergyCostPerMessage: 1)))
            .Build();

        var engine = new SimulationEngine(world);
        engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "contact", "payload"),
        });

        var json = WorldSaveSerializer.Save(world);
        var loaded = WorldSaveSerializer.Load(json);
        var loadedEngine = new SimulationEngine(loaded);

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Single(loaded.InFlightMessages);
        Assert.Equal(5, loaded.InFlightMessages[0].DeliverTick);
        Assert.Empty(loaded.GetInbox("A"));

        for (var tick = 0; tick < 4; tick++)
        {
            loadedEngine.Step(Array.Empty<Intent>());
        }

        var message = Assert.Single(loaded.GetInbox("A"));
        Assert.Equal(5, message.DeliveredTick);
        Assert.Equal("payload", message.Payload);
        Assert.Empty(loaded.InFlightMessages);
    }

    [Fact]
    public void CoreReservesAndRecoverySurviveRoundTrip()
    {
        var world = new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("wilds", 10_000, 10_000)
            .AddCore("A", "core", new SeedCoreConfig(1, 1, 100, 100))
            .Build();

        var engine = new SimulationEngine(world);
        for (var tick = 0; tick < 100; tick++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var core = loaded.Cores["A"];

        Assert.Equal(100, core.EnergyReserve);
        Assert.Equal(100, core.MaterialReserve);
        Assert.Equal(world.Statistics.EnergyRegeneratedTotal, loaded.Statistics.EnergyRegeneratedTotal);

        var blueprint = new RecoveryBlueprint("minimum", 10, 10, 10, 0, 10, 0);
        var result = new SimulationEngine(loaded).TryBootstrapFromCore("A", blueprint, new Vec2(500, 500));
        Assert.True(result.Success);
        Assert.Equal(90, loaded.Cores["A"].EnergyReserve);
    }

    [Fact]
    public void RegionArrayOrderDoesNotAffectLoadedHash()
    {
        var world = new WorldBuilder()
            .AddRegion("b", 1_000, 1_000)
            .AddRegion("a", 1_000, 1_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "b",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 10,
                EnergyCapacity: 10,
                Material: 0))
            .Build();

        var data = WorldSaveSerializer.ToData(world);
        var reversed = data with { Regions = data.Regions.Reverse().ToArray() };
        var loaded = WorldSaveSerializer.FromData(reversed);

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    [Fact]
    public void V1SaveIsMigratedToCurrentSchema()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 25)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 50,
                EnergyCapacity: 100,
                Material: 5))
            .Build();

        var v1Data = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 1,
            Blueprints = null,
            Links = null,
            MaterialShipments = null,
            EnergyShipments = null,
            NextShipmentId = 0,
            NextEnergyShipmentId = 0,
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(v1Data));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    [Fact]
    public void SaveRejectsUnsupportedSchemaVersion()
    {
        var json = WorldSaveSerializer.Save(TestWorlds.CombatPair());
        var unsupported = json.Replace(
            $"\"schemaVersion\": {WorldSaveSerializer.CurrentSchemaVersion}",
            "\"schemaVersion\": 99");

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Load(unsupported));

        Assert.Equal("UNSUPPORTED_SAVE_VERSION", exception.Code);
    }

    [Fact]
    public void SaveRejectsMalformedJson()
    {
        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Load("{"));

        Assert.Equal("INVALID_SAVE", exception.Code);
    }

    private static List<Intent> Policy(WorldState world)
    {
        var intents = new List<Intent>();

        foreach (var entity in world.EnumerateEntities())
        {
            var enemy = world.EnumerateEntities().FirstOrDefault(candidate =>
                !string.Equals(candidate.CivilizationId, entity.CivilizationId, StringComparison.Ordinal));

            if (enemy is null)
            {
                continue;
            }

            if (entity.Position.IsWithinRange(enemy.Position, entity.Weapon?.RangeMm ?? 0))
            {
                intents.Add(new AttackIntent(entity.Id, enemy.Id));
            }
            else
            {
                intents.Add(new MoveIntent(entity.Id, enemy.Position));
            }
        }

        return intents;
    }
}
