# Parallel Controller Batch — Hot Path and Gates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the conservative hot path, evidence, ADR, CI performance gate, and Godot smoke trigger for the parallel controller batch.

**Architecture:** Keep the JSON boundary and per-device engines; cache the harness entry point, add a byte-limit fast path, and cache per-region observation slices. Then document and gate the measured result.

**Tech Stack:** .NET 10, C#, Jint 4.18.0, xUnit, GitHub Actions.

---

### Task 4: Headless parallel benchmark and budget gate

**Files:**
- Modify: `src/SeedField.Headless/Program.cs:40-47`
- Modify: `src/SeedField.Headless/Program.Perf.cs:88-174`

- [ ] **Step 1: Parse the flags**

In `Program.cs`, before the existing `--benchmark-jint` branch, add:

```csharp
        if (TryReadBenchmarkDevices(
                args,
                "--benchmark-jint-parallel",
                out var parallelDevices,
                out var parallelInvalid))
        {
            var dof = ReadIntFlag(args, "--parallel-dof", 0);
            var budget = ReadDoubleFlag(args, "--budget-ms", 0);
            return parallelInvalid
                ? 2
                : RunParallelJintBenchmark(parallelDevices, dof, budget);
        }
```

Add these helpers beside `TryReadBenchmarkDevices`:

```csharp
    private static int ReadIntFlag(string[] args, string flag, int fallback)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length &&
               int.TryParse(args[index + 1], out var value)
            ? value
            : fallback;
    }

    private static double ReadDoubleFlag(string[] args, string flag, double fallback)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length &&
               double.TryParse(
                   args[index + 1],
                   System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var value)
            ? value
            : fallback;
    }
```

- [ ] **Step 2: Add the benchmark method**

At the end of `Program.Perf.cs`:

```csharp
    private static int RunParallelJintBenchmark(int devices, int dof, double budgetMs)
    {
        var world = BuildPerfWorld(devices);
        var engine = new SimulationEngine(world);
        var options = new ControllerExecutionOptions
        {
            MinBatchSize = 1,
            MaxDegreeOfParallelism = dof > 0 ? dof : Environment.ProcessorCount,
        };
        var runner = new ControllerBatchRunner(options);
        var controllers = world.EnumerateEntities()
            .Where(entity => entity.CivilizationId == "A")
            .Select(entity => new DeviceControllerSlot(entity.Id, "A", PerfDeviceScript))
            .ToList();

        for (var tick = 0; tick < 2; tick++)
        {
            var items = controllers
                .Select((controller, index) => new ControllerBatchItem(
                    index,
                    controller,
                    engine.BuildControllerObservation(controller.EntityId)))
                .ToList();
            engine.Step(runner.Run(items).SelectMany(decision => decision.Intents).ToList());
        }

        var observationMs = 0.0;
        var decideMs = 0.0;
        var stepMs = 0.0;
        var total = Stopwatch.StartNew();
        for (var tick = 0; tick < 10; tick++)
        {
            var phase = Stopwatch.StartNew();
            var items = new List<ControllerBatchItem>(controllers.Count);
            for (var index = 0; index < controllers.Count; index++)
            {
                items.Add(new ControllerBatchItem(
                    index,
                    controllers[index],
                    engine.BuildControllerObservation(controllers[index].EntityId)));
            }

            phase.Stop();
            observationMs += phase.Elapsed.TotalMilliseconds;

            phase.Restart();
            var decisions = runner.Run(items);
            phase.Stop();
            decideMs += phase.Elapsed.TotalMilliseconds;

            phase.Restart();
            engine.Step(decisions.SelectMany(decision => decision.Intents).ToList());
            phase.Stop();
            stepMs += phase.Elapsed.TotalMilliseconds;
        }

        total.Stop();
        foreach (var controller in controllers) controller.Dispose();

        var ticks = 10;
        var avgTickMs = total.Elapsed.TotalMilliseconds / ticks;
        Console.WriteLine(
            $"JINT_PARALLEL devices={devices} entities={world.EnumerateEntities().Count()} " +
            $"ticks={ticks} avgTickMs={avgTickMs:0.00} " +
            $"ticksPerSec={1000.0 / avgTickMs:0.0} obsMs={observationMs / ticks:0.00} " +
            $"decideMs={decideMs / ticks:0.00} stepMs={stepMs / ticks:0.00} " +
            $"dof={runner.LastUsedDegreeOfParallelism}");

        if (budgetMs > 0 && avgTickMs > budgetMs)
        {
            Console.WriteLine($"BUDGET_EXCEEDED avgTickMs={avgTickMs:0.00} budgetMs={budgetMs:0.00}");
            return 2;
        }

        return 0;
    }
```

