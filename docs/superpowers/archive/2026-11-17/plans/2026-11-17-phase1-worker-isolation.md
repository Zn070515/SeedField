# SeedField Phase 1 Worker Isolation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Run the opponent controller in a separate worker process with framed IPC and OS resource limits, while the simulation remains authoritative and third-party import stays closed.

**Architecture:** Parent (`SimulationEngine` host) owns the world and serializes a filtered `Observation`; a per-controller worker process hosts Jint and returns raw intent JSON. A length-prefixed stdin/stdout protocol carries `hello/ready/observe/intents/fault/shutdown`. Windows Job Objects cap memory/process count and kill the worker; Jint still caps statements/memory/time.

**Tech Stack:** .NET 10.0, Jint 4.18.0, xUnit, Windows Job Objects (P/Invoke), anonymous stdin/stdout pipes.

---

## File Map

| File | Responsibility |
|---|---|
| `src/SeedField.Runtime.Js/IController.cs` | Controller contract + factory delegate |
| `src/SeedField.Runtime.Js/JsController.cs` | Add public observation/decision JSON methods; implement `IController` |
| `src/SeedField.Runtime.Js/ControllerSlot.cs` | Accept a controller factory |
| `src/SeedField.Runtime.Js/DeviceControllerSlot.cs` | Pass factory through |
| `src/SeedField.Runtime.Js/Worker/IpcFraming.cs` | 4-byte length-prefixed UTF-8 frames |
| `src/SeedField.Runtime.Js/Worker/WorkerEnvelope.cs` | Protocol DTO + JSON options |
| `src/SeedField.Runtime.Js/Worker/WorkerController.cs` | Parent-side process controller |
| `src/SeedField.Runtime.Js/Worker/WorkerLocator.cs` | Find the worker executable |
| `src/SeedField.Runtime.Js/Worker/WindowsJobObject.cs` | Job Object limits (Windows) |
| `src/SeedField.Worker/SeedField.Worker.csproj` | Worker executable project |
| `src/SeedField.Worker/Program.cs` | Worker protocol loop |
| `src/SeedField.Headless/Program.cs` | `--worker-opponent` scenario |
| `tests/SeedField.Runtime.Js.Tests/IpcFramingTests.cs` | Framing tests |
| `tests/SeedField.Runtime.Js.Tests/WorkerControllerTests.cs` | Worker lifecycle/fault/determinism |
| `tests/SeedField.Runtime.Js.Tests/ControllerFactoryTests.cs` | Factory wiring |
| `docs/engineering/adr/ADR-0021-worker-isolation-phase1.md` | Decision/evidence |
| `docs/engineering/evidence/2026-11-17-worker-isolation.md` | Commands/results |

---

## Task 1: Public controller contract and JSON boundary

**Files:** Create `IController.cs`; modify `JsController.cs`, `ControllerSlot.cs`, `DeviceControllerSlot.cs`; test `ControllerFactoryTests.cs`.

- [ ] **Step 1: Write the failing test**
```csharp
[Fact]
public void ControllerSlotUsesTheProvidedFactory()
{
    var created = 0;
    var slot = new ControllerSlot("A", "function decide(){return [];}",
        factory: (civ, script, options) => { created++; return new FakeController(civ); });
    slot.Decide(EmptyObservation());
    Assert.Equal(1, created);
}
```
`FakeController` returns an empty `ControllerDecision`.

- [ ] **Step 2: Run and verify failure**
Run: `dotnet test tests/SeedField.Runtime.Js.Tests --filter ControllerSlotUsesTheProvidedFactory`
Expected: compile error (no factory parameter).

- [ ] **Step 3: Implement contract**
```csharp
public interface IController : IDisposable
{
    string CivilizationId { get; }
    ControllerDecision Decide(Observation observation);
}

public delegate IController ControllerFactory(
    string civilizationId, string script, JsControllerOptions? options);
```
- `JsController : IController`.
- `ControllerSlot` ctor adds `ControllerFactory? factory = null`, stores `_factory ?? ((c, s, o) => new JsController(c, s, o))`; `Deploy` uses it.
- `DeviceControllerSlot` adds and forwards the factory.

- [ ] **Step 4: Expose JSON helpers**
```csharp
public static string SerializeObservation(Observation observation) =>
    ObservationJson.Serialize(observation);

public static ControllerDecision ParseDecisionJson(string json, string civilizationId) =>
    ParseDecision(json, civilizationId);

public ControllerRunResult RunJson(string observationJson)
```
`ControllerRunResult(bool Success, string? IntentsJson, string? FaultCode, string? FaultMessage)`.
`Decide(observation)` becomes: serialize → `RunJson` → parse/fault.

- [ ] **Step 5: Run tests and commit**
Run: `dotnet test SeedField.slnx`
Expected: all pass.
```bash
git add src/SeedField.Runtime.Js tests/SeedField.Runtime.Js.Tests
git commit -m "refactor(js): SeedField 控制器工厂与 JSON 边界"
```

---

## Task 2: Framed IPC

**Files:** Create `src/SeedField.Runtime.Js/Worker/IpcFraming.cs`, `WorkerEnvelope.cs`; test `IpcFramingTests.cs`.

