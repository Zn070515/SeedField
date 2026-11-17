# Parallel Controller Batch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut per-tick controller cost from 31 ms to under 16.7 ms for 100 always-active devices, and scale to 1000 devices at 6 Hz, without changing script call frequency or intent order.

**Architecture:** Independent per-device Jint engines already exist. Run their `Decide` calls in a bounded `Parallel.For` batch after the main thread builds all observations; return decisions in input order and keep `SimulationEngine.Step` as the only world writer.

**Tech Stack:** .NET 10, C#, Jint 4.18.0, xUnit, Headless benchmark CLI.

---

### Task 1: Sequential batch runner

**Files:**
- Create: `src/SeedField.Runtime.Js/ControllerExecutionOptions.cs`
- Create: `src/SeedField.Runtime.Js/ControllerBatchRunner.cs`
- Test: `tests/SeedField.Runtime.Js.Tests/ControllerBatchRunnerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
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

        Assert.Equal(3, decisions.Count);
        Assert.Equal(3, Assert.Single(decisions[0].Intents).EntityId.Value);
        Assert.Equal(1, Assert.Single(decisions[1].Intents).EntityId.Value);
        Assert.Equal(2, Assert.Single(decisions[2].Intents).EntityId.Value);
    }

    private static DeviceControllerSlot Device(EntityId id, string civ) =>
        new(id, civ, "function decide(obs) { return [{ type: \"wait\", entityId: obs.viewerEntityId }]; }");

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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/SeedField.Runtime.Js.Tests/SeedField.Runtime.Js.Tests.csproj --filter ReturnsDecisionsInInputIndexOrder`
Expected: FAIL with `ControllerBatchRunner` / `ControllerBatchItem` not found.

- [ ] **Step 3: Write minimal implementation**

`ControllerExecutionOptions.cs`:

```csharp
namespace SeedField.Runtime.Js;

/// <summary>
/// Execution policy for one tick's controller batch. The runner stays sequential
/// below <see cref="MinBatchSize"/> so small worlds do not pay thread-pool costs.
/// </summary>
public sealed record ControllerExecutionOptions
{
    public int MinBatchSize { get; init; } = 8;

    public int MaxDegreeOfParallelism { get; init; } = Math.Max(1, Environment.ProcessorCount);

    public void Validate()
    {
        if (MinBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinBatchSize));
        }

        if (MaxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDegreeOfParallelism));
        }
    }
}
```

`ControllerBatchRunner.cs`:

```csharp
using SeedField.Simulation;

namespace SeedField.Runtime.Js;

public sealed record ControllerBatchItem(
    int Index,
    DeviceControllerSlot Controller,
    Observation Observation);

/// <summary>
/// Executes one tick's independent device controllers. Observations are built by
/// the caller before <see cref="Run"/>; the runner only reads them and never
/// touches WorldState.
/// </summary>
public sealed class ControllerBatchRunner
{
    private readonly ControllerExecutionOptions _options;

    public ControllerBatchRunner(ControllerExecutionOptions? options = null)
    {
        _options = options ?? new ControllerExecutionOptions();
        _options.Validate();
    }

    public int LastUsedDegreeOfParallelism { get; private set; } = 1;

    public ControllerDecision[] Run(IReadOnlyList<ControllerBatchItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var decisions = new ControllerDecision[items.Count];
        if (items.Count == 0)
        {
            LastUsedDegreeOfParallelism = 1;
            return decisions;
        }

        var degree = Math.Min(_options.MaxDegreeOfParallelism, items.Count);
        var parallel = items.Count >= _options.MinBatchSize && degree > 1;
        LastUsedDegreeOfParallelism = parallel ? degree : 1;

        if (parallel)
        {
            Parallel.For(
                0,
                items.Count,
                new ParallelOptions { MaxDegreeOfParallelism = degree },
                index => decisions[items[index].Index] = Decide(items[index]));
        }
        else
        {
            foreach (var item in items)
            {
                decisions[item.Index] = Decide(item);
            }
        }

        return decisions;
    }

    private static ControllerDecision Decide(ControllerBatchItem item)
    {
        try
        {
            return item.Controller.Decide(item.Observation);
        }
        catch (Exception exception)
        {
            return ControllerDecision.Fault(ScriptFaultCodes.Fault, exception.Message);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/SeedField.Runtime.Js.Tests/SeedField.Runtime.Js.Tests.csproj --filter ReturnsDecisionsInInputIndexOrder`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/SeedField.Runtime.Js/ControllerExecutionOptions.cs src/SeedField.Runtime.Js/ControllerBatchRunner.cs tests/SeedField.Runtime.Js.Tests/ControllerBatchRunnerTests.cs