Add `using SeedField.Runtime.Js;` at the top of `Program.Perf.cs` if it is missing.

- [ ] **Step 3: Run the benchmark at 100 and 1000 devices**

```powershell
dotnet build SeedField.slnx -c Release
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint-parallel 100 --parallel-dof 16 > logs/bench-parallel-100.log 2>&1
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint-parallel 1000 --parallel-dof 16 > logs/bench-parallel-1000.log 2>&1
```

Expected: logs contain `JINT_PARALLEL` lines. Record the values for the evidence
file. 100 devices should be far below 16.7 ms; 1000 devices should be below 167 ms.
If 1000 devices exceeds 167 ms, re-run with `--parallel-dof 32`; if still over,
record the number and continue with the hot-path task before re-measuring.

- [ ] **Step 4: Verify the budget exit code**

```powershell
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint-parallel 100 --budget-ms 0.01
echo $LASTEXITCODE
```

Expected: `BUDGET_EXCEEDED` and exit code `2`.

- [ ] **Step 5: Commit**

```powershell
git add src/SeedField.Headless/Program.cs src/SeedField.Headless/Program.Perf.cs
git commit -m "feat(perf): SeedField 并行 Jint 基准与预算门禁"
```


### Task 5: Conservative hot path

**Files:**
- Modify: `src/SeedField.Runtime.Js/JsController.cs`
- Modify: `src/SeedField.Simulation/ObservationCache.cs`
- Modify: `src/SeedField.Simulation/ObservationBuilder.cs`
- Test: `tests/SeedField.Runtime.Js.Tests/JsControllerTests.cs`

- [ ] **Step 1: Add the failing size-check test**

Add to `JsControllerTests`:

```csharp
    [Fact]
    public void RejectsObservationAboveByteLimitBeforeParsing()
    {
        using var controller = new JsController(
            "A",
            "function decide(obs) { return []; }");
        var oversized = new string(' ', JsController.MaxObservationBytes + 1);

        var result = controller.RunJson(oversized);

        Assert.False(result.Success);
        Assert.Equal(ScriptFaultCodes.Fault, result.FaultCode);
    }
```

Run: `dotnet test tests/SeedField.Runtime.Js.Tests/SeedField.Runtime.Js.Tests.csproj --filter RejectsObservationAboveByteLimitBeforeParsing`
Expected: PASS already (the old code enforces the limit). This test pins the
behavior while the fast path is added.

- [ ] **Step 2: Cache the harness function and add the byte fast path**

In `JsController`, add a field:

```csharp
    private JsValue _runFunction = JsValue.Undefined;
```

After `_engine.Execute(Harness);` in the constructor:

```csharp
            _runFunction = _engine.Evaluate("__seedfield_run");
```

Replace the byte check in `RunJson` with:

```csharp
        var length = observationJson.Length;
        if (length > MaxObservationBytes ||
            (length * 4L > MaxObservationBytes &&
             System.Text.Encoding.UTF8.GetByteCount(observationJson) > MaxObservationBytes))
        {
            return ControllerRunResult.Fault(
                ScriptFaultCodes.Fault,
                $"Observation exceeds {MaxObservationBytes} bytes.");
        }
```