- [ ] **Step 1: Write failing tests**
```csharp
[Fact] public void FrameRoundTripsUtf8() {
    var stream = new MemoryStream();
    IpcFraming.Write(stream, "{\"kind\":\"observe\"}");
    stream.Position = 0;
    Assert.Equal("{\"kind\":\"observe\"}", IpcFraming.Read(stream));
}
[Fact] public void OversizedFrameIsRejected() {
    var stream = new MemoryStream();
    IpcFraming.Write(stream, new string('a', IpcFraming.MaxFrameBytes + 1));
    stream.Position = 0;
    Assert.Throws<IpcProtocolException>(() => IpcFraming.Read(stream));
}
```
Add truncated-frame and EOF tests.

- [ ] **Step 2: Run and verify failure**
Run: `dotnet test tests/SeedField.Runtime.Js.Tests --filter IpcFraming`
Expected: compile error.

- [ ] **Step 3: Implement**
```csharp
public static class IpcFraming
{
    public const int MaxFrameBytes = 4 * 1024 * 1024;
    public static void Write(Stream stream, string text) { /* length LE + UTF8 */ }
    public static string? Read(Stream stream) { /* EOF -> null; bad length -> throw */ }
}
```
Length prefix: `BinaryPrimitives.WriteInt32LittleEndian`; strict UTF-8 (`new UTF8Encoding(false, true)`); length 0..Max.

- [ ] **Step 4: Envelope**
```csharp
public sealed record WorkerEnvelope(
    int ProtocolVersion = 1,
    string Kind = "",
    string? CivilizationId = null,
    string? Script = null,
    int? MaxStatements = null,
    long? MemoryLimitBytes = null,
    int? TimeoutMs = null,
    long? Id = null,
    string? ObservationJson = null,
    string? IntentsJson = null,
    string? Code = null,
    string? Message = null);
```
JSON: camelCase, ignore nulls, strict max depth 16.

- [ ] **Step 5: Run tests and commit**
```bash
dotnet test tests/SeedField.Runtime.Js.Tests --filter "Ipc|Envelope"
git commit -m "feat(js): SeedField 工作进程 IPC 帧"
```

---

## Task 3: Worker executable

**Files:** Create `src/SeedField.Worker/SeedField.Worker.csproj`, `Program.cs`; modify `SeedField.slnx`.

- [ ] **Step 1: Create project**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>SeedField.Worker</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\SeedField.Runtime.Js\SeedField.Runtime.Js.csproj" />
  </ItemGroup>
