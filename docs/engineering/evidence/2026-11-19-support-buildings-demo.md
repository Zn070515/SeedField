# 2026-11-19 支援建筑实时演示截图

- 分支：`feat/support-buildings`
- 关联：ADR-0050（Repair Bay）、ADR-0051（Shield Projector）
- 截图：`game/SeedField.Game/screenshots/support-demo.png`

## 复现

```powershell
godot --path game/SeedField.Game res://Scenes/MindustryStyle.tscn -- --support-demo
```

Godot 运行 24 Tick（4 Tick/s），然后保存截图并退出。

## 场景

```text
区域 2000×1200 mm，topdown 90°
A：Repair Bay(400,600) + Shield Generator(800,600)
A：Shielded Frame A1(700,450)、A2(900,750)，各 50 盾
B：Raider(1700,600)，Sensor 5000，Weapon DMG 20，cooldown 0
```

每 Tick B 交替攻击 A1/A2。维修坞给最近的受损友军 +5 HP；护盾发生器给范围内
带盾友军每目标 +5 盾、扣 1E。截图中的事件日志同时出现：

```text
T22 SHIELD 4 +5 (5 total)
T22 HIT 3 -10 HP (shield -10)
T23 REPAIR 3 +5 HP
T23 SHIELD 3 +5 (5 total)
T23 SHIELD 4 +5 (10 total)
T23 HIT 4 -10 HP (shield -10)
```

## 结论

- 程序与 topdown 美术在同一真实 Godot 场景中闭合：维修、补盾、受击事件同时可见。
- 当前只是演示场景，不是 `Main` 正式客户端；主客户端仍走 oblique 视觉路由。
- 数值是演示值，不是正式平衡。

## 未覆盖

- 正式 UI（工坊建造、选中、状态面板）与音频；
- `Main` 的 topdown 迁移；
- 1000 实体性能与多维修/多投影器叠加；
- 护盾光环的专用 VFX。