Replace `_engine.Invoke("__seedfield_run", observationJson)` with:

```csharp
            var result = _engine.Invoke(_runFunction, observationJson);
```

Add `using Jint.Native;` to the file.

- [ ] **Step 3: Run the controller tests**

Run: `dotnet test tests/SeedField.Runtime.Js.Tests/SeedField.Runtime.Js.Tests.csproj`
Expected: all pass.

- [ ] **Step 4: Cache diplomacy/logistics slices in `ObservationCache`**

Store the world in the cache:

```csharp
    private readonly WorldState _world;
    private readonly Dictionary<string, List<ObservedMessage>> _messages = new();
    private readonly Dictionary<string, List<ObservedShipment>> _shipments = new();
    private readonly Dictionary<string, List<ObservedEnergyShipment>> _energyShipments = new();
    private readonly Dictionary<string, List<ObservedControlPoint>> _controlPoints = new();
    private readonly Dictionary<string, List<ObservedTreaty>> _treaties = new();
    private readonly Dictionary<string, List<ObservedTreatyProposal>> _proposals = new();
    private readonly Dictionary<string, List<ObservedTradeOffer>> _tradeOffers = new();

    private ObservationCache(WorldState world)
    {
        _world = world;
        Tick = world.Tick;
        foreach (var region in world.Regions.Values)
        {
            _regions[region.Id] = region;
        }
    }

    private static List<T> Cached<T>(
        Dictionary<string, List<T>> cache, string key, Func<List<T>> build)
    {
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var value = build();
        cache[key] = value;
        return value;
    }

    public IReadOnlyList<ObservedMessage> Messages(string regionId, string civilizationId) =>
        Cached(_messages, $"{regionId}\0{civilizationId}", () => _world
            .GetInbox(civilizationId)
            .Where(message => string.Equals(
                message.TargetRegionId, regionId, StringComparison.Ordinal))
            .OrderBy(message => message.Id)
            .Select(message => new ObservedMessage(
                message.Id,
                message.FromEntityId.Value,
                message.FromCivilizationId,
                message.TargetRegionId,
                message.Topic,
                message.Payload,
                message.SentTick,
                message.DeliveredTick))
            .ToList());
```

Repeat the same pattern for `Shipments`, `EnergyShipments`, `ControlPoints`,
`Treaties`, `Proposals`, and `TradeOffers`, copying the existing filters from
`ObservationBuilder.BuildForEntity` verbatim (same `OrderBy` and `Where` clauses).
Then replace the inline LINQ blocks in `BuildForEntity` with:

```csharp
        var messages = cache.Messages(viewer.RegionId, viewer.CivilizationId);
        var shipments = cache.Shipments(viewer.RegionId, viewer.CivilizationId);
        var energyShipments = cache.EnergyShipments(viewer.RegionId, viewer.CivilizationId);
        var controlPoints = cache.ControlPoints(viewer.RegionId);
        var treaties = cache.Treaties(viewer.CivilizationId);
        var proposals = cache.Proposals(viewer.CivilizationId);
        var tradeOffers = cache.TradeOffers(viewer.RegionId, viewer.CivilizationId);
```

Pass these lists into the `Observation` constructor (cast to `IReadOnlyList<T>` as
needed). Do not change `Build(world, civilizationId)`; it keeps its own behavior.

Run: `dotnet test tests/SeedField.Simulation.Tests --filter DeviceObservation`
Expected: all pass, including the existing compact/full view tests.

- [ ] **Step 5: Measure the sequential and parallel benchmarks again**

```powershell
dotnet build SeedField.slnx -c Release
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint 100 > logs/bench-jint-100-after.log 2>&1
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint-parallel 100 --parallel-dof 16 > logs/bench-parallel-100-after.log 2>&1
```

Record the phase numbers. Keep the change only if total time improves or memory
does not regress; revert the slice cache if it is neutral and adds complexity.

