# ADR-0048：多矿种配方与副产物

- 状态：**Accepted**
- 日期：2026-11-18
- 关联决策：M2（物料经济）、ADR-0044（矿脉）、ADR-0046（生产链）
- 前置：单一 Ore 库存、`SmelterModule` 1:1 冶炼

## 背景

ADR-0046 引入了 Ore→M→Components，但四类矿仍共用一份 Ore，所有矿种冶炼结果相同；
地图上的铜/铅/钛/煤只是视觉差异。需要让矿种选择影响产出结构，并为副产物提供
真实用途。

## 决定

### 1. 按矿种分库存

- `RegionStock` 用 `OreCopper/OreLead/OreTitanium/OreCoal` 四份库存；
  `GetOre(civ)` 仍返回总和，`GetOre(civ, kind)` 返回单种。
- 钻机把 Ore 写入**矿脉自身的矿种**；`OreMined` 已携带 Kind。
- 跨区物流/贸易仍只搬 M，不新增 Ore 运输；Ore 是本地冶炼输入。
- 世界存档 v25：Stock 新增四字段；v24 的单 Ore 库存迁移到 Copper。
- 观察根新增 `oreCopper/oreLead/oreTitanium/oreCoal`，`ore` 保持总和。

### 2. 配方

`SmeltingRecipes` 是场景内容（demo 值，不是平衡）：

| 矿种 | 每批消耗 | 产出 M | 副产物 |
|---|---:|---:|---|
| Copper | 1 | 1 | — |
| Lead | 2 | 1 | +1 Component |
| Titanium | 2 | 3 | — |
| Coal | 2 | 1 | +4 Energy |

- `SmelterModule.OrePerTick` 是每 Tick 可处理的 Ore 上限；批次数 =
  `min(OrePerTick / OrePerBatch, 可用 Ore / OrePerBatch)`，至少 1 批（若够）。
- 能量成本按“本 Tick 是否生产”扣一次，不按批次叠加。
- 副产物写入区域库存：Component 进构件、Energy 进区域电网。
- 事件 `OreSmelted` 增加 `Kind`/`Byproduct`/`ByproductAmount`；统计新增
  `ByproductProducedTotal`。

### 3. 矿种选择

- `SmelterModule.PreferredOre`（可选）优先；库存不足该配方最低批量时回退。
- 未指定时选择**库存最多的可冶炼矿种**，同库存按配方目录顺序（Copper→Lead→
  Titanium→Coal）决胜，保证确定性。
- 矿种选择进入观察与存档（PreferredOre 随 Smelter 保存）。

### 4. 演示

初始场景新增铅矿与一台 `PreferredOre = Lead` 冶炼厂，与铜冶炼厂并存：
铜线稳定产 M，铅线额外产构件副产物。

## 非目标

- 不做配方编辑/科技解锁；配方是固定目录。
- 不做副产物二次加工、仓储上限或品质分级。
- 不做传送带/多资源跨区运输。
- 数值（1/2/3/4）是演示配置，不是正式平衡。

## 后果

- 矿种选择有真实经济意义：铅补充构件、煤补充能源、钛提高 M 产率。
- 单一冶炼厂会优先消耗库存最多的矿种；需要特定矿种时用 PreferredOre。
- 存档/哈希升级 v25；v24 的 Ore 自动迁移为 Copper，不丢失材料。
- 观察与脚本可以按矿种库存调整采矿/冶炼策略。

## 验证

- `OreRecipeTests`：四种矿写入各自库存、铜/铅/钛/煤配方、副产物、偏好/丰度选择、
  存档往返、v24 迁移、观察分矿种。
- 全量测试、仓库门禁、Godot `--save-smoke` 通过。

## 回滚

删除按矿种库存、配方目录与副产物字段，恢复单 Ore 1:1 冶炼；v25 存档需迁移或
保留字段但忽略。
