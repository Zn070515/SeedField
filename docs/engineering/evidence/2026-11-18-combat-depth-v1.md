# M4 战斗深度 v1 证据（2026-11-18）

## 变更

- `ArmorModule(FlatReduction)`；`WeaponKind.Direct/Beam`（Beam 忽略装甲）。
- `ShieldModule(Capacity, RegenPerTick, RegenDelayTicks, EnergyCostPerRegen)`：
  先吸收伤害，延迟后只花本地能量再生，≤50% 生命时停止。
- 模块损伤：≤25% 生命时武器/传感器/驱动失效（`ModuleDisabled`）。
- `EntityDamaged` 增加护盾/装甲吸收字段；新增三项统计。
- 世界存档 v21；黄金场景 A 装甲+护盾、B 光束武器，哈希按新规则更新。
- HUD 护盾条与受伤日志。

## 测试

```powershell
dotnet test SeedField.slnx
```

结果：**444 通过 / 0 失败**

- 仿真 294（含 6 个 CombatModulesTests）
- Runtime.Js 123
- 仓库门禁 19
- Catalog 8

关键用例：

- `CombatModulesTests.ShieldAbsorbsDamageBeforeHealthAndArmorReducesIt`
- `CombatModulesTests.BeamWeaponIgnoresArmor`
- `CombatModulesTests.ShieldRegeneratesAfterTheDelayAndSpendsEnergy`
- `CombatModulesTests.CriticalDamageDisablesWeaponDriveAndSensor`
- `CombatModulesTests.DamageStateThresholdsAreStable`
- `CombatModulesTests.SaveRoundTripKeepsArmorShieldAndCurrentShield`

## Godot

```powershell
godot --path game/SeedField.Game -- --save-smoke
godot --path game/SeedField.Game -- --capture
```

实际输出：

```text
SAVE_SMOKE ok=True before=90db0003fab691a7 mutated=702732d9f4d86824 after=90db0003fab691a7 tick=36 A=v1 B=v1 tutorial=5/5
SCREENSHOT_SAVED .../screenshots/m0-combat.png
```

截图显示 A 方单位生命条上方的青色护盾条。

## 边界

- 装甲/护盾未进入 Observation；脚本不能读取当前护盾或装甲值。
- 无护盾朝向、模块独立耐久、维修、范围伤害或穿甲弹种。
- 3 装甲 / 25 护盾 / 2 再生 / 4 延迟 / 1 能量 是场景演示值，不是平衡。
