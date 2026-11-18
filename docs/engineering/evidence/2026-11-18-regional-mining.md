# M2 区域矿脉与钻机采集证据（2026-11-18）

## 变更

- `ResourceDepositState` + `WorldState.Deposits`/`NextDepositId`（schema v20）。
- `DrillModule` 进入蓝图/实例/存档/哈希/部署/升级。
- `MineIntent`：范围内最近未耗尽矿脉，扣能量、产 Ore、扣余量、发 `OreMined`。
- `OreMinedTotal` 统计；日志/事件过滤/颜色接线。
- Jint `{type:"mine"}`、TypeScript 契约、`builtin.harvester.v1`。
- 观察：`ObservedDeposit` + `hasDrill`/`drillRangeMm`；文明级按己方区域、
  设备级按 `max(传感器, 钻机)` 范围过滤；三套序列化与 TS 契约同步。
- UI：Godot 绘制矿脉颜色/范围/余量标签与 `DEPLETED` 状态。
- 初始场景：A 方铜矿 + 钻机；黄金哈希按空矿脉/新字段更新。

## 测试

```powershell
dotnet test SeedField.slnx
```

结果：**438 通过 / 0 失败**

- 仿真 288（含 11 个 MiningTests + v19 迁移）
- Runtime.Js 123（含 harvester 模板与契约）
- 仓库门禁 19
- Catalog 8

关键用例：

- `MiningTests.DrillMinesTheNearestDepositIntoTheRegionalStock`
- `MiningTests.MiningDepletesTheDepositAndThenRejects`
- `MiningTests.OutOfRangeDepositIsRejected`
- `MiningTests.NearestTieIsBrokenByStableDepositId`
- `MiningTests.SaveRoundTripKeepsDepositRemainingAndDrill`
- `MiningTests.DeviceObservationIncludesDepositsInsideDrillRange`
- `MiningTests.CivilizationObservationIncludesDepositsInOwnedRegions`
- `DeviceProgramsTests.HarvesterTemplateEmitsMineIntent`
- `DeviceProgramsTests.HarvesterTemplateMovesTowardAFarDeposit`

## Headless

```powershell
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --mining
```

实际输出：

```text
MINING ore=12 smelted=8 oreStock=4 material=8 remaining=0 depleted=True ok=True
```

## Godot 保存冒烟

```powershell
godot --path game/SeedField.Game -- --save-smoke
```

实际输出：

```text
SEEDFIELD_PRERUN tick=34 shots=4 entities=5
SAVE_SMOKE ok=True before=d13d8d05391ba378 mutated=c48efa96829a51b5 after=d13d8d05391ba378 tick=34 A=v1 B=v1 tutorial=5/5
```

## 观察与 UI

```powershell
godot --path game/SeedField.Game -- --capture
```

`game/SeedField.Game/screenshots/m0-combat.png` 显示 A 方铜矿圈、剩余量与钻机；
矿脉耗尽后标签变为 `DEPLETED`。

## 边界

- 矿脉没有隐藏/侦察发现；己方区域与设备视野内即可见。
- 四类矿统一产 Ore；冶炼/构件见 ADR-0046。
- 演示数值 5M/tick、5000 余量不是平衡数据。
- 矿脉耗尽后仍保留在世界上，但暂无专门的视觉耗尽状态。
