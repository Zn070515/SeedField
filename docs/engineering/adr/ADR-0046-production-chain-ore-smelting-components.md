# ADR-0046：M2 生产链——矿石、冶炼、构件与建造消耗

- 状态：**Accepted**
- 日期：2026-11-18
- 关联决策：E-01/E-02（M 简化物流）、M2（物料/经济闭环）
- 前置：区域矿脉与钻机（ADR-0044）、建造队列与 BuildTicks

## 背景

ADR-0044 让钻机直接产出 M，M 同时是原料与建造材料：没有“原料→中间品→产品”
的层次，冶炼厂/工厂没有玩法意义，高级设备也无法用构件成本区分。M2 要求 M
可采集、储存、运输、消耗，并让生产链有真实的转化与时间。

## 决定

1. **三种库存层次**（每区域/文明）：
   - `Ore`：矿脉采集的原料，四类矿统一为 Ore。
   - `Material`（M）：冶炼后的建造材料。
   - `Components`：由 M 加工的高级构件。
2. **钻机产 Ore**：`OreMined` 事件与 `OreMinedTotal`；不再直接产 M。
3. **冶炼厂** `SmelterModule(OrePerTick, EnergyCostPerTick)`：
   - 每 Tick 把区域内 Ore 1:1 转为 M，受速率与本地能量限制；
   - 事件 `OreSmelted`、统计 `OreSmeltedTotal`。
4. **构件厂** `FabricatorModule(ComponentsPerTick, MaterialPerComponent,
   EnergyCostPerTick, MaxComponents)`：
   - 每 Tick 消耗 `ComponentsPerTick × MaterialPerComponent` 的 M，产出构件；
   - 区域构件达到 `MaxComponents` 后停止，避免持续抽干 M；
   - 事件 `ComponentsFabricated`、统计 `ComponentsFabricatedTotal`。
5. **建造成本**：
   - `DeviceBlueprint.ComponentCost`（默认 0）；建造队列同时检查/扣除 M 与构件；
   - `ConstructionOrder.ComponentsPaid` 入存档；取消/失败同时退款 M 与构件；
   - 事件 `ConstructionCancelled`/`ConstructionFailed` 增加 `ComponentsRefunded`。
6. **建造时间**：继续使用既有 `BuildTicks` 队列；生产链不替代建造时间。
7. **被动生产**：冶炼/构件生产在 Step 内、意图之前自动执行；只花本地能量，
   临界损伤（≤25%）停止。没有新增 Intent，非程序员部署即可运转。
8. **存档/哈希 v22**：
   - 区域库存新增 Ore/Components；实体/蓝图新增 Smelter/Fabricator；
   - 建造订单新增 ComponentsPaid；统计新增三项；
   - v21 及更早迁移为无 Ore/Components、无生产模块、构件成本 0。

## 非目标

- 不做多矿种差异化冶炼、配方、副产物、污染或传送带。
- 不做构件种类/质量/库存上限（只有总量与 Fabricator 的 MaxComponents）。
- 不把生产模块暴露给 Observation 脚本；生产是设备被动能力。
- 不冻结数值：4 ore/tick、2 M/构件、20 构件上限是演示配置。

## 后果

- M 的来源与用途分离：钻机→Ore→冶炼→M→构件→高级建造，形成可解释的生产链。
- 高级蓝图可以用构件成本区分；建造仍受 BuildTicks 与材料双重约束。
- 生产是局部的：跨区仍需物流；构件不能从其他区域免费获得。
- 存档/哈希升级到 v22，旧档迁移为无生产链状态；黄金哈希按新流程更新。
- 演示场景：钻机产 Ore，旁边冶炼厂把 Ore 转为 M；构件厂蓝图可部署。

## 验证

- `ProductionChainTests`：冶炼、构件上限、构件成本/退款、存档往返哈希。
- `MiningTests`：钻机产 Ore 而非 M。
- `--mining`：`MINING ore=12 smelted=8 oreStock=4 material=8 ... ok=True`。
- Godot `--save-smoke` 哈希一致；全量测试与仓库门禁通过。

## 回滚

删除 Ore/Components 库存、Smelter/Fabricator、ComponentCost 与相关存档字段；
钻机恢复直接产 M。v22 存档需要迁移或保留字段但忽略。
