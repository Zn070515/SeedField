# ADR-0017：设备残骸与回收

- 状态：**Accepted（原型语义；回收比例为场景配置）**
- 日期：2026-11-17
- 关联决策：R-03、E-01、E-02
- 前置：ADR-0015、ADR-0016

## 背景

外部设备被摧毁后只产生 `EntityDestroyed` / `EntityInventoryLost`，没有任何可回收物。
调研（`03-Resources/游戏设计_核心区恢复围困与离线对手调研.md`）中的“可回收损失”
模式（Dwarf Fortress reclaim、Project Zomboid 尸体搜刮、Minecraft 墓碑）表明：
损失应该留下可探索/可回收的痕迹，而不是纯删除。

本 ADR 只做**设备残骸**；离线文明废墟与恢复面板另立 ADR。

## 决定

### 1. 残骸生成

- 世界级场景规则 `WreckRecoveryPercent`（0..100，默认 0 = 关闭）。
- 战斗摧毁实体时，若满足：
  - 实体有 `BlueprintId` 且该版本仍在 `Workshop` 中；
  - 蓝图 `MaterialCost > 0`；
  - 回收比例 > 0；
  则生成 `WreckState`：
  - `Id` 稳定自增（`NextWreckId`）；
  - `RegionId` / `Position` 取被毁实体；
  - `OwnerCivilizationId` 取被毁实体文明；
  - `BlueprintId` / `BlueprintVersion` 保留蓝图知识引用；
  - `MaterialRecoverable = floor(MaterialCost × WreckRecoveryPercent / 100)`；
  - `CreatedTick`。
- `MaterialRecoverable` 为 0 时不生成残骸。
- 无蓝图实例（场景直建实体）不生成残骸。
- 被毁实体携带的本地材料仍按现有 `MaterialLostTotal` 计入损失，不重复进入残骸。

### 2. 回收命令

- 新增宿主命令：
  - `RecoverWreckCommand(CivilizationId, WreckId)`；
  - 统一入口 `ExecuteWreckCommand`；脚本不直接调用。
- 可回收条件：
  - 残骸存在；
  - 回收方是**原主**（在任意位置可远程回收自己的残骸），或
  - 回收方在残骸所在区域有**存活实体**（战场搜刮）。
- 成功后：
  - `MaterialRecoverable` 进入该区域该文明的库存；
  - `Statistics.MaterialSalvagedTotal` 累加；
  - 残骸从世界移除；
  - 发 `WreckSalvaged`。
- 失败错误码：`WreckNotFound`、`WreckUnavailable`。
- 蓝图知识不因回收/摧毁而移除：工坊版本仍在世界中，残骸只保存引用用于 UI/日志。

### 3. 事件与表现

- 新增 `WreckCreated`（含残骸 ID、区域、原主、蓝图、可回收 M）与
  `WreckSalvaged`（回收方、区域、实际 M）。
- Godot 事件日志白名单/颜色/文案已接入。
- Headless `--wrecks` 演示：部署 → 摧毁 → 残骸 50% M → 回收 → 存档哈希一致。

## 存档与哈希

- schema v15 → v16：
  - `WreckSaveData[]`、`NextWreckId`、`WreckRecoveryPercent`；
  - `StatisticsSaveData.MaterialSalvagedTotal`。
- 旧存档迁移：无残骸、`NextWreckId=1`、回收比例 0、统计 0。
- 加载校验：残骸 ID 唯一且为正、区域存在、位置在区域内、材料非负、
  `CreatedTick` 不晚于存档 Tick、`NextWreckId` 大于所有残骸 ID；
  回收比例 0..100。
- 哈希纳入残骸全部字段、`NextWreckId`、回收比例与回收统计。

## 非目标

- 残骸老化/消失/被掠夺概率。
- 残骸占用区域容量、影响移动/视野。
- 自动回收、脚本 `Intent` 化、回收 UI 面板。
- 离线文明废墟（快照→切片→老化→掠夺）。
- 残骸蓝图知识丢失/逆向工程。

## 后果

- 默认 0 时行为不变；显式配置后，摧毁蓝图设备会留下按成本比例计算的残骸。
- 原主可远程回收，敌方需要在该区域有存活实体才能搜刮；避免远程偷取。
- 回收统计进入存档/哈希，可用于后续经济与平衡分析。
- 主切片哈希因新增世界字段变化，属预期。
