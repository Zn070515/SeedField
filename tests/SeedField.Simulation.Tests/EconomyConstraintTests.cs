using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class EconomyConstraintTests
{
    [Fact]
    public void PowerGeneratesIntoLocalStorageThenOverflows()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 5,
                EnergyCapacity: 10,
                Material: 0,
                Power: new PowerModule(EnergyPerTick: 2, StorageCapacity: 10)))
            .Build();

        var engine = new SimulationEngine(world);
        for (var tick = 0; tick < 4; tick++)
        {
            engine.Step(Array.Empty<Intent>());
        }

        Assert.Equal(10, world.FindEntity(new EntityId(1))!.Energy);
        Assert.Equal(8, world.Statistics.EnergyGeneratedTotal);
        Assert.Equal(3, world.Statistics.EnergyOverflowedTotal);
        Assert.Equal(3, world.Regions["r0"].GetEnergy("A"));
    }

    [Fact]
    public void ComputeBudgetRejectsASecondActionButAllowsWait()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 1_000,
                EnergyCapacity: 1_000,
                Material: 0,
                Drive: new DriveModule(100, EnergyCostPerMove: 0),
                State: new StateModule(ComputePerTick: 1, StorageCapacity: 10)))
            .Build();

        var engine = new SimulationEngine(world);
        var result = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(200, 100)),
            new MoveIntent(new EntityId(1), new Vec2(300, 100)),
            new WaitIntent(new EntityId(1)),
        });

        Assert.True(result.IntentResults[0].Accepted);
        Assert.Equal(SimulationErrorCode.ComputeBudgetExceeded, result.IntentResults[1].ErrorCode);
        Assert.True(result.IntentResults[2].Accepted);
        Assert.Equal(new Vec2(200, 100), world.FindEntity(new EntityId(1))!.Position);
    }

    [Fact]
    public void EntityWithoutStateModuleHasNoComputeLimit()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 1_000,
                EnergyCapacity: 1_000,
                Material: 0,
                Drive: new DriveModule(100, EnergyCostPerMove: 0)))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(200, 100)),
            new MoveIntent(new EntityId(1), new Vec2(300, 100)),
        });

        Assert.All(result.IntentResults, item => Assert.True(item.Accepted));
        Assert.Equal(new Vec2(300, 100), world.FindEntity(new EntityId(1))!.Position);
    }

    [Fact]
    public void BandwidthLimitRejectsTheSecondMessage()
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
                Comm: new CommModule(
                    LatencyTicks: 1,
                    EnergyCostPerMessage: 0,
                    BandwidthPerTick: 1)))
            .Build();

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "one", "first"),
            new SendMessageIntent(new EntityId(1), "A", "two", "second"),
        });

        Assert.True(result.IntentResults[0].Accepted);
        Assert.Equal(SimulationErrorCode.NoBandwidth, result.IntentResults[1].ErrorCode);
        Assert.Single(world.GetInbox("A"));
    }

    [Fact]
    public void DeploymentConsumesLocalMaterialAndRejectsWhenInsufficient()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 50)
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "block",
            0,
            "Block",
            10,
            10,
            Power: new PowerModule(1, 10),
            MaterialCost: 30));

        DeploymentService.Deploy(world, blueprint, "A", "r0", new Vec2(100, 100));

        Assert.Equal(20, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(30, world.Statistics.MaterialSpentTotal);

        var exception = Assert.Throws<BlueprintValidationException>(() =>
            DeploymentService.Deploy(world, blueprint, "A", "r0", new Vec2(200, 200)));
        Assert.Equal("INSUFFICIENT_MATERIAL", exception.Code);

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        Assert.Equal(20, loaded.Regions["r0"].GetMaterial("A"));
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }

    [Fact]
    public void StorageCapacityExpiresOldestInboxMessages()
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
                Comm: new CommModule(1, EnergyCostPerMessage: 0),
                State: new StateModule(ComputePerTick: 10, StorageCapacity: 2)))
            .Build();
        var engine = new SimulationEngine(world);

        TickResult? lastResult = null;
        for (var i = 0; i < 3; i++)
        {
            lastResult = engine.Step(new Intent[]
            {
                new SendMessageIntent(new EntityId(1), "A", $"topic-{i}", $"payload-{i}"),
            });
            Assert.True(lastResult.IntentResults[0].Accepted);
        }

        var inbox = world.GetInbox("A");
        Assert.Equal(new long[] { 2, 3 }, inbox.Select(message => message.Id));
        Assert.Equal(3, world.Statistics.MessagesDelivered);
        Assert.Equal(1, world.Statistics.MessagesExpired);
        Assert.Contains(lastResult!.Events, item => item is MessageExpired { MessageId: 1 });
    }

    [Fact]
    public void PowerAndStateModulesSurviveDeploymentAndSaveLoad()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "generator",
            0,
            "Generator",
            50,
            100,
            Sensor: new SensorModule(1_000),
            Comm: new CommModule(1, EnergyCostPerMessage: 1, BandwidthPerTick: 2),
            Power: new PowerModule(EnergyPerTick: 3, StorageCapacity: 100),
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 20)));

        var entity = DeploymentService.Deploy(world, blueprint, "A", "r0", new Vec2(100, 100));
        Assert.NotNull(entity.Power);
        Assert.NotNull(entity.State);

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var loadedEntity = Assert.Single(loaded.EnumerateEntities());

        Assert.Equal(3, loadedEntity.Power!.EnergyPerTick);
        Assert.Equal(2, loadedEntity.State!.ComputePerTick);
        Assert.Equal(2, loadedEntity.Comm!.BandwidthPerTick);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
    }
}
