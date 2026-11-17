# SeedField Phase 2 Package Trust Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Add signed offline-package loading, explicit consent, CPU/disk limits and IPC fuzzing on top of Phase 1, without opening third-party execution until OS containment is independently verified.

**Architecture:** A new host-only `SeedField.PackageHost` validates a local bundle directory (manifest + JS), checks content hash and an ECDSA P-256 signature against a trust store, and requires explicit consent. The worker gains Job Object CPU time and a temp-directory disk watchdog. `ThirdPartyImportEnabled` stays `false`; no game path executes loaded packages.

**Tech Stack:** .NET 10.0, ECDSA P-256/SHA-256, `System.Text.Json`, Windows Job Objects, xUnit.

---

## File Map

| File | Responsibility |
|---|---|
| `src/SeedField.PackageHost/SeedField.PackageHost.csproj` | Host-only package loading project |
| `src/SeedField.PackageHost/PackageTrustStore.cs` | Key-id → public key allowlist |
| `src/SeedField.PackageHost/PackageSignature.cs` | Canonical manifest, sign/verify |
| `src/SeedField.PackageHost/OpponentPackageLoader.cs` | Directory load, hash/signature/consent |
| `src/SeedField.PackageHost/PackageImportConsent.cs` | Confirmed/allow-unsigned record |
| `src/SeedField.Runtime.Js/Opponents/OpponentPackage.cs` | Expose safe-path/hash helpers; signature fields |
| `src/SeedField.Runtime.Js/Worker/DirectorySizeGuard.cs` | Bounded recursive size |
| `src/SeedField.Runtime.Js/Worker/WindowsJobObject.cs` | Add process CPU time limit |
| `src/SeedField.Runtime.Js/Worker/WorkerController.cs` | CPU option, disk watchdog |
| `tests/SeedField.Runtime.Js.Tests/PackageHostTests.cs` | Signature/hash/consent/path tests |
| `tests/SeedField.Runtime.Js.Tests/IpcFuzzingTests.cs` | Deterministic frame/envelope fuzz |
| `tests/SeedField.Runtime.Js.Tests/DirectorySizeGuardTests.cs` | Disk guard tests |
| `src/SeedField.Headless/Program.cs` | `--package-check <dir>` consent CLI |
| `docs/engineering/adr/ADR-0022-package-trust-and-phase2-gates.md` | Decision/evidence |
| `docs/engineering/evidence/2026-11-17-package-trust.md` | Commands/results |

---

## Task 1: Package host + signatures

- [ ] **Step 1: Extend manifest**
```csharp
public sealed record OpponentPackageManifest(
    int SchemaVersion,
    string PackageId,
    string Version,
    string Kind,
    string EngineCompat,
    string[] EntryPoints,
    string[] RequiredCapabilities,
    string ContentHash,
    string SignerKeyId = "",
    string Signature = "");
```
Expose:
```csharp
public static bool IsSafeRelativePath(string path)
public static string ComputeContentHash(IReadOnlyDictionary<string, string> programs)
```

- [ ] **Step 2: PackageSignature**
```csharp
public static string Canonicalize(OpponentPackageManifest manifest);
public static OpponentPackageManifest Sign(
    OpponentPackageManifest manifest, ECDsa privateKey, string keyId);
public static bool Verify(OpponentPackageManifest manifest, ECDsa publicKey);
```
Canonical JSON sorts entryPoints/capabilities and includes contentHash (not signature/keyId).

- [ ] **Step 3: Trust store**
```csharp
public sealed class PackageTrustStore
{
    public PackageTrustStore Add(string keyId, ECDsa publicKey);
    public bool TryGet(string keyId, out ECDsa publicKey);
    public static PackageTrustStore Empty { get; }
}
```

- [ ] **Step 4: Tests**
- Sign/verify round-trip.
- Tamper a program → `CONTENT_HASH_MISMATCH`.
- Tamper signature → `INVALID_SIGNATURE`.
- Unknown key → `UNTRUSTED_SIGNER`.
- Unsigned without consent/allow → `UNSIGNED_PACKAGE`.
- Unsigned with `UserConfirmed && AllowUnsigned` → `TrustLevel.Unsigned`.
- Traversal/oversize/invalid UTF8 rejected.

- [ ] **Step 5: Commit**
```bash
git commit -m "feat(package): SeedField 签名包与信任存储"
```

---

## Task 2: Loader + consent

