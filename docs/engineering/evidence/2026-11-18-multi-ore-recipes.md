# 多矿种配方与副产物证据（2026-11-18）

## 变更

- 区域 Ore 按 Copper/Lead/Titanium/Coal 分库存；`GetOre` 总和保持兼容。
- 钻机按矿脉矿种入库。
- `SmeltingRecipes`：铜 1→1M；铅 2→1M+1 构件；钛 2→3M；煤 2→1M+4E。
- `SmelterModule.PreferredOre`；未指定时选库存最多的可冶炼矿种。
- `OreSmelted` 携带矿种/副产物；新增 `ByproductProducedTotal`。
- 观察新增四种矿库存；世界存档 v25（v24 单 Ore 迁移到铜）。
- 初始场景新增铅矿 + 铅冶炼厂。

## 测试

```powershell
dotnet test SeedField.slnx
```

结果：**466 通过 / 0 失败**

- 仿真 315（含 13 个 OreRecipeTests）
- Runtime.Js 124
- 仓库门禁 19
- Catalog 8

关键用例：

- `OreRecipeTests.DrillsStoreOreUnderTheDepositKind`
- `OreRecipeTests.CopperRecipeIsOneToOne`
- `OreRecipeTests.LeadRecipeProducesComponentsAsAByproduct`
- `OreRecipeTests.CoalRecipeProducesEnergyAsAByproduct`
- `OreRecipeTests.TitaniumRecipeYieldsThreeMaterialPerTwoOre`
- `OreRecipeTests.PreferredOreOverridesAbundanceSelection`
- `OreRecipeTests.V24SingleOreSaveMigratesToCopper`
- `OreRecipeTests.ObservationExposesPerKindOre`

## Godot

```powershell
godot --path game/SeedField.Game -- --save-smoke
```

实际输出：

```text
SEEDFIELD_PRERUN tick=36 shots=4 entities=8
SAVE_SMOKE ok=True before=... after=... tick=36 A=v1 B=v1 tutorial=5/5
```

## 边界

- 配方为固定目录，无编辑/解锁；无副产物二次加工。
- 数值为演示配置，不是平衡。
- 物流仍只搬 M；Ore 是区域本地资源。
