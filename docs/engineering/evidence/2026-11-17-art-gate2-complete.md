# 证据：Gate 2 五项收口（2026-11-17）

关联：ADR-0024、ADR-0025、
`docs/engineering/evidence/2026-11-17-art-vertical-slice.md`、
`docs/engineering/evidence/2026-11-17-art-gate2-masks-benchmark.md`。

## 1. 范围

- 分支 `art/visual-slice`；表现层与样片资产。
- 未改 `SeedField.Simulation` 世界状态、存档 schema、Jint、离线包安全契约。
- `CoreVisualStateRules` 是只读分类器，不参与仿真结算。

## 2. 方向对比

- 同一台不对称 drill 工体渲染 16 方向（22.5° 步进）：
  `art/generated/style_oblique/direction_probe_oblique_*.png`。
- 4/8/16 子集对比：`art/renders/direction-4-8-16.png`。
- 512px 子集体积：4 方向 422 KB、8 方向 882 KB、16 方向 1795 KB；
  本机 16 帧 Cycles 约 6.5s。
- 结论：移动工体 **8 方向**；16 只用于对比，不量产。

## 3. 核心四态

`CoreVisualStateRules`（Simulation，只读）：

- 优先级 `Absent > Besieged > Bootstrap > Regenerating > Idle`；
- `Regenerating` = 任一储备低于上限；
- `Bootstrap` = `EntityBootstrapped` 后 12 Tick；
- 测试：`CoreVisualStateRulesTests`，7 条通过。

资产：`seed_core`（idle）、`seed_core_regenerating`、`seed_core_bootstrap`、
`seed_core_besieged`；共享 `pivot [0.5, 0.84]` 与 footprint 420。
对比表：`art/renders/core-states.png`。

真实状态截图：

```text
game/SeedField.Game/screenshots/art-core-idle.png
game/SeedField.Game/screenshots/art-core-regenerating.png
game/SeedField.Game/screenshots/art-core-bootstrap.png
game/SeedField.Game/screenshots/art-core-besieged.png
```

默认 PvE 场景的 core A 按 `CoreShellRules` 真实处于 besieged；其余三态用
peaceful 场景（不部署 B strike）生成，不是伪造状态。

```text
CORE_STATE_SMOKE idle=Idle bootstrap=Bootstrap regenerating=Regenerating besieged=Besieged ok=True
```

## 4. 模块替换

- 资产：`worker_frame`（空插槽）、`worker_frame_drill`（挖掘臂）、
  `worker_frame_sensor`（桅杆/阵列）、`worker_frame_weapon`（炮塔/炮管），各 8 方向。
- 场景新增 `scout` Mk3 `Breacher`（Sensor + Weapon），走现有 `UpgradeCommand`。
- `VisualEntitySnapshot.VisualAssetName`：Weapon→weapon，Sensor→sensor，否则 base。
- 模块对比表：`art/renders/module-swap.png`（空插槽/drill/sensor/weapon）；Main 升级后截图
  `screenshots/art-module-swap.png`。
- smoke：

```text
MODULE_SWAP_SMOKE before=worker_frame_sensor after=worker_frame_weapon version=3 ok=True
```

## 5. 情报边界与核心区归属

- `DrawPresentationEntities` 不再直接枚举 `WorldState` 画所有实体；普通画面只画 A 方
  自己与 `ObservationBuilder.Build(world,"A")` 已感知的敌方实体。
- 未侦察的 B4 默认不出现；F10 / `--omniscient-debug` 是显式全知调试开关。
- 回归 smoke：

```text
VISIBILITY_SMOKE unseenB4=True omniscientB4=True ok=True
```

- 核心本体位于独立区域 `core-a`。Main 在左侧画独立 `CORE-A REGION` 面板，并用
  `EXIT` 链接指向 frontier 的 A 出口；核心不占用 frontier 坐标。

## 6. Main 接入

- `Scripts/Presentation/VisualAssetCatalog.cs` 只读 manifest。
- `VisualEntitySnapshot` 对己方从 `EntityState` 复制不可变字段，对已感知敌方从
  `ObservedEntity` 构造；绘图类不接收 `WorldState`。
- `EntityVisual`：精灵、接触阴影、血/能量条、8 方向朝向；
  默认不画 ID。
- `CoreVisual`：四态精灵 + 状态环 + 文字。
- `TacticalOverlay`（F7）：sensor/weapon 范围、控制点半径、实体 ID。
- `DebugOverlay`（F8）：sprite bounds/pivot、tick/FPS/坐标。
- `--classic-visuals` 保留旧圆点绘制回退。

截图：

```text
screenshots/art-main-combat.png
screenshots/art-main-tactical.png
screenshots/art-main-debug.png
screenshots/art-main-omniscient.png   # explicit F10/--capture-omniscient
```

## 7. 法线/发光

- 正面帧 normal/emissive：power、四个 core 状态、worker drill/sensor。
- `Art/manifest.json` schemaVersion 3 记录逐帧 pass 路径/SHA/alphaBBox。
- `VisualBench` 用 `CanvasTexture` + `PointLight2D` + `CanvasModulate`；
  emissive 加法叠层。
- 截图：`screenshots/art-mask-normal.png`、`art-mask-plain.png`、
  `art-bench-100.png`。
- 性能表见 `2026-11-17-art-gate2-masks-benchmark.md`；开发机 100 台
  masks-on 约 1831 FPS（vsync 关闭、上限参考），约为 masks-off 的 56%。

## 8. 验证与哈希

```text
dotnet build SeedField.slnx
dotnet test SeedField.slnx
SeedField.Repository.Tests: 18 passed
SeedField.Simulation.Tests: 230 passed
SeedField.Runtime.Js.Tests: 85 passed
Total: 333 passed, 0 failed

python tools/build_art_manifest.py --check
ART_MANIFEST_OK
```

```text
dotnet run --project src/SeedField.Headless -c Debug --no-build
Run 1 final hash: 34886c1bb93164a1
Run 2 final hash: 34886c1bb93164a1
REPRODUCIBLE: identical state hash across both runs.
```

Godot 实机运行：

```text
--module-swap-smoke       ok=True
--core-state-smoke        ok=True
--visibility-smoke        ok=True
--capture-visual          SCREENSHOT_SAVED ... art-main-combat.png
--capture-tactical        SCREENSHOT_SAVED ... art-main-tactical.png
--capture-debug           SCREENSHOT_SAVED ... art-main-debug.png
--classic-visuals --capture  SCREENSHOT_SAVED ... m0-combat.png
```

## 9. 未验证/风险

- VisualBench 只测开发机 RTX 4090；不是最低配置，也未测 4K/其他 GPU。
- Main 暂不启用 2D 灯光；只验证了 VisualBench 中的 CanvasTexture 法线。
- 法线/发光只做正面帧；移动工体其余 7 方向尚无 pass。
- 默认场景 core 为 besieged；四态截图依赖 peaceful 场景变体。
- 未做正式地表材质、全方向法线图集、真实事件逐帧动画。
