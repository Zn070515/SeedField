# ADR-0015：核心区恢复配额与恢复债务

- 状态：**Accepted（原型语义；数值为场景配置）**
- 日期：2026-11-17
- 关联决策：R-01、R-02、R-03、R-04、E-01
- 前置：ADR-0002、ADR-0013

## 背景

R-01/R-02 要求核心区不可毁，且外部全毁后必须能有限步骤复建最低实体。当前
`TryBootstrapFromCore` / `TryRestoreLatestProduct` 只检查核心储备，没有恢复频率或
连续恢复成本约束。结果是：

- 只要储备够，玩家可以连续恢复，缺少“被围困/被压制”的时间压力；
- 外部损失与核心恢复之间没有可调的节奏；
- R-04 的“恢复配额/围困/防套利”一直未实现。

调研（`03-Resources/游戏设计_核心区恢复围困与离线对手调研.md`）显示成熟做法是
**分层损失 + 有条件的保护 + 可回收损失**。本 ADR 只落地第一块：**恢复配额**。
核心区外壳层、围困对恢复速度的影响、残骸回收另立 ADR。

## 决定

### 1. 配置

`SeedCoreConfig` 新增（全部为场景配置，不是正式平衡）：

| 字段 | 默认 | 含义 |
|---|---|---|
| `RecoveryWindowTicks` | 0 | 快速恢复窗口长度；0 或配额为 0 时关闭配额 |
| `RecoveryQuotaPerWindow` | 0 | 每个窗口允许的快速恢复次数 |
| `RecoveryCostGrowthPercent` | 0 | 连续恢复的额外成本百分比/层 |
| `RecoveryStreakDecayTicks` | 0 | 连续恢复计数在多少 Tick 无恢复后清零；0 = 不衰减 |
| `RecoveryOverdraftCostPercent` | 0 | 超出配额时允许恢复的额外成本；0 = 硬拒绝 |

### 2. 窗口与配额

- 窗口由 `RecoveryWindowStartTick` 与 `RecoveriesInWindow` 记录；每 Tick 在
  `RegenerateCores` 中刷新，恢复尝试前再检查一次。
- 窗口内次数 < 配额：正常恢复。
- 次数达到配额：
  - `RecoveryOverdraftCostPercent > 0`：**慢速模式**，恢复仍成功，但成本增加；
  - 为 0：返回 `RecoveryQuotaExhausted`，并携带 `RecoveryQuotaDenied`
    （`Used` / `Limit` / `ResetsInTicks`）。
- 默认 0 时行为与 ADR-0002 的原型恢复一致；只有显式配置才启用配额。

### 3. 恢复债务

- `RecoveryStreak` 在每次成功恢复后 +1；连续恢复成本乘数为
  `100 + RecoveryCostGrowthPercent × RecoveryStreak`（上限 1,000,000%）。
- 超过 `RecoveryStreakDecayTicks` 没有恢复时，计数清零。
- 成本按**向上取整**应用到能量与材料：
  `ceil(base × multiplier / 100)`；`EntityBootstrapped`、统计与储备扣减都使用缩放后成本。
- 耗尽配额后的透支额外成本与债务叠加。

### 4. 保底再生不变

- 永久核心的低速 E/M 再生不受配额、围困或恢复债务影响（R-02）。
- 配额只限制“快速复建”的频率/成本，不限制保底再生产物。

### 5. 受影响的入口

- `TryBootstrapFromCore` 与 `TryRestoreLatestProduct` 都执行同一套配额/成本逻辑；
  `ExecuteRecoveryCommand` 只是统一宿主入口，不能绕过。
- 新增错误码 `RecoveryQuotaExhausted`；新增事件 `RecoveryQuotaDenied`。

## 存档与哈希

- schema v13 → v14：`CoreSaveData` 新增配额配置与
  `RecoveryWindowStartTick` / `RecoveriesInWindow` / `RecoveryStreak` /
  `LastRecoveryTick` / `RecoveryOverdraftCostPercent`。
- 旧存档迁移：窗口/配额/增长/衰减/透支均为 0，`LastRecoveryTick = -1`。
- 哈希纳入全部新字段；读档后窗口、债务与上次恢复 Tick 继续。
- 加载校验：新字段非负；`LastRecoveryTick` 与窗口起点不得晚于存档 Tick；
  无透支时窗口内次数不得超过配额。

## 非目标

- 核心区“外壳层”（可摧毁的锚点/工业壳）与围困对恢复速度的影响。
- 残骸回收、离线文明废墟、恢复 UI 面板。
- 恢复配额的具体平衡数值；R-04 仍然 OPEN。
- 跨文明的恢复配额借用/交易。

## 后果

- 默认配置下现有核心恢复测试与场景行为不变。
- 配置配额后，快速复建受时间窗口约束；配置透支后，玩家不会被硬锁死，
  但连续恢复会越来越贵。
- 连续恢复成本与窗口状态进入存档/哈希，读档不会重置压力。
- 围困玩法仍需后续 ADR 才能影响恢复速度；当前围困只能通过切断外部资源
  间接延长恢复准备时间。