</Project>
```
Add to `SeedField.slnx` under `/src/`.

- [ ] **Step 2: Protocol loop**
`Program.Main`:
1. `var input = Console.OpenStandardInput(); var output = Console.OpenStandardOutput();`
2. Loop `IpcFraming.Read(input)`; parse envelope; switch `kind`:
   - `hello`: `new JsController(civ, script, options)`; reply `ready`; on error reply `fault` with `Id=0`.
   - `observe`: `controller.RunJson(obsJson)`; reply `intents` or `fault` with same `Id`.
   - `shutdown`: dispose + return 0.
   - unknown: reply `fault` `IPC_UNKNOWN_KIND`; continue.
3. Catch `IpcProtocolException`/`JsonException`: write fault then exit 2.
4. Never write human text to stdout; diagnostics to stderr.

- [ ] **Step 3: Build**
Run: `dotnet build SeedField.slnx`
Expected: 0 warnings/errors.

- [ ] **Step 4: Commit**
```bash
git add src/SeedField.Worker SeedField.slnx
git commit -m "feat(worker): SeedField 隔离工作进程"
```

---

## Task 4: Parent worker controller and Job Object

**Files:** Create `WorkerController.cs`, `WorkerLocator.cs`, `WindowsJobObject.cs`; test `WorkerControllerTests.cs`.

- [ ] **Step 1: Failing tests**
```csharp
[Fact] public void WorkerReturnsIntents() {
    using var worker = NewWorker("function decide(obs){return [{type:'wait',entityId:obs.viewerEntityId||1}];}");
    var decision = worker.Decide(ObservationForEntity1());
    Assert.Single(decision.Intents);
    Assert.IsType<WaitIntent>(decision.Intents[0]);
}
[Fact] public void InfiniteLoopReturnsTimeoutFault() {
    using var worker = NewWorker("function decide(){ while(true){} }",
        new WorkerControllerOptions { DecisionTimeoutMs = 2_000, ControllerOptions = new JsControllerOptions { Timeout = TimeSpan.FromMilliseconds(100) } });
    Assert.Contains(worker.Decide(ObservationForEntity1()).Faults,
        fault => fault.Code == ScriptFaultCodes.Timeout);
}
[Fact] public void DisposeStopsTheWorker() { /* HasExited */ }
```
Add a same-observation determinism test (two calls → same intent count/type).

- [ ] **Step 2: Implement process lifecycle**
`WorkerControllerOptions`: `ExecutablePath`, `StartupTimeoutMs=5000`, `DecisionTimeoutMs=500`, `JobMemoryLimitBytes=128MB`, `RequireJobObject=false`, `ControllerOptions`.
`WorkerLocator.Find()`:
1. `AppContext.BaseDirectory/SeedField.Worker.exe`;
2. walk up to `SeedField.slnx`, then `src/SeedField.Worker/bin/{Debug|Release}/net10.0/SeedField.Worker.exe`;
3. throw `WorkerIsolationException` with searched paths.

`WorkerController` ctor:
- start `Process` with `UseShellExecute=false`, `CreateNoWindow=true`, redirect stdin/stdout/stderr, working dir temp subdir.
- assign Job Object; set `JobObjectAssigned`.
- send `hello`; wait `ready` with `StartupTimeoutMs`.
- `Decide`: serialize observation; send `observe` with monotonic id; read `intents`/`fault` with `DecisionTimeoutMs`; parse via `JsController.ParseDecisionJson`.
- On timeout/EOF/process exit: kill, return `Fault("SCRIPT_TIMEOUT"/"SCRIPT_FAULT", ...)`; next `Decide` restarts once.
- `Dispose`: send `shutdown`, close stdin, wait 300 ms, kill, dispose job.

- [ ] **Step 3: Job Object**
`WindowsJobObject` P/Invoke:
- `CreateJobObject`, `SetInformationJobObject`, `AssignProcessToJobObject`, `CloseHandle`.
- Flags: `PROCESS_MEMORY 0x100 | JOB_MEMORY 0x200 | ACTIVE_PROCESS 0x8 | KILL_ON_JOB_CLOSE 0x2000`.
- On non-Windows: no-op, `Assigned=false`.
- If assignment fails and `RequireJobObject` is true, throw; otherwise continue (dev fallback) and record `IsolationDegraded`.

- [ ] **Step 4: Run tests**
Run: `dotnet test tests/SeedField.Runtime.Js.Tests --filter WorkerController`
Expected: pass.
If worker not found: build solution first; locator expects `bin/Debug/net10.0/SeedField.Worker.exe`.

- [ ] **Step 5: Commit**
```bash
git commit -m "feat(worker): SeedField 父进程工作控制器与 Job Object"
```

---

## Task 5: Headless worker integration

**Files:** Modify `src/SeedField.Headless/Program.cs`; test `WorkerHeadlessTests.cs` or run CLI.

- [ ] **Step 1: Add flag**
`--worker-opponent` runs the 600-tick scenario with B's `ControllerSlot` using:
```csharp
factory: (civ, script, options) => new WorkerController(civ, script,
    new WorkerControllerOptions { ControllerOptions = options ?? new JsControllerOptions() })
```
A stays in-process.

- [ ] **Step 2: Print comparison**
Run in-process and worker scenarios; print:
```text
WORKER_OPPONENT inProcess=<hash> worker=<hash> match=<bool>
```
Return 0 only when hashes match and both runs are reproducible.

- [ ] **Step 3: Run**
```powershell
dotnet run --project src/SeedField.Headless -- --worker-opponent
```
Expected: `match=True`, exit 0.

- [ ] **Step 4: Commit**
```bash
git commit -m "feat(headless): SeedField 工作进程对手演示"
```

---

## Task 6: Docs, evidence, gates

**Files:** Create `docs/engineering/adr/ADR-0021-worker-isolation-phase1.md`, `docs/engineering/evidence/2026-11-17-worker-isolation.md`; modify `README.md`, `docs/engineering/IMPLEMENTATION_BASELINE.md`, `SECURITY.md`.

- [ ] **Step 1: ADR-0021**
Record: parent authority, protocol, limits, worker lifetime, allowlist-only scope, non-goals (AppContainer/network denial still Phase 2), rollback (`--worker-opponent` off).
- [ ] **Step 2: Evidence**
Commands, test counts, hash match, timeout test, worker executable path, known gaps.
- [ ] **Step 3: Update docs**
README flags/evidence rows; baseline; SECURITY gate wording; KB project note.
- [ ] **Step 4: Verify full gate**
```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless -- --worker-opponent
dotnet run --project src/SeedField.Headless
godot --headless --path game/SeedField.Game -- --save-smoke
```
- [ ] **Step 5: Commit**
```bash
git commit -m "docs: SeedField Phase 1 工作进程隔离证据"
```

---

## Verification Checklist

- [ ] `ThirdPartyImportEnabled` still `false`.
- [ ] No new file/network/process API in `src/SeedField.Runtime.Js/Opponents`.
- [ ] Worker process killed on dispose; timeout returns fault, parent stays responsive.
- [ ] In-process and worker 600-tick hashes match.
- [ ] All `.cs`/`.md` ≤ 600 lines.
- [ ] `dotnet test SeedField.slnx` and CI green.

## Risks / Rollback

- Job Object unavailable/nested-job failure → `IsolationDegraded=true`; Phase 1 remains allowlist-only, so no untrusted package runs.
- Worker startup cost per controller; acceptable for 2 civilizations, revisit for device-level workers.
- IPC timeout too tight on slow CI → default 500 ms; raise via options if needed.
- Rollback: keep `--worker-opponent` opt-in; in-process path remains default.
