# ADR-0044：区域矿脉、钻机采集与 M 的本地闭环

- 状态：**Accepted**
- 日期：2026-11-18
- 关联决策：E-01、E-02（M 简化物流）、M2（物料/经济闭环）
- 前置：区域库存、建造扣 M、确定性 Tick、废土矿脉美术（ADR-0043）

## 背景

M2 要求 M 是“可采集/储存/运输/消耗”的物质资源，但此前 M 只来自场景库存、
核心再生、战斗掉落与残骸回收；地图上的矿脉只是美术，钻机没有玩法。玩家无法
通过部署设备建立本地采集闭环。

## 决定

### 1. 有限矿脉

- 新增 `ResourceDepositState`：稳定 `Id`、`RegionId`、`OreKind`、`Position`、
  `RadiusMm`、`Remaining`、`YieldPerTick`。
- 矿脉属于某一个区域，位置/半径/余量都进入世界状态；耗尽后保留在世界上
  （供表现/诊断），但不再产出。
- `OreKind`（Copper/Lead/Titanium/Coal）目前**只做视觉与产出差异**，统一产出 M；
  不引入第二套材料经济。多资源分化需另立 ADR。
- `WorldBuilder.AddDeposit(...)` 以稳定 id 递增创建；世界存档 schema v20 保存。

### 2. 钻机模块与 MineIntent

- 新增 `DrillModule(RatePerTick, RangeMm, EnergyCostPerTick)`，可挂蓝图/实例；
  钻机计入 Execution 分支。
- `MineIntent(EntityId)` 不带目标 id：引擎在设备所在区域内选择**范围内最近**
  未耗尽矿脉；距离相同按稳定矿脉 id 决胜。
- 校验顺序：有钻机 → 范围内有矿脉 → 未耗尽 → 能量足够；只有成功后扣能量、
  扣余量、写入区域库存、产生 `MaterialMined`。
- 能量沿用既有的本地储能 + 电网自动取电/恢复储备规则；不足时 `NoEnergy` 并
  产生 `PowerShortfall`。
- 失败码：`NoDrill`、`DepositOutOfRange`、`DepositDepleted`；若范围内只有
  已耗尽矿脉，返回 `DepositDepleted` 而不是“没有矿”。
- 统计新增 `MaterialMinedTotal`；事件进入日志/事件过滤。

### 3. 持久化与确定性

- 世界存档 v20：顶层 `deposits` 数组 + `nextDepositId`；实体/蓝图新增 `drill`
  字段。v19 及更早存档没有这些字段时按空矿脉/无钻机迁移。
- 哈希加入 NextDepositId、每区域矿脉（id/kind/位置/半径/余量/产出）、实体
  钻机模块与 `MaterialMinedTotal`。
- 黄金场景哈希因“空矿脉计数 + NextDepositId + 新统计字段”变化，已按测试说明
  更新；该场景行为不变。

### 4. 脚本与内置模板

- Jint 意图解析新增 `{ type: "mine", entityId }`；TypeScript 契约同步。
- 新增 `builtin.harvester.v1`：每 Tick 对自身执行 `mine`；蓝图显式
  `ControllerProgramId = builtin.harvester.v1` 才启用。
- 初始场景新增一处铜矿与 A 方钻机，演示无玩家代码的采集闭环。

## 非目标

- 不做矿床观察/发现（当前脚本不能看到矿脉列表；见“开放问题”）。
- 不做多资源、冶炼、传送带、钻头磨损、矿脉视觉耗尽状态。
- 不改跨区物流/能源/通信规则；矿脉产出进入所在区域库存，跨区仍需运输。
- 不把演示数值（5M/tick、5000 余量）写成正式平衡。

## 后果

- M2 的“采集→本地库存→建造消耗”闭环成立；玩家可在矿脉上部署钻机持续产 M。
- 矿脉是有限资源，长期工业仍需扩张/跨区运输，避免无限本地供给。
- 脚本暂时只能“盲采最近矿”；要规划矿点需要后续观察字段与 UI。
- 世界存档与哈希变更，旧档迁移为无矿脉；黄金哈希已更新。

## 验证

- `MiningTests`：最近矿脉、耗尽拒绝、越界拒绝、无钻机拒绝、无能量不产出、
  距离相同按 id、存档往返、双跑确定性。
- `SaveLoadTests.V19SaveWithoutDepositsLoadsEmptyDeposits`。
- `DeviceProgramsTests.HarvesterTemplateEmitsMineIntent`。
- `--mining`：`MINING mined=12 stock=12 remaining=0 depleted=True ok=True`。
- Godot `--save-smoke`：矿区世界保存/读取哈希一致。
- 全量测试与仓库门禁通过。

## 开放问题

- 矿床是否应进入 Observation（传感器/侦察发现），以及 UI 如何标注已耗尽矿脉。
- 是否引入多资源（Cu/Pb/Ti/C）与冶炼链；需要独立经济 ADR。
- 钻机是否应有磨损、过热、维护或电力优先级。
