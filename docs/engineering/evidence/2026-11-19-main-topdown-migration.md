# 2026-11-19 Main 客户端 topdown 实体迁移

- 分支：`feat/support-buildings`
- 关联：ADR-0041（废土正交俯视）、ADR-0050（Repair Bay）、ADR-0051（Shield Projector）
- 截图：`game/SeedField.Game/screenshots/main-topdown.png`

## 复现

```powershell
godot --path game/SeedField.Game res://Scenes/Main.tscn -- --capture-main-topdown
```

新增 `--capture-main-topdown` 只用于截图，不改变正常启动流程。

## 改动

1. `Main.InitializePresentation` 的实体目录从默认 oblique 切到 **topdown**：
   ```csharp
   _visualCatalog = VisualAssetCatalog.Load("topdown");
   ```
   世界地形/道具目录本来就是 topdown；现在实体、单位、核心、世界层统一。
2. `VisualEntitySnapshot` 新增统一映射：
   | Blueprint / 条件 | 资产 |
   |---|---|
   | `power` | `waste_generator` |
   | `sensing` | `waste_sentry` |
   | `comm` / `state` | `waste_relay` |
   | `drill` | `waste_drill` |
   | `smelter` / `smelter-lead` | `waste_smelter` |
   | `fabricator` | `waste_foundry` |
   | `repair_bay` | `waste_repair_bay` |
   | `shield_generator` | `waste_shield_generator` |
   | `barricade` | `waste_barricade` |
   | `scout` | 无武器 `waste_scout` / 有武器 `waste_sentry` |
   | `strike` / `beam` | `waste_sentry` |
   | 其他有武器/传感器 | `waste_sentry` / `waste_scout`，否则 `worker_frame` |
3. `WorkshopPanel` 预览同步使用 topdown 目录与同一映射。
4. `VisualAssetCatalog.GetCore` 在缺少 state 变体时回退 `waste_core` / `seed_core`。
5. `Scenarios` 部署一座 Repair Bay 与一座 Shield Generator，进入主场景即可看到。
6. 新增 `--capture-main-topdown` → `screenshots/main-topdown.png`。

## 验证

```text
dotnet test SeedField.slnx
  → 526 passed / 0 failed

godot --headless ... -- --module-swap-smoke
  → before=waste_scout after=waste_sentry version=3 ok=True
```

截图显示：核心、A/B 单位、控制点、地形、维修坞/护盾发生器、铜/铅矿脉、工坊入口
全部走 topdown 资产；主 HUD/教程/工坊面板保持原样。

## 未覆盖 / 后续

- `sensing` / `comm` / `state` 还没有专用 topdown 美术，当前借用 sentry/relay。
- 核心四态在主客户端回退同一 `waste_core` 底图，状态靠颜色/弧线/文字区分。
- `VisualBench` / `StyleTest` 仍是独立场景，未跟随 Main 迁移。
- 主客户端相机/缩放、脚印、阴影、HUD 与 topdown 的最终配比仍需视觉调优。
- 未做 1000 实体 topdown 渲染基准；`--benchmark-mixed` 仍是旧资产路径。
