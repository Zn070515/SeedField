# ADR-0016：核心区外壳锚点与围困压力

- 状态：**Accepted（原型语义；数值为场景配置）**
- 日期：2026-11-17
- 关联决策：R-01、R-02、R-03、R-04
- 前置：ADR-0015

## 背景

ADR-0015 已让核心快速恢复受窗口/配额/债务约束，但“围困”仍然只通过切断外部资源间接
影响玩家，核心外壳没有可摧毁的实体目标。R-04 仍缺少“围困有压力但不锁死”的机制。

调研结论（`03-Resources/游戏设计_核心区恢复围困与离线对手调研.md`）建议：
- 永久种子层不可毁；
- 外围“锚点/工业壳”可被摧毁或占领；
- 围困只影响外壳与恢复节奏，不切断保底再生。

本 ADR 落地最小外壳：**可摧毁的核心锚点 + 敌对存在带来的恢复压力**。

## 决定

### 1. 核心锚点实体

- `DeviceBlueprint` / `EntityDefinition` / `EntityState` 新增 `IsCoreAnchor`。
- 锚点是普通可攻击实体，复用现有生命、武器、传感器、部署与摧毁流程。
- **锚点必须部署在核心区之外的外壳区域**（通常是核心区的直接相邻区域）。
  核心区本身仍由 `CoreProtection` 保护：外文明不能部署、不能行动、不能进入；
  因此永久种子不会成为可攻击目标（R-01）。
- 锚点蓝图仍必须覆盖至少一个既有分支（`DeviceBlueprint` 现有校验），
  通常使用 `EnergyCapacity > 0`。
- 升级可以把实体切换为/退出锚点状态；部署或升级后刷新核心覆盖目标。

### 2. 锚点覆盖目标

- `SeedCoreState.AnchorTarget` 记录该核心**曾经同时拥有的最大锚点数**。
- 部署、升级、`WorldBuilder.Build` 与读档时：
  `AnchorTarget = max(AnchorTarget, 当前存活锚点数)`。
- 覆盖缺口 = `max(0, AnchorTarget - 当前存活锚点数)`。
- 重建锚点会缩小缺口，但不会降低历史覆盖目标；“损失过”的压力不会自动消失。
- 锚点被摧毁后不需要额外状态：存活数下降，缺口由派生计算得到。

### 3. 恢复压力

`SeedCoreConfig` 新增（全部默认 0，默认行为不变）：

| 字段 | 含义 |
|---|---|
| `AnchorRecoveryCostPercent` | 每个覆盖缺口锚点增加的恢复成本百分比 |
| `SiegeRecoveryCostPercent` | 被围困时增加的恢复成本百分比 |
| `SiegeQuotaPenalty` | 被围困时减少的有效快速恢复配额 |

- 围困判定：核心区存在**直接链路**连接到某区域，且该区域有非本文明、
  且无互不侵犯条约的实体。多跳围困暂不计算。
- 有效配额 = `max(1, RecoveryQuotaPerWindow - SiegeQuotaPenalty)`，
  避免围困把快速恢复永久降为 0；配额耗尽仍可按 ADR-0015 的透支慢速模式处理。
- 成本乘数 = `100 + 恢复债务 + 透支 + 缺口×AnchorRecoveryCostPercent
  + 围困 SiegeRecoveryCostPercent`，上限 1,000,000%。
- **保底再生不变**：围困与缺口不影响永久核心的 E/M 再生（R-02）。

### 4. 事件与诊断

- 不新增专用事件；锚点被毁沿用 `EntityDestroyed`，成本变化体现在
  `EntityBootstrapped` 的缩放成本上。
- 配额耗尽仍发 `RecoveryQuotaDenied`，`Limit` 使用**有效配额**而非配置值。

## 存档与哈希

- schema v14 → v15：
  - `EntitySaveData.IsCoreAnchor`、`BlueprintSaveData.IsCoreAnchor`；
  - `CoreSaveData.AnchorTarget`、`AnchorRecoveryCostPercent`、
    `SiegeRecoveryCostPercent`、`SiegeQuotaPenalty`。
- 旧存档迁移：无锚点、目标 0、所有压力字段 0。
- 哈希纳入实体锚点标记、核心覆盖目标与三个压力字段。
- 读档后按存活锚点重新校准 `AnchorTarget`（只增不减）。

## 非目标

- 锚点“占领”状态、锚点仓储上限/外部产出加成。
- 多跳围困、围困方维持成本、围困 UI。
- 锚点专用摧毁事件、残骸回收、离线文明废墟（后续 ADR）。
- R-04 的正式费率；当前全部是场景配置。

## 后果

- 核心区依然不可进入、种子不可攻击；外壳有了可摧毁的具体目标。
- 失去锚点会持续提高快速恢复成本，重建锚点可恢复；围困会同时加价并压缩配额。
- 默认 0 时行为与 ADR-0015 一致；主切片哈希因新增实体字段而变化，属预期。
- 玩家若完全不建锚点，不会凭空受到锚点缺口惩罚（`AnchorTarget=0`）。
