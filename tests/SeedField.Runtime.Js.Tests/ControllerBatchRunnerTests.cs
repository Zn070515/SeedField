using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class ControllerBatchRunnerTests
{
    [Fact]
    public void ReturnsDecisionsInInputIndexOrder()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "A"))
            .AddEntity(Unit(3, "A"))
            .Build();
        var engine = new SimulationEngine(world);
        using var one = Device(new EntityId(1), "A");
        using var two = Device(new EntityId(2), "A");
        using var three = Device(new EntityId(3), "A");
        var runner = new ControllerBatchRunner(new ControllerExecutionOptions
        {
            MinBatchSize = 1,
            MaxDegreeOfParallelism = 1,
        });
        var items = new List<ControllerBatchItem>
        {
            new(0, three, engine.BuildControllerObservation(new EntityId(3))),
            new(1, one, engine.BuildControllerObservation(new EntityId(1))),
            new(2, two, engine.BuildControllerObservation(new EntityId(2))),
        };

        var decisions = runner.Run(items);

        Assert.Equal(3, decisions.Length);
        Assert.Equal(3, Assert.Single(decisions[0].Intents).EntityId.Value);
        Assert.Equal(1, Assert.Single(decisions[1].Intents).EntityId.Value);
        Assert.Equal(2, Assert.Single(decisions[2].Intents).EntityId.Value);
    }

    [Fact]
    public void RejectsNonPositiveExecutionBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ControllerBatchRunner(new ControllerExecutionOptions { MinBatchSize = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ControllerBatchRunner(new ControllerExecutionOptions
            {
                MaxDegreeOfParallelism = 0,
            }));
    }

    [Fact]
    public void ParallelBatchKeepsOrderAndIsolatesFaults()
    {
        var builder = new WorldBuilder().AddRegion("r0", 10_000, 10_000);
        for (var id = 1L; id <= 16; id++)
        {
            builder.AddEntity(Unit(id, "A"));
        }

        var world = builder.Build();
        var engine = new SimulationEngine(world);
        var controllers = new List<DeviceControllerSlot>();
        for (var id = 1L; id <= 16; id++)
        {
            var script = id == 7
                ? "function decide(obs) { throw new Error('boom'); }"
                : "function decide(obs) { return [{ type: \"wait\", entityId: obs.viewerEntityId }]; }";
            controllers.Add(new DeviceControllerSlot(new EntityId(id), "A", script));
        }

        var runner = new ControllerBatchRunner(new ControllerExecutionOptions
        {
            MinBatchSize = 1,
            MaxDegreeOfParallelism = 8,
        });
        var items = controllers
            .Select((controller, index) => new ControllerBatchItem(
                index,
                controller,
                engine.BuildControllerObservation(controller.EntityId)))
            .ToList();

        var decisions = RunOnDedicatedThread(() => runner.Run(items));

        Assert.Equal(8, runner.LastUsedDegreeOfParallelism);
        for (var index = 0; index < decisions.Length; index++)
        {
            var expected = items[index].Controller.EntityId.Value;
            if (expected == 7)
            {
                Assert.Empty(decisions[index].Intents);
                Assert.Contains(
                    decisions[index].Faults,
                    fault => fault.Code == ScriptFaultCodes.Fault);
            }
            else
            {
                Assert.Equal(expected, Assert.Single(decisions[index].Intents).EntityId.Value);
            }
        }

        foreach (var controller in controllers)
        {
            controller.Dispose();
        }
    }

    [Fact]
    public void PoolThreadFallsBackToSequentialToAvoidStarvation()
    {
        var builder = new WorldBuilder().AddRegion("r0", 10_000, 10_000);
        for (var id = 1L; id <= 8; id++)
        {
            builder.AddEntity(Unit(id, "A"));
        }

        var world = builder.Build();
        var engine = new SimulationEngine(world);
        var controllers = Enumerable.Range(1, 8)
            .Select(id => Device(new EntityId(id), "A"))
            .ToList();
        var runner = new ControllerBatchRunner(new ControllerExecutionOptions
        {
            MinBatchSize = 1,
            MaxDegreeOfParallelism = 4,
        });
        var items = controllers
            .Select((controller, index) => new ControllerBatchItem(
                index,
                controller,
                engine.BuildControllerObservation(controller.EntityId)))
            .ToList();

        var degree = Task.Run(() =>
        {
            runner.Run(items);
            return runner.LastUsedDegreeOfParallelism;
        }).GetAwaiter().GetResult();

        Assert.Equal(1, degree);
        foreach (var controller in controllers)
        {
            controller.Dispose();
        }
    }

    [Fact]
    public void ParallelBatchMatchesSequentialOnScriptedCombat()
    {
        var sequential = RunOnDedicatedThread(() => RunScenario(parallel: false));
        var parallel = RunOnDedicatedThread(() => RunScenario(parallel: true));

        Assert.Equal(sequential.Hash, parallel.Hash);
        Assert.Equal(sequential.Events, parallel.Events);
    }

    private static (ulong Hash, List<string> Events) RunScenario(bool parallel)
    {
        var builder = new WorldBuilder().AddRegion("r0", 20_000, 20_000);
        builder.AddEntity(Fighter(1, "A", 10_000));
        builder.AddEntity(Fighter(2, "A", 12_000));
        builder.AddEntity(Fighter(3, "B", 14_000));
        builder.AddEntity(Fighter(4, "B", 16_000));
        var world = builder.Build();
        var engine = new SimulationEngine(world);
        var controllers = world.EnumerateEntities()
            .Where(entity => entity.CivilizationId == "A")
            .Select(entity => new DeviceControllerSlot(entity.Id, "A", CombatScript))
            .ToList();
        var runner = new ControllerBatchRunner(new ControllerExecutionOptions
        {
            MinBatchSize = 1,
            MaxDegreeOfParallelism = parallel ? 4 : 1,
        });
        var events = new List<string>();

        for (var tick = 0; tick < 20; tick++)
        {
            var items = controllers
                .Select((controller, index) => new ControllerBatchItem(
                    index,
                    controller,
                    engine.BuildControllerObservation(controller.EntityId)))
                .ToList();
            var intents = runner.Run(items).SelectMany(decision => decision.Intents).ToList();
            var result = engine.Step(intents);
            events.AddRange(result.Events.Select(simulationEvent =>
                $"{simulationEvent.Tick}:{simulationEvent.GetType().Name}:{simulationEvent}"));
        }

        foreach (var controller in controllers)
        {
            controller.Dispose();
        }

        return (WorldStateHasher.Compute(world), events);
    }

    private const string CombatScript = """
        function decide(obs) {
            var me = null;
            var enemy = null;
            for (var i = 0; i < obs.entities.length; i++) {
                var e = obs.entities[i];
                if (e.entityId === obs.viewerEntityId) { me = e; }
                else if (!e.isSelf && !enemy) { enemy = e; }
            }
            if (!me) { return []; }
            if (enemy && me.hasWeapon) {
                return [{ type: "attack", entityId: obs.viewerEntityId, targetId: enemy.entityId }];
            }
            return [{ type: "wait", entityId: obs.viewerEntityId }];
        }
        """;

    private static EntityDefinition Fighter(long id, string civilizationId, int x) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            new Vec2(x, 10_000),
            MaxHealth: 100,
            Energy: 1_000_000,
            EnergyCapacity: 1_000_000,
            Material: 0,
            Sensor: new SensorModule(6_000),
            Weapon: new WeaponModule(RangeMm: 5_000, Damage: 7, EnergyCost: 1, CooldownTicks: 1),
            Drive: new DriveModule(SpeedMmPerTick: 100, EnergyCostPerMove: 1),
            Comm: new CommModule(LatencyTicks: 1, EnergyCostPerMessage: 1));

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

    private static DeviceControllerSlot Device(EntityId id, string civilizationId) =>
        new(
            id,
            civilizationId,
            "function decide(obs) { return [{ type: \"wait\", entityId: obs.viewerEntityId }]; }");

    private static EntityDefinition Unit(long id, string civilizationId) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 10,
            EnergyCapacity: 10,
            Material: 0);
}
