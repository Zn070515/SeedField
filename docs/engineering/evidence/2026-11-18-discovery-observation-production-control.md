# 侦察发现、观察扩展与生产开关证据（2026-11-18）

## 变更

- 矿脉需侦察发现：`DiscoveredDeposits` 按文明持久化，观察只含已发现矿脉。
- 观察新增装甲/护盾/武器类型/生产模块/生产开关与区域库存。
- `SetProductionIntent` 可暂停/恢复被动冶炼与构件生产。
- 世界存档 v23（发现）→ v24（生产开关）；哈希与 v22 迁移。
- 三套观察序列化与 TypeScript 契约同步。

## 测试

```powershell
dotnet test SeedField.slnx
```

结果：**453 通过 / 0 失败**

- 仿真 302
- Runtime.Js 124
- 仓库门禁 19
- Catalog 8

关键用例：

- `MiningTests.DepositsRemainHiddenUntilScouted`
- `MiningTests.SaveRoundTripKeepsDiscoveredDeposits`
- `CombatModulesTests.ObservationExposesArmorShieldAndProductionModules`
- `ProductionChainTests.ProductionCanBePausedAndResumed`
- `DeviceProgramsTests.ScriptsCanPausePassiveProduction`
- `ControllerContractTests.TypeScriptContractCoversEveryObservationField`

## Godot

```powershell
godot --path game/SeedField.Game -- --save-smoke
```

实际输出：

```text
SEEDFIELD_PRERUN tick=36 shots=4 entities=6
SAVE_SMOKE ok=True before=56c718801de651bc mutated=da1c75ac800b59e0 after=56c718801de651bc tick=36 A=v1 B=v1 tutorial=5/5
```

## 边界

- 矿脉发现后永久可见；无战争迷雾。
- 四类矿仍统一为一种 Ore；无配方/副产物/传送带。
- 护盾/装甲只读，脚本不能直接修改。
- 生产开关只影响被动冶炼/构件，不影响采矿。
- 数值仍是演示配置。
