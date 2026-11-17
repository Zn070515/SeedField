# 证据：核心区恢复配额与恢复债务（2026-11-17）

关联：ADR-0015、ADR-0002、ADR-0013。调研背景见
`PersonalKnowledgeBase/03-Resources/游戏设计_核心区恢复围困与离线对手调研.md`。

## 1. 配置与默认值

| 字段 | 默认 | 行为 |
|---|---|---|
| `RecoveryWindowTicks` | 0 | 0 = 关闭配额 |
| `RecoveryQuotaPerWindow` | 0 | 每窗口快速恢复次数 |
| `RecoveryCostGrowthPercent` | 0 | 连续恢复成本增长 |
| `RecoveryStreakDecayTicks` | 0 | 0 = 债务永不衰减 |
| `RecoveryOverdraftCostPercent` | 0 | 0 = 配额耗尽硬拒绝；>0 = 慢速模式 |

- 默认值下现有核心恢复行为不变（现有 `CoreRecoveryTests`/`RecoveryCommandTests` 全绿）。

## 2. 配额

- 窗口由 `RecoveryWindowStartTick` / `RecoveriesInWindow` 记录，每 Tick 刷新。
- 配额内正常恢复；耗尽时：
  - 无透支配置 → `RecoveryQuotaExhausted` + `RecoveryQuotaDenied`
    （`Used/Limit/ResetsInTicks`）；
  - 有透支配置 → 恢复成功但成本增加。
- 实测：窗口 10、配额 2、成本 40/30 → 前两次成功；第三次
  `RecoveryQuotaExhausted`（tick 1，剩 9 Tick）；10 Tick 后窗口重置，
  下一次成功且 `RecoveriesInWindow=1`。
- 实测（慢速模式）：窗口 10、配额 1、透支 200%、成本 10/10 →
  第一次 10/10，第二次 30/30，仍成功，`RecoveriesInWindow=2`。

## 3. 恢复债务

- 连续恢复乘数 `100 + growth × streak`，向上取整应用到 E/M。
- `RecoveryStreakDecayTicks` 无恢复后清零。
- 实测：growth 50%、decay 20 → 第一次 40/30，第二次 60/45；
  第三次因储备不足 `NoEnergy`；20 Tick 后债务清零，下一次回到 40/30。
- `EntityBootstrapped`、`EnergySpentTotal`/`MaterialSpentTotal` 与储备扣减
  都使用缩放后成本。

## 4. 入口覆盖

- `TryBootstrapFromCore` 与 `TryRestoreLatestProduct` 都检查同一配额与债务。
- 实测：`RestoreCoreProductCommand` 配额 1 → 第一次成功，第二次
  `RecoveryQuotaExhausted` 且携带 `RecoveryQuotaDenied`。
- `ExecuteRecoveryCommand` 仍然只是宿主入口，不能绕过。

## 5. 存档与哈希

- schema v13 → v14：`CoreSaveData` 新增窗口/配额/增长/衰减/透支与
  `RecoveryWindowStartTick`、`RecoveriesInWindow`、`RecoveryStreak`、
  `LastRecoveryTick`。
- 旧 v13 存档默认全部 0、`LastRecoveryTick=-1`。
- 加载校验：字段非负；恢复 Tick/窗口起点不得晚于存档 Tick；
  无透支时窗口内次数不得超过配额。
- 实测：配额/债务/窗口状态存档往返哈希一致，读档后继续同一窗口与债务。

## 6. 验证命令与结果

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --recovery-quota
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj
godot --headless --path game/SeedField.Game -- --save-smoke
godot --headless --path game/SeedField.Game -- --power-breaker-smoke
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Repository.Tests: 6 passed
SeedField.Simulation.Tests: 198 passed
SeedField.Runtime.Js.Tests: 48 passed
```

```text
RECOVERY_QUOTA first=True second=True slowMode=True afterWindow=True windowStart=10 used=1 streak=0 hashOk=True ok=True
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

## 7. 未覆盖

- 核心区“外壳层”（可摧毁锚点/工业壳）与围困对恢复速度的影响。
- 残骸回收、离线文明废墟、恢复 UI 面板。
- 具体平衡数值：R-04 仍 OPEN；当前全部字段是场景配置。
- 长时间多文明恢复压力测试。