- [ ] **Step 6: Commit**

```powershell
git add src/SeedField.Runtime.Js/JsController.cs src/SeedField.Simulation/ObservationCache.cs src/SeedField.Simulation/ObservationBuilder.cs src/SeedField.Simulation/ObservationShare.cs tests/SeedField.Runtime.Js.Tests/JsControllerTests.cs
git commit -m "perf(perf): SeedField 控制器热路径与共享观测切片"
```

### Task 6: Evidence, ADR, CI gate, Godot smoke trigger

**Files:**
- Create: `docs/engineering/adr/ADR-0028-parallel-controller-batch.md`
- Create: `docs/engineering/evidence/2026-11-17-performance-parallel-controllers.md`
- Modify: `docs/engineering/IMPLEMENTATION_BASELINE.md` (ADR table and status)
- Modify: `.github/workflows/ci.yml`
- Modify: `.github/workflows/godot-smoke.yml`

- [ ] **Step 1: Write the ADR and evidence**

ADR-0028 records: measured baseline, why constraints/direct objects were rejected,
the parallel batch decision, deterministic ordering, deployment/tick separation,
and the rejection of C++/native engines for this pass. Evidence records the
before/after tables from Tasks 4 and 5, including 100 and 1000 devices, DOF, and
process memory.

- [ ] **Step 2: Add the CI perf job**

In `ci.yml`, add a `perf` job on `windows-latest` after `build-test`:

```yaml
  perf:
    name: perf · jint
    runs-on: windows-latest
    timeout-minutes: 15

    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: 10.0.x
      - name: Build
        run: dotnet build SeedField.slnx -c Release
      - name: Parallel controller budget
        shell: pwsh
        run: |
          dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -c Release --no-build -- --benchmark-jint-parallel 100 --parallel-dof 8 --budget-ms 120
```

Use `--parallel-dof 8` on the 4-core Windows runner to avoid oversubscription;
the threshold is intentionally looser than the dev baseline.

- [ ] **Step 3: Trigger Godot smoke for game changes**

In `godot-smoke.yml`, add:

```yaml
on:
  workflow_dispatch:
  pull_request:
    paths:
      - "game/**"
      - "src/SeedField.Simulation/**"
```

- [ ] **Step 4: Update the baseline and verify docs**

Add ADR-0028 to the ADR table and the implementation status list. Run:

```powershell
dotnet test SeedField.slnx
python tools/refresh_design_manifest.py --check
```

Expected: all tests pass; manifest unchanged (no design-bible edits).

- [ ] **Step 5: Commit**

```powershell
git add docs/engineering/adr/ADR-0028-parallel-controller-batch.md docs/engineering/evidence/2026-11-17-performance-parallel-controllers.md docs/engineering/IMPLEMENTATION_BASELINE.md .github/workflows/ci.yml .github/workflows/godot-smoke.yml
git commit -m "docs(perf): SeedField 并行控制器证据与门禁"
```

### Task 7: Final verification

- [ ] **Step 1: Build and test**

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

Expected: 0 warnings, 0 errors; 336+ tests pass.

- [ ] **Step 2: Re-run the benchmarks and record final numbers**

```powershell
dotnet build SeedField.slnx -c Release
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint 100
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint-parallel 100 --parallel-dof 16
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint-parallel 1000 --parallel-dof 16
```

- [ ] **Step 3: Report using the AGENTS delivery format**

Include: purpose, ADR IDs, schema (none), files, build command, tests, performance/memory,
unverified risks, next/rollback.

## Verification

- `dotnet build SeedField.slnx` and `dotnet test SeedField.slnx` pass.
- The byte-limit test rejects oversized observations before parsing.
- Device observation tests still pass with the cached per-region slices.
- Release benchmarks are recorded in the evidence file.

## Rollback

Revert the hot-path commit; `JsController` returns to the name lookup and
full byte count, and `BuildForEntity` returns to inline LINQ. No schema or script
contract changes are involved.
