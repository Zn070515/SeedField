# ADR-0047：矿脉侦察发现、观察扩展与生产开关

- 状态：**Accepted**
- 日期：2026-11-18
- 关联决策：M2（信息受限/物料）、M4（装备差异可读）
- 前置：ADR-0044（矿脉/钻机）、ADR-0045（装甲/护盾）、ADR-0046（生产链）

## 背景

上一轮之后仍有四个明显缺口：

1. 矿脉无需侦察就出现在观察里，废土地图的探索没有信息价值；
2. 脚本看不到装甲/护盾/武器类型/生产模块，无法按装备差异写策略；
3. 冶炼/构件生产完全被动，脚本无法暂停或按局势调节；
4. 四类矿仍统一为一种 Ore，且没有配方/副产物（已由 ADR-0048 解决）。

本轮先解决 1–3；4 需要独立的经济/内容 ADR。

## 决定

### 1. 矿脉侦察发现

- `WorldState.DiscoveredDeposits`：每文明的已发现矿脉 id 集合，排序、进存档与哈希。
- 每 Tick 侦察：存活己方设备用 `max(传感器范围, 钻机范围)` 发现同区域矿脉；
  临界损伤时传感器失效，但钻机范围仍可发现。
- 发现是**永久**的；观察只包含已发现矿脉。
- 文明级观察列出全部已发现矿脉；设备级还要求在本区域且设备视野内。
- 世界存档 v23。

### 2. 观察扩展

- `ObservedEntity` 新增：`armor`、`shield`、`maxShield`、`weaponKind`、
  `hasSmelter`、`hasFabricator`、`productionEnabled`。
- `Observation` 根新增：`material`、`ore`、`components`（文明级为跨区总和，
  设备级为所在区域库存）。
- 完整 JSON、紧凑 JSON、Jint 对象、expander、TypeScript 契约与漂移测试同步。
- 脚本现在可以读取防御状态与生产模块，但**不能**直接写入护盾/装甲。

### 3. 生产开关

- 新增 `SetProductionIntent(EntityId, Enabled)`（Jint `{type:"production", enabled}`）。
- `EntityState.ProductionEnabled` 默认 true；关闭后该设备的冶炼/构件被动生产暂停，
  采矿与其他动作不受影响。
- 开关进入世界存档 v24 与哈希；观察暴露 `productionEnabled`。
- 关闭在下一 Tick 的生产阶段生效（生产先于意图执行）。

## 非目标（仍待后续 ADR）

- 四类矿差异化与配方/副产物由 ADR-0048 完成；传送带仍不做。
- 护盾无朝向/分区；模块无独立耐久或命中部位。
- 矿脉没有战争迷雾之外的隐藏规则（发现后永久可见）。
- 数值仍是演示配置，不是正式平衡。

## 后果

- 侦察设备获得真实信息价值；未侦察的矿脉不会出现在脚本观察中。
- 脚本可按装甲/护盾/武器类型与生产模块调整策略。
- 生产可以按局势暂停，避免构件厂持续抽干 M。
- 存档/哈希升级至 v24，旧档迁移为“无发现、生产启用”。
- 黄金哈希按发现状态与生产开关字段更新。

## 验证

- `MiningTests.DepositsRemainHiddenUntilScouted`、发现存档往返。
- `CombatModulesTests.ObservationExposesArmorShieldAndProductionModules`。
- `ProductionChainTests.ProductionCanBePausedAndResumed`。
- `DeviceProgramsTests.ScriptsCanPausePassiveProduction`。
- 全量测试、仓库门禁、Godot `--save-smoke` 通过。

## 回滚

删除发现集合、观察新字段与 `SetProductionIntent`，恢复“矿脉全可见/生产永远被动”；
v24 存档需迁移或保留字段但忽略。
