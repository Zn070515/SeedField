# M2 区域矿脉与钻机采集证据（2026-11-18）

## 变更

- `ResourceDepositState` + `WorldState.Deposits`/`NextDepositId`（schema v20）。
- `DrillModule` 进入蓝图/实例/存档/哈希/部署/升级。
- `MineIntent`：范围内最近未耗尽矿脉，扣能量、产 M、扣余量、发 `MaterialMined`。
- `MaterialMinedTotal` 统计；日志/事件过滤/颜色接线。
- Jint `{type:"mine"}`、TypeScript 契约、`builtin.harvester.v1`。
- 初始场景：A 方铜矿 + 钻机；黄金哈希按空矿脉/新字段更新。

## 测试

```powershell
dotnet test SeedField.slnx
```

结果：**434 通过 / 0 失败**

- 仿真 285（含 8 个 MiningTests + v19 迁移）
- Runtime.Js 122（含 harvester 模板）
- 仓库门禁 19
- Catalog 8

关键用例：

- `MiningTests.DrillMinesTheNearestDepositIntoTheRegionalStock`
- `MiningTests.MiningDepletesTheDepositAndThenRejects`
- `MiningTests.OutOfRangeDepositIsRejected`
- `MiningTests.NearestTieIsBrokenByStableDepositId`
- `MiningTests.SaveRoundTripKeepsDepositRemainingAndDrill`
- `DeviceProgramsTests.HarvesterTemplateEmitsMineIntent`

## Headless

```powershell
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --mining
```

实际输出：

```text
MINING mined=12 stock=12 remaining=0 depleted=True ok=True
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

## 边界

- 矿脉未进入 Observation；脚本只能盲采范围内最近矿。
- 四类矿统一产 M，无多资源/冶炼。
- 演示数值 5M/tick、5000 余量不是平衡数据。
- 矿脉耗尽后仍保留在世界上，但暂无专门的视觉耗尽状态。