git commit -m "feat(perf): SeedField 控制器顺序批处理"
```

### Task 2: Parallel execution, ordering, fault isolation

**Files:**
- Modify: `tests/SeedField.Runtime.Js.Tests/ControllerBatchRunnerTests.cs`
- Modify: `src/SeedField.Runtime.Js/ControllerBatchRunner.cs` (only if a test fails)

- [ ] **Step 1: Add the failing tests**

```csharp
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

        var decisions = runner.Run(items);

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

        foreach (var controller in controllers) controller.Dispose();
    }

    [Fact]
    public void ParallelBatchMatchesSequentialOnScriptedCombat()
    {
        var sequential = RunScenario(parallel: false);
        var parallel = RunScenario(parallel: true);

        Assert.Equal(sequential.Hash, parallel.Hash);
        Assert.Equal(sequential.Events, parallel.Events);
    }

    private static (ulong Hash, List<string> Events) RunScenario(bool parallel)
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 20_000, 20_000)
            .AddEntity(Fighter(1, "A", 10_000))
            .AddEntity(Fighter(2, "A", 12_000))
            .AddEntity(Fighter(3, "B", 14_000))
            .AddEntity(Fighter(4, "B", 16_000))
            .Build();
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

        foreach (var controller in controllers) controller.Dispose();
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SeedField.Runtime.Js.Tests/SeedField.Runtime.Js.Tests.csproj --filter "ParallelBatch|ParallelBatchMatches"`
Expected: FAIL. `ParallelBatchKeepsOrderAndIsolatesFaults` should fail because the
sequential-only runner reports `LastUsedDegreeOfParallelism = 1` or because the
fault path is not isolated; the combat test should fail if execution is not
parallel. If both pass immediately, change the runner back to sequential and
re-run to confirm the tests actually exercise the parallel branch.

- [ ] **Step 3: Implement parallel execution**

The Task 1 implementation already contains the `Parallel.For` branch. If the
ordering test fails, the cause is writing `decisions[index]` instead of
`decisions[items[index].Index]`; fix that. If the fault test fails, ensure
`Decide` catches `Exception` and returns `ControllerDecision.Fault`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/SeedField.Runtime.Js.Tests/SeedField.Runtime.Js.Tests.csproj --filter "ParallelBatch"`
Expected: PASS for all three batch tests.

- [ ] **Step 5: Commit**

```powershell
git add tests/SeedField.Runtime.Js.Tests/ControllerBatchRunnerTests.cs src/SeedField.Runtime.Js/ControllerBatchRunner.cs
git commit -m "test(perf): SeedField 并行批处理顺序与故障隔离"
```

### Task 3: Main integration

**Files:**
- Modify: `game/SeedField.Game/Scripts/Main.Smoke.cs:337-352`
- Modify: `game/SeedField.Game/Scripts/Main.cs:43`

- [ ] **Step 1: Add the runner field**

In `Main.cs` near the existing `_deviceControllers` field:

```csharp
    private readonly ControllerBatchRunner _controllerBatch = new();
```

- [ ] **Step 2: Replace the sequential loop**

Replace the body of `StepOnce` up to `var result = _engine.Step(intents);` with:

```csharp
    private void StepOnce()
    {
        var items = new List<ControllerBatchItem>(_deviceControllers.Count);
        foreach (var controller in _deviceControllers)
        {
            if (_world.FindEntity(controller.EntityId) is null)
            {
                continue;
            }

            items.Add(new ControllerBatchItem(
                items.Count,
                controller,
                _engine.BuildControllerObservation(controller.EntityId)));
        }

        var decisions = _controllerBatch.Run(items);
        var intents = new List<Intent>(decisions.Length);
        foreach (var decision in decisions)
        {
            intents.AddRange(decision.Intents);
            _faultCount += decision.Faults.Count;
        }

        var result = _engine.Step(intents);
```

- [ ] **Step 3: Build the game project**

Run: `dotnet build game/SeedField.Game/SeedField.Game.csproj -c Debug`
Expected: build succeeds, 0 warnings.

- [ ] **Step 4: Commit**

```powershell
git add game/SeedField.Game/Scripts/Main.cs game/SeedField.Game/Scripts/Main.Smoke.cs
git commit -m "perf(game): SeedField Main 控制器并行批处理"
```

## Verification

- `dotnet build SeedField.slnx` and `dotnet test SeedField.slnx` pass.
- `--benchmark-jint-parallel 100 --parallel-dof 16` stays under 16.7 ms/tick.
- `--benchmark-jint-parallel 1000 --parallel-dof 16` stays under 167 ms/tick.
- Sequential and parallel scripted combat produce the same events and world hash.

## Rollback

Revert the batch-runner and Main integration commits. `Main.Smoke.cs` returns to
the per-controller `foreach`; Simulation, save schema, and controller scripts are
unchanged, so no data migration is needed.
