# Parallel Controller Batch — Hot Path and Gates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the conservative hot path, evidence, ADR, CI performance gate, and Godot smoke trigger for the parallel controller batch.

**Architecture:** Keep the JSON boundary and per-device engines; cache the harness entry point, add a byte-limit fast path, and cache per-region observation slices. Then document and gate the measured result.

**Tech Stack:** .NET 10, C#, Jint 4.18.0, xUnit, GitHub Actions.

---

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
