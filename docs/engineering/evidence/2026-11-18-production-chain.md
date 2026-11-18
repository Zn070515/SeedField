# M2 生产链证据（2026-11-18）

## 变更

- 区域库存新增 `Ore` 与 `Components`。
- 钻机产 Ore（`OreMined` / `OreMinedTotal`）。
- `SmelterModule`：Ore→M；`FabricatorModule`：M→Components（有区域上限）。
- `DeviceBlueprint.ComponentCost`；建造队列同时扣 M/构件，取消/失败同时退款。
- `ConstructionOrder.ComponentsPaid`；事件增加构件退款字段。
- 世界存档 v22；初始场景增加冶炼厂，钻机+冶炼形成 M 流。
- 事件日志覆盖采矿/冶炼/构件生产。

## 测试

```powershell
dotnet test SeedField.slnx
```

结果：**448 通过 / 0 失败**

- 仿真 298（含 4 个 ProductionChainTests）
- Runtime.Js 123
- 仓库门禁 19
- Catalog 8

关键用例：

- `ProductionChainTests.SmelterConvertsRegionalOreIntoMaterial`
- `ProductionChainTests.FabricatorConvertsMaterialIntoComponentsUpToItsCap`
- `ProductionChainTests.AdvancedConstructionRequiresAndRefundsComponents`
- `ProductionChainTests.SaveRoundTripKeepsOreComponentsAndProductionModules`
- `MiningTests.DrillMinesTheNearestDepositIntoTheRegionalStock`（断言 Ore 库存）

## Headless

```powershell
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --mining
```

实际输出：

```text
MINING ore=12 smelted=8 oreStock=4 material=8 remaining=0 depleted=True ok=True
```

## Godot

```powershell
godot --path game/SeedField.Game -- --save-smoke
```

实际输出：

```text
SEEDFIELD_PRERUN tick=36 shots=4 entities=6
SAVE_SMOKE ok=True before=1278647c08cfa1ac mutated=5e8dc49f3138f01c after=1278647c08cfa1ac tick=36 A=v1 B=v1 tutorial=5/5
```

## 边界

- 单一 Ore/Components 种类；无配方、副产物、传送带。
- 生产模块为被动能力，不进入 Observation；脚本不能直接控制产量。
- 4 ore/tick、2 M/构件、20 构件上限是演示值，不是平衡。
- 构件厂默认只由场景蓝图提供，初始未部署；玩家可在工坊建造。
