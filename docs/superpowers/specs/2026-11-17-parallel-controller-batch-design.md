# Parallel Controller Batch — Design

> Status: [PROPOSED]  
> Date: 2026-11-17  
> Related: ADR-0027 (spatial observation), ADR-0023 (structural gates)

## Problem

`--benchmark-jint 100` (Release, dev machine) spends 31.3 ms per tick:

| Phase | ms/tick | Share |
|---|---:|---:|
| Observation build | 1.3 | 4% |
| Observation JSON serialize | 2.2 | 7% |
| Jint `decide` | 28.7 | 89% |

Each device sees 1–65 entities (avg 29.1) because the benchmark's 6 km sensors
overlap a dense foreign formation; average observation JSON is 6.9 KiB.

Probe results (outside the repo, Jint 4.18.0, 100 independent engines):

- Jint's statement/time/memory constraints cost only ~10–15%; they are not the
  bottleneck.
- Injecting a prepared Jint object graph instead of `JSON.parse` is slower at
  realistic sizes (65 entities: ~16 ms vs ~10.6 ms per 100 calls), so the JSON
  boundary stays.
- Running the same 100 `RunJson` calls on 16 threads costs 3.2 ms vs 20.7 ms
  sequential (6.5×), with identical inputs and per-engine instances.

The correct lever is therefore **parallel execution of independent controllers**,
not a native JSON/observation rewrite.

## Goals

1. Keep the 60 Hz worst case: 100 always-active devices at ≤16.7 ms/tick
   (Release, 32-thread dev machine).
2. Target 1000 devices at ≤167 ms/tick (the game currently runs at 6 ticks/s).
3. Preserve semantics exactly:
   - every controller is still called once per tick;
   - intents are still submitted in stable device order;
   - no wall clock or floating-point settlement enters the simulation;
   - one `SimulationEngine.Step` remains the only world mutation.
4. Spend as little CPU and memory as possible: bounded parallelism, no
   oversubscription, sequential fallback for small batches.

## Non-Goals

- No default sleep/idle cadence. It changes when scripts are called and is not
  needed once execution is parallel.
- No native (C++/QuickJS/V8) JavaScript engine. Measurement shows the Jint
  interpreter is not replaceable within this scope without a new ADR.
- No direct Jint object injection for observations. Measured slower.
- No change to observation JSON schema or save schema.

## Design

### 1. `ControllerBatchRunner` (SeedField.Runtime.Js)

A small, testable executor for one tick's controller work:

```csharp
public sealed record ControllerBatchItem(
    int Index, DeviceControllerSlot Controller, Observation Observation);

public sealed class ControllerBatchRunner
{
    public ControllerBatchRunner(ControllerExecutionOptions? options = null);
    public IReadOnlyList<ControllerDecision> Run(IReadOnlyList<ControllerBatchItem> items);
}
```

Behavior:

- Output is a `ControllerDecision[]` indexed by `Index`, so callers flatten
  intents in the same order as the old `foreach`.
- Uses `Parallel.For` when `items.Count >= MinBatchSize` and
  `Environment.ProcessorCount > 1`; otherwise runs sequentially.
- `MaxDegreeOfParallelism` defaults to `Environment.ProcessorCount`, capped by
  an option (default 32) to avoid oversubscription on shared machines.
- Any exception escaping a single controller is converted to
  `ControllerDecision.Fault(ScriptFaultCodes.Fault, message)`; other items still
  run. This matches the existing "one bad script does not stop the tick"
  behavior.
- The runner never touches `WorldState`; observations are built by the caller
  before the batch and are read-only during it.
- `DeviceControllerSlot` instances are per entity (verified in Main); deployment
  happens on the main thread between ticks, so a batch never races with
  `Deploy`. This is documented in the ADR.

### 2. Main / Headless integration

- `Main.StepOnce` builds observations for all device controllers on the main
  thread, runs a batch, then appends decisions in device order before
  `engine.Step(intents)`.
- Headless `--benchmark-jint N` gains `--parallel` (and `--parallel-dof K`) to
  measure the batch path. Without the flag the old sequential loop remains, so
  before/after numbers stay comparable.
- `--benchmark-jint N --budget-ms X` exits with code 2 when the measured
  average tick exceeds `X`.

### 3. Conservative hot path

Only changes with a measured, semantics-preserving payoff:

- Cache the compiled harness entry point (`__seedfield_run`) per `JsController`
  instead of resolving it by name on every call.
- Fast-path `MaxObservationBytes`: `json.Length > MaxObservationBytes` is an
  immediate reject and `json.Length * 4 <= MaxObservationBytes` is an immediate
  accept; only the middle band calls `Encoding.UTF8.GetByteCount`.
- `ObservationCache` caches per-(region, civilization) message, shipment,
  control-point, treaty, proposal, and trade-off slices so a device observation
  does not rescan the whole inbox/diplomacy state.
- No change to `ObservationJson` output in this pass; serialization is 7% of the
  budget and is already parallelized with `Decide`.

### 4. Determinism and correctness tests

- Sequential vs parallel equivalence: a scripted scenario run twice (sequential
  runner vs parallel runner), asserting identical `TickResult.Events` (type,
  tick, ids, order) and identical `WorldStateHasher` hash.
- Batch ordering: decisions come back in input `Index` order even when
  completion order differs.
- Fault isolation: one throwing controller yields a fault decision while a
  sibling controller still produces its intent.
- Small-batch fallback: below `MinBatchSize` the runner executes in order and
  does not require a thread pool.
- Existing 336 tests stay green; golden scenario hash unchanged.

### 5. Performance gate

- Evidence document with before/after numbers for 100 and 1000 devices,
  including observation/serialize/decide phases and parallel speedup.
- CI `perf` job (Windows, Release) runs
  `--benchmark-jint 100 --parallel --budget-ms <generous>`, uploads the log, and
  fails on budget overrun. The threshold is set well above the dev-machine
  result to absorb shared-runner variance; the dev baseline is recorded in the
  evidence file.
- Godot smoke workflow gains a PR path filter for `game/**` in addition to
  `workflow_dispatch`.

## Rollout (conservative → aggressive)

1. Conservative: hot-path items + batch runner with sequential fallback.
2. Parallel: enable the runner in Main and Headless; measure 100 devices.
3. Scale: measure 1000 devices; tune `MaxDegreeOfParallelism`, `MinBatchSize`,
   and observation-cache slices. Keep the fastest configuration that still
   passes determinism tests.
4. Gate: add `--budget-ms`, evidence, CI perf job, and Godot smoke trigger.

Each step is a separate commit with its own test and measurement. If a step
fails the equivalence test or the budget, it is reverted rather than weakened.

## Risks

| Risk | Mitigation |
|---|---|
| Parallel execution changes intent order | Index-addressed output + ordering test |
| A script fault aborts the batch | Per-item exception isolation + test |
| Deployment races with a running batch | Batch is synchronous on the main thread; deploy only between ticks |
| Shared observation cache is not thread-safe | Observations are built before the batch, sequentially |
| CI runner variance makes the gate flaky | Generous threshold, median of runs, log artifact |
| Memory blow-up with 1000 engines | Per-engine limits stay; measure process memory; cap DOF |
