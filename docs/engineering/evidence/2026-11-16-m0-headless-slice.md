# 证据：M0 Headless 纵切片（2026-11-16）

## 环境

```text
dotnet SDK 10.0.404
dotnet runtime 10.0.15
Node v26.4.0（仅用于核对；游戏运行时不使用）
git 2.55.0
Godot：未安装
```

## 执行的命令与结果

```powershell
dotnet test tests/SeedField.Simulation.Tests/SeedField.Simulation.Tests.csproj
# 19 passed

dotnet test tests/SeedField.Runtime.Js.Tests/SeedField.Runtime.Js.Tests.csproj
# 7 passed

dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj
# exit code 0
```

最终日志（节选）：

```text
SeedField M0 headless slice
ticks: 600
final: tick=600 entities=1 energySpent=423 shots=52 damage=414 destroyed=2 messagesDelivered=39
controller faults: 0
event counts:
  EntityDamaged: 52
  EntityDestroyed: 2
  EntityMoved: 280
  IntentRejected: 51
  MessageDelivered: 39
  MessageQueued: 39
  ShotFired: 52
intent rejection reasons:
  UnknownEntity: 1
  WeaponCoolingDown: 50
sample timeline:
  t   0  entity 1 moved (1000,1000) -> (1092,1075) (-1E)
  t   0  entity 3 moved (14000,10000) -> (13910,10000) (-1E)
  ...
Run 1 final hash: 813340ffafaffbb0
Run 2 final hash: 813340ffafaffbb0
REPRODUCIBLE: identical state hash across both runs.
```

## 覆盖的验收点

| 验收点 | 证据 |
|---|---|
| 固定种子/输入下 5,000 Tick 状态哈希一致 | `DeterminismTests.SameWorldAndSameController_ProducesIdenticalHashesForFiveThousandTicks` |
| 消息不得同 Tick 到达 | `MessageTests.SentMessageIsNeverVisibleInTheSameTick`（延迟 1 与 2 两例） |
| 同一库存不能同 Tick 重复消费 | `EnergySpendingTests.TwoExpensiveMovesInOneTickCannotBothSpendTheSameEnergy` |
| 非法意图不得改变世界 | `EnergySpendingTests.RejectedIntentDoesNotChangeTheWorld` |
| 传感器信息过滤 | `ObservationTests.ObservationContainsOwnEntitiesAndOnlySensedForeignEntities` |
| 外部全毁后核心区可复建 | `CoreRecoveryTests.AfterTotalExternalLossCoreCanBootstrapAMinimumEntity` |
| Jint 死循环不破坏世界 | `JsControllerTests.InfiniteLoopIsStoppedAndReturnsNoIntent` |
| 脚本无 CLR/宿主访问 | `JsControllerTests.ScriptCannotReachClrOrHostObjects` |
| `eval` 字符串编译被拒绝 | `JsControllerTests.EvalIsRejectedByStringCompilationPolicy` |
| 真实 JS 驱动单位产生战术差异 | Headless 36 条消息、52 次射击、2 个实体被摧毁 |

## 未覆盖

- Godot 实际画面与截图（编辑器未安装）。
- 跨区、存档迁移、第三方包隔离、长期（10k+ 实体）性能。
- 随机命中与正式平衡数值。
