# 证据：恢复面板与核心维护（2026-11-17）

关联：ADR-0019、ADR-0016、ADR-0015。

## 1. 核心维护命令

- `MaintainCoreCommand`：扣 `RecoveryMaintenanceMaterialCost`，清零窗口/债务，
  `LastRecoveryTick=-1`，发 `CoreMaintained`。
- 不补储备、不创建实体、不影响锚点/围困/保底再生。
- 实测：
  - 恢复一次（40/30，streak=1）→ 维护（材料 10）→ 储备 100→60，
    `MaterialSpentTotal=40`（含本次 30+10），streak/used 归零；
  - 下一次恢复回到 40/30，不再有 50% 债务；
  - 维护成本 50、储备 0 → `InsufficientMaterial`，状态不变；
  - 未知文明 → `CoreNotAvailable`；
  - 维护成本进存档/哈希，v16 旧档默认 0。

## 2. 诊断

`CoreRecoveryDiagnosticsBuilder` 输出：

- 核心区域、E/M 储备与上限；
- 配额/有效配额/窗口/剩余 Tick/是否可恢复；
- streak 与成本乘数（含透支/围困）；
- 锚点目标/存活/缺口；
- 围困状态/成本/配额惩罚；
- 维护成本、残骸数。

实测（围困 + 一次恢复）：`used=1, effective=1, streak=1, cost=250%, besieged=true`。

## 3. F6 面板

- `RecoveryPanel` 显示上述指标；`RECOVER MIN` / `MAINTAIN CORE` / `CLOSE F6`。
- 与 F1/F2/F3/F4 互斥；打开暂停；按钮只调用宿主命令。
- 场景新增 `core-a` + A 核心 + 与 frontier 的链路 + B 观察者（制造真实围困）。
- 实机截图 `game/SeedField.Game/screenshots/m0-recovery.png` 已人工检查：
  - `quota 1/1 (base 2)`、`reset 41t`、`debt streak 1`、`cost ×450%`；
  - `anchors 0/0`、`BESIEGED +100% cost -1 quota`；
  - `RECOVER MIN`、`MAINTAIN CORE`、`CLOSE F6` 完整显示，无裁切。
- `--recovery-smoke`：

```text
RECOVERY_SMOKE recovered=True cleared=True material=20 ok=True
```

## 4. 存档

- schema v16 → v17：`CoreSaveData.RecoveryMaintenanceMaterialCost`。
- 旧 v16 默认 0；维护成本进入哈希。
- `SAVE_SMOKE ok=True before=bb98794a239ab147 after=bb98794a239ab147`。

## 5. 验证命令与结果

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet build game/SeedField.Game/SeedField.Game.csproj
godot --headless --path game/SeedField.Game -- --recovery-smoke
godot --headless --path game/SeedField.Game -- --save-smoke
godot --path game/SeedField.Game -- --capture-recovery
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Repository.Tests: 6 passed
SeedField.Simulation.Tests: 220 passed
SeedField.Runtime.Js.Tests: 48 passed
RECOVERY_SMOKE recovered=True cleared=True material=20 ok=True
SAVE_SMOKE ok=True before=bb98794a239ab147 mutated=d8f61830f18c20c0 after=bb98794a239ab147
POWER_BREAKER_SMOKE opened=True held=True closed=True maintained=True ok=True
```

## 6. 未覆盖

- 正式维护费率/冷却、锚点维护、残骸/废墟入口。
- 面板不显示残骸列表、不提供废墟导入按钮。
- 多核心/多文明切换仍为单 A 面板。