- [ ] **Step 1: Consent and result**
```csharp
public sealed record PackageImportConsent(bool UserConfirmed, bool AllowUnsigned);
public enum PackageTrustLevel { Signed, Unsigned }
public sealed record LoadedOpponentPackage(
    OpponentPackage Package, PackageTrustLevel Trust, string SignerKeyId);
```

- [ ] **Step 2: Loader**
`Load(directory, trustStore, consent)`:
1. Require `UserConfirmed`.
2. Read `manifest.json` (≤64 KiB, strict UTF8).
3. Enumerate files; only `manifest.json` and entry programs; reject other files/reparse points.
4. Read programs (strict UTF8, entry paths safe); compute content hash; compare declared.
5. Verify signature if present; otherwise require `AllowUnsigned`.
6. `OpponentPackage.Create(manifest, programs)`.

- [ ] **Step 3: Tests**
Directory temp fixture; signed load, tamper, unsigned consent, extra hidden file, reparse skipped.

- [ ] **Step 4: Lint**
`PackageImportBoundaryTests`:
- `ThirdPartyImportEnabled` remains false.
- `SeedField.PackageHost` has no `HttpClient`/`Socket`/`WebRequest`/`Process.Start`.

- [ ] **Step 5: Commit**
```bash
git commit -m "feat(package): SeedField 本地包加载与显式同意"
```

---

## Task 3: CPU/disk/fuzz hard limits

- [ ] **Step 1: Job CPU**
`WorkerControllerOptions.JobCpuLimitMs = 30_000`; validate.
`WindowsJobObject` sets `PerProcessUserTimeLimit = -(ms * 10_000)` and
`JOB_OBJECT_LIMIT_PROCESS_TIME = 0x2`.

- [ ] **Step 2: Disk guard**
```csharp
public static long ComputeBytes(string root, long stopAt);
```
Skips reparse points/errors, stops once `stopAt` exceeded.
`WorkerControllerOptions.DiskLimitBytes = 8 * 1024 * 1024`.
WorkerController starts a 250 ms watchdog; on exceed, records `DISK_LIMIT` and kills.

- [ ] **Step 3: Tests**
- DirectorySizeGuard: files/size/stopAt/reparse-safe.
- Options validation.
- CPU/disk OS behavior is evidence-level (no flaky timing test).

- [ ] **Step 4: Fuzz**
`IpcFuzzingTests`: 200 deterministic random frames and envelopes; only
`IpcProtocolException` or success, no other exception.

- [ ] **Step 5: Commit**
```bash
git commit -m "feat(worker): SeedField CPU/磁盘限额与 IPC fuzz"
```

---

## Task 4: Authorization CLI + docs

- [ ] **Step 1: CLI**
`--package-check <directory> [--allow-unsigned] [--confirm]`:
- loads with `PackageTrustStore.Empty` + consent;
- prints package id/version/trust/content hash;
- **never executes**; prints `EXECUTION_DISABLED` because `ThirdPartyImportEnabled=false`.

- [ ] **Step 2: ADR-0022**
Record what is done (signatures, consent, CPU/disk, fuzz) and what remains
(AppContainer, OS network/file denial, read-only mount, independent review).
Import stays closed.

- [ ] **Step 3: Evidence**
Commands, test counts, package-check output, known gaps.

- [ ] **Step 4: Docs**
README flag/evidence; baseline; SECURITY; threat-model gate status; KB.

- [ ] **Step 5: Verify**
```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless -- --worker-opponent
dotnet run --project src/SeedField.Headless -- --package-check <temp-package> --allow-unsigned --confirm
```

---

## Verification

- `dotnet build SeedField.slnx` and `dotnet test SeedField.slnx` are green.
- `--worker-opponent` still matches the in-process 600 Tick hash.
- `--package-check` rejects missing consent and shows `EXECUTION_DISABLED`.
- `ThirdPartyImportEnabled` remains `false`; PackageHost has no network/process API.
- All `.cs`/`.md` files stay at or below 600 lines.

## Hard stop

Do **not** set `ThirdPartyImportEnabled = true`. AppContainer, OS network/file denial,
read-only package mount, independent security review and a real release signing key
are not implemented; the loader is a validation/consent surface only.

## Rollback

Remove `SeedField.PackageHost` from the solution/UI; Phase 1 worker remains. No game
path runs loaded packages, so rollback has no state or save impact.
