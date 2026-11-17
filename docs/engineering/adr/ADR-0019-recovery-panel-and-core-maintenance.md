# ADR-0019：恢复面板与核心维护

- 状态：**Accepted（原型语义；维护成本为场景配置）**
- 日期：2026-11-17
- 关联决策：R-02、R-04、G-07
- 前置：ADR-0015、ADR-0016

## 背景

ADR-0015/0016 已实现恢复配额、恢复债务、外壳锚点与围困压力，但宿主只能通过测试/命令
观察，玩家看不到：

- 当前配额、有效配额、窗口重置；
- 恢复债务与成本乘数；
- 锚点覆盖缺口；
- 是否被围困；
- 何时应该“恢复”或“维护”。

本 ADR 增加只读诊断与宿主操作面板，并提供“核心维护”命令（清空配额/债务）。

## 决定

### 1. 核心维护命令

- 新命令 `MaintainCoreCommand(CivilizationId)`，经 `ExecuteRecoveryCommand`。
- `SeedCoreConfig.RecoveryMaintenanceMaterialCost`（默认 0）：
  - 从核心材料储备扣除；
  - 不足返回 `InsufficientMaterial`；
  - 成功计入 `MaterialSpentTotal`。
- 效果：
  - `RecoveriesInWindow = 0`；
  - `RecoveryStreak = 0`；
  - `RecoveryWindowStartTick = 当前 Tick`；
  - `LastRecoveryTick = -1`；
  - 发 `CoreMaintained`。
- 不补充 E/M 储备、不创建实体、不影响锚点/围困/保底再生。
- 成本为场景配置；默认 0 表示免费重置，正式平衡前不启用（R-04 OPEN）。
- 这是 `[PROPOSED]` 的“付费清债”规则：用材料换快速恢复窗口，不是消除损失。

### 2. 只读诊断

`CoreRecoveryDiagnosticsBuilder.Build(world, civilizationId)` 返回：

- 核心区域、E/M 储备与上限；
- 配额配置、已用、有效配额、窗口长度/剩余、是否可恢复；
- 恢复债务 streak、当前成本乘数（含透支与围困）；
- 锚点目标/存活/缺口、每缺口成本；
- 是否被围困、围困成本、配额惩罚；
- 维护材料成本；
- 世界残骸总数。

诊断只读，不消耗 RNG、不修改世界；UI 与 Headless 共享。

### 3. Godot 面板

- `F6` 打开/关闭 `RecoveryPanel`（`F5` 已用于保存）。
- 面板显示上述诊断，并提供：
  - `RECOVER MIN`：用场景配置的 `EmergencyRecovery` 蓝图在核心区中心执行
    `BootstrapCoreCommand`；
  - `MAINTAIN CORE`：执行 `MaintainCoreCommand`；
  - `CLOSE F6`。
- 面板只把按钮转成宿主命令；不直接改 `WorldState`、不自己计算配额/债务。
- 与 F1/F2/F3/F4 面板互斥；打开时暂停。
- `--recovery-smoke` 验证“恢复 → 维护”接线；
  `--capture-recovery` 生成 `screenshots/m0-recovery.png`。
- 演示场景新增 `core-a` 核心区、A 的核心、与 frontier 的直连链路，以及一个
  B 方观察者实体，使面板能显示真实围困状态；核心区不参与画面绘制。

## 存档与哈希

- schema v16 → v17：`CoreSaveData.RecoveryMaintenanceMaterialCost`。
- 旧存档默认维护成本 0（免费维护）。
- 维护本身只改变既有配额/债务/储备字段，不新增状态。
- 哈希纳入维护成本；诊断不进入哈希。

## 非目标

- 正式维护费率、维护时长、冷却；
- 锚点维护、残骸回收入口、废墟导入 UI；
- 跨文明维护/借用；
- 恢复队列、批量恢复、撤销。

## 后果

- 玩家能在游戏内看到配额/债务/锚点/围困，不再依赖日志推断。
- `RECOVER MIN` 与 `MAINTAIN CORE` 都走统一宿主命令，便于后续 UI/测试复用。
- 免费维护（默认）只是原型便利；正式版必须配置成本，否则配额可被无限重置。
- 面板改动已用实机截图检查，避免裁切。
