# 证据：确定性随机与断路器随机故障（2026-11-17）

关联：ADR-0014、ADR-0013。

## 1. 世界随机流

- `DeterministicRandom` 使用 SplitMix64；`Seed` 与 `State` 都属于世界状态。
- `WorldState.Random` 是唯一实例；`WorldBuilder.SetRandomSeed` 设置场景种子。
- `NextUInt64` / `NextInt` / `Percent`；`Percent(0)` 不消耗随机值。
- 已知向量（seed 0）：`e220a8397b1dcdaf`、`6e789e6aa1b965f4`、`06c45d188009454f`。
- 实测：同种子 16 次输出一致；不同种子首个输出不同。

## 2. 随机断路器故障

- `PowerPolicy.BreakerFaultChancePercent`（0..100，默认 0）。
- 每次故障跳闸判定一次；0% 不取随机数，100% 必然锁定。
- 命中后立即 `MaintenanceRequired=true`，`BreakerLockedOut.Reason="fault"`；
  未命中时仍按 `BreakerTripLimit` 锁定，`Reason="trips"`。
- 维护流程不变：`MaintainBreakerCommand` 清除锁定与计数，但不自动合闸。
- 实测：seed 42 + 100% 故障 → 首次跳闸即锁定，随机状态从 42 推进到
  `9e3779b97f4a7c3f`；存档往返后种子/状态一致。

## 3. 存档与哈希

- schema v12 → v13：`RandomSeed`、`RandomState`、`BreakerFaultChancePercent`。
- 旧 v12 存档迁移：随机种子/状态 0、故障概率 0。
- 哈希新增随机种子/状态与故障概率。
- 实测：`--power-fault` 的 `hashOk=True`；主切片双跑哈希一致。
- 踩坑：首次实现漏把 `BreakerFaultChancePercent` 写进 `ToData`，`hashOk=False`；
  补齐后通过。这是“新增字段必须同步 ToData/FromData/哈希”的又一次实证。

## 4. 表现

- F4 摘要新增 `fault X%`；区域行 `MAINT!` 与事件日志的锁定原因沿用 v12。
- 重新截图 `game/SeedField.Game/screenshots/m0-power.png`，确认
  `fault 0%`、`trip≤∞`、`maint 0M`、`trips0` 与 `MAINTAIN` 均完整显示。

## 5. 验证命令与结果

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --power-fault
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --power-maintenance
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj
godot --headless --path game/SeedField.Game -- --save-smoke
godot --headless --path game/SeedField.Game -- --power-breaker-smoke
godot --path game/SeedField.Game -- --capture-power
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Repository.Tests: 6 passed
SeedField.Simulation.Tests: 189 passed
SeedField.Runtime.Js.Tests: 48 passed
```

```text
POWER_FAULT lockedOut=True reason=fault maintenance=True seed=42 state=9e3779b97f4a7c3f randomOk=True hashOk=True ok=True
POWER_MAINTENANCE firstTrip=True reclosed=True lockedOut=True closeBlocked=True maintained=True closed=True hashOk=True ok=True
POWER_BREAKER tripped=True drawBlocked=True heldBelowReserve=True autoClosed=True manualHeld=True manualClosed=True ok=True
```

```text
Run 1 final hash: 9e86aabc336e8ab8
Run 2 final hash: 9e86aabc336e8ab8
REPRODUCIBLE: identical state hash across both runs.
```

```text
SAVE_SMOKE ok=True before=46325180ab79f6d1 mutated=ef8bab4fafdef03e after=46325180ab79f6d1 tick=39 A=v1 B=v1
POWER_BREAKER_SMOKE opened=True held=True closed=True maintained=True ok=True
```

## 6. 未覆盖

- 随机战斗命中/伤害、掉落、AI 决策与地图生成。
- 维护时长、备件与远程维护。
- 长时间随机流回放（当前只验证种子/状态往返与短路流）。
- 算法版本迁移：一旦更换 PRNG，需要新 ADR 与存档迁移。
