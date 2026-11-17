# ADR-0032：残骸占领与锚点仓储/产出加成

- 状态：**Accepted**
- 日期：2026-11-17
- 关联决策：ADR-0016（外壳锚点）、ADR-0017（残骸）、ADR-0018（离线废墟）
- 前置：在线残骸、核心外壳规则、世界存档 v19

## 背景

离线废墟导入与战斗摧毁都会留下 `WreckState`，但目前只有“回收”一种操作；
外壳锚点只参与围困成本/配额，不提供任何正向收益。玩法上需要：

- 废墟可以被在场文明**占领**，并阻止其他文明抢收；
- 每存活一个外壳锚点，给核心提供可配置的**仓储上限与每 Tick 产出**。

## 决定

### 1. 残骸占领

- 新增 `OccupyWreckCommand(CivilizationId, WreckId)`；
- 占领条件：该文明在残骸所在区域有**存活实体**（不能远程占领）；
- 已被其他文明占领 → `WreckUnavailable`；同一文明重复占领幂等成功；
- 占领写入 `WreckState.OccupiedByCivilizationId` / `OccupiedTick`，进入存档与哈希；
- `RecoverWreckCommand`（回收=清理）规则：
  - 被其他文明占领 → 拒绝；
  - 否则保持原规则：原主可远程回收，其他文明需在场；
  - 占领者在场，因此可以回收；
- 新增 `WreckOccupied` 事件，并加入日志/颜色/情报过滤（A 方或可见时显示）。

### 2. 锚点仓储与产出

`SeedCoreConfig` 新增三个场景配置（默认 0，保持旧行为）：

| 字段 | 含义 |
|---|---|
| `AnchorEnergyPerTick` | 每个存活锚点每 Tick 追加的核心 E 产出 |
| `AnchorMaterialPerTick` | 每个存活锚点每 Tick 追加的核心 M 产出 |
| `AnchorStorageBonus` | 每个存活锚点增加的核心 E/M 仓储上限 |

`RegenerateCores` 每 Tick：

```text
anchors = CountAliveAnchors(civ)
maxE = MaxEnergyReserve + anchors * AnchorStorageBonus
maxM = MaxMaterialReserve + anchors * AnchorStorageBonus
rateE = EnergyPerTick + anchors * AnchorEnergyPerTick
rateM = MaterialPerTick + anchors * AnchorMaterialPerTick
```

- 使用 `Int128`/饱和加法，避免溢出；
- 产出仍受仓储上限约束，`CoreRegenerated` 报告实际加入量；
- 锚点死亡/移除后加成立即消失；
- `AnchorTarget`、围困成本/配额、恢复规则不变。

### 3. 存档与哈希

- 世界存档 **v18 → v19**：
  - `WreckSaveData` 增加 `OccupiedByCivilizationId` / `OccupiedTick`；
  - `CoreSaveData` 增加三个锚点配置；
- 旧存档缺省：未占领、锚点配置 0；v18 迁移显式清零新字段；
- 读档校验：占领者 ID 非空、占领 Tick 在 `0..world.Tick`；未占领时 Tick 必须 -1；
- 占领状态与锚点配置进入 `WorldStateHasher`。

## 非目标

- 不做独立的“废墟实体”美术/碰撞/清理动画；当前以 `WreckState` 作为废墟实体。
- 不增加占领 UI 按钮/快捷键；本轮先提供宿主命令与事件，面板后续接入。
- 不做占领计时、主权衰减、税收或锚点专属蓝图。
- 锚点数值是场景配置，不是正式平衡。

## 后果

- 残骸多了“先占后收”的战术层：在场文明可以封锁其他文明的远程回收。
- 锚点从纯防守成本变成可配置的经济/仓储收益；正式版需要经济上限与数值。
- 存档 v19；旧档可迁移，历史证据中的旧哈希保持原值。

## 验证

- `WreckRecoveryTests` 新增：
  - 无在场实体不能占领；
  - 在场文明占领后，原主远程回收被拒绝；
  - 占领者可以回收并取得材料；
  - 占领状态存档往返与哈希一致。
- `AnchorSupportTests`：
  - 锚点追加 E/M 产出；
  - 仓储上限随锚点提高；
  - 无锚点时旧行为不变；
  - 配置存档往返；v18 迁移为 0。
- 全量测试、Godot smoke、CI 通过。

## 回滚

移除 `OccupyWreckCommand`/占领字段/`WreckOccupied` 与三个锚点配置字段，恢复
`RecoverWreck` 旧规则；存档 schema 回 v18，v19 存档需要丢弃新增字段。
默认 0 配置下经济行为不变。
