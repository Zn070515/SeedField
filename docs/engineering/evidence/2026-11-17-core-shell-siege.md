# 证据：核心区外壳锚点与围困压力（2026-11-17）

关联：ADR-0016、ADR-0015、ADR-0002。

## 1. 锚点实体

- `DeviceBlueprint`/`EntityDefinition`/`EntityState` 新增 `IsCoreAnchor`。
- 锚点是普通可攻击实体；核心区仍受 `CoreProtection` 保护，锚点必须部署在核心区外的
  外壳区域。
- 实测：B 方在 shell 区域一击摧毁 A 的锚点（`EntityDestroyed`），A 的
  `AnchorTarget` 保持 1，存活锚点数变为 0。

## 2. 覆盖缺口与恢复成本

- `AnchorTarget = max(历史同时存活锚点数)`；部署/升级/Build/读档时更新。
- 缺口 = `AnchorTarget - 存活锚点数`。
- 实测：缺口 1 + `AnchorRecoveryCostPercent=50` → 恢复成本 40/30 变为 60/45。
- 重建锚点后缺口 0 → 成本回到 40/30；目标仍为 1。

## 3. 围困压力

- 判定：核心区直接链路上的区域存在非本文明、且无 NAP 的实体。
- `SiegeRecoveryCostPercent=100` → 恢复成本翻倍（40/30 → 80/60）。
- `SiegeQuotaPenalty=1`、基础配额 2 → 有效配额 1；第二次恢复返回
  `RecoveryQuotaExhausted`，事件 `Limit=1`。
- 移除敌对实体后，有效配额恢复为 2，成本回到 40/30。
- 保底再生不受影响；围困只影响快速恢复成本/配额。

## 4. 存档与哈希

- schema v14 → v15：实体/蓝图锚点标记、`AnchorTarget`、三个压力字段。
- 旧 v14 存档默认无锚点、目标 0、压力 0。
- 读档按存活锚点校准 `AnchorTarget`（只增不减）。
- 实测：锚点被毁后存档往返哈希一致，加载后无存活锚点且 `AnchorTarget=1`。
- 主切片哈希因新增实体字段变为 `b3e1648ccbb545f8`（双跑一致）。

## 5. 验证命令与结果

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --core-siege
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj
godot --headless --path game/SeedField.Game -- --save-smoke
godot --headless --path game/SeedField.Game -- --power-breaker-smoke
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Repository.Tests: 6 passed
SeedField.Simulation.Tests: 202 passed
SeedField.Runtime.Js.Tests: 48 passed
```

```text
CORE_SIEGE siegeCost=True quotaDenied=True anchorLost=True gapCost=True rebuiltCost=True target=1 hashOk=True ok=True
```

```text
Run 1 final hash: b3e1648ccbb545f8
Run 2 final hash: b3e1648ccbb545f8
REPRODUCIBLE: identical state hash across both runs.
```

```text
SAVE_SMOKE ok=True before=c683286b4aa486b1 mutated=1577ffdd90c05cbe after=c683286b4aa486b1 tick=39 A=v1 B=v1
POWER_BREAKER_SMOKE opened=True held=True closed=True maintained=True ok=True
```

## 6. 未覆盖

- 锚点占领状态、仓储/外部产出加成。
- 多跳围困、围困方维持成本、围困 UI。
- 残骸回收、离线文明废墟、恢复面板。
- R-04 正式费率仍 OPEN；当前全部为场景配置。
