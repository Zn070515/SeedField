# 证据：美术 A/B 相机纵切片（2026-11-17）

关联：`docs/design-bible/v1.9-rev2/05-PRESENTATION/ART_DIRECTION_AND_VFX.md`、
`docs/art/archive/2026-11-17/VISUAL_LANGUAGE_V0_1.md`、`docs/art/archive/2026-11-17/VERTICAL_SLICE_EXECUTION.md`（已被 ADR-0041 归档为历史参考）。
Gate 2 五项收口见 `2026-11-17-art-gate2-complete.md` 与
`2026-11-17-art-gate2-masks-benchmark.md`。

## 1. 范围

- 分支：`art/visual-slice`；基线 `1d5e580`。
- 只做表现层样片：不改 SimulationCore、存档 schema、Jint、离线包或 PvE 契约。
- 状态：`[PROPOSED]` Gate 1 已完成并选 A 斜俯视；Gate 2 世界空间样片已落地。
  仍不是正式美术验收。决策见 ADR-0024。

## 2. 源与导出

- 源脚本：`art/blender/seedfield_art.py`、`render_style_assets.py`。
- 两套路线：`oblique`（35° 正交）与 `topdown`（90° 正交）。
- 三件资产：`power_node`、`seed_core`、`worker_frame`。
- 方向：斜俯视 10 帧（4+2+4），严格俯视 3 帧。
- 512×512 RGBA/8bit；清单 `game/SeedField.Game/Art/manifest.json`。
- 运行时资产由 `ArtAssetTests` 校验：清单、SHA-256、尺寸、RGBA、
  必需资产齐全、无未登记 PNG。

## 3. 真实 Godot 世界空间场景

`Scenes/StyleTest.tscn` 使用 `Scripts/StyleTest.cs`、`StyleTestWorld.cs`、
`StyleTestHud.cs`：

- 世界固定 2400×1500；地形、道路、广场、设施、网络与选中环全部世界坐标；
- 真实 `Camera2D`：远 0.62×、中 1.00×、近 1.65×；不是屏幕空间缩放贴图；
- HUD 在独立 `CanvasLayer`，不随相机缩放；
- 默认 / 选中 / 战术 / 调试四种叠层；默认隐藏射程圈/敌情；
- HUD 标注 `STATIC SAMPLE · DEMO DATA · DOES NOT READ WORLDSTATE`；
- 截图从真实 Godot Viewport 保存，不是 Blender 效果图。

截图（12 张，本轮全部重拍）：

```text
game/SeedField.Game/screenshots/art-oblique-mid-default.png
game/SeedField.Game/screenshots/art-oblique-mid-selected.png
game/SeedField.Game/screenshots/art-oblique-mid-tactical.png
game/SeedField.Game/screenshots/art-oblique-mid-debug.png
game/SeedField.Game/screenshots/art-oblique-far-default.png
game/SeedField.Game/screenshots/art-oblique-near-default.png
game/SeedField.Game/screenshots/art-topdown-mid-default.png
game/SeedField.Game/screenshots/art-topdown-mid-selected.png
game/SeedField.Game/screenshots/art-topdown-mid-tactical.png
game/SeedField.Game/screenshots/art-topdown-mid-debug.png
game/SeedField.Game/screenshots/art-topdown-far-default.png
game/SeedField.Game/screenshots/art-topdown-near-default.png
```

对比表：

```text
art/renders/camera-ab-layers.png
art/renders/camera-ab-zoom.png
art/renders/concept_sheet.png
```

## 4. A/B 成本与路线决定

| 项 | 斜俯视 35° | 严格俯视 90° |
|---|---|---|
| 方向帧 | 电力 4、核心 2、工体 4 = 10 | 各 1 = 3 |
| PNG 总量 | 约 1.5 MB | 约 0.6 MB |
| 本机 Cycles 渲染 | 10 帧约 8s | 3 帧约 4s |
| 体量/材质 | 强 | 弱（靠环纹/材质） |
| 战术可读性 | 中 | 强 |
| 单位旋转 | 多方向图集 | 单图旋转 |

未测：真实 FPS/帧时/显存/最低配置。渲染耗时只证明管线可跑，不代表美术工时。

**决定（ADR-0024）**：Gate 2 以 A 斜俯视为主要方向，B 保留对照。
A 更适合“玩家建造的精密工业设备”；B 的电力/核心容易都变成同心圆。

## 5. 轮廓、接触阴影与地形

- 种子核心改为低矮五折 cradle：宽基座、五根内倾支撑、开放护环、悬浮暖色
  种子；俯视时不再与电力塔近似。
- 模块工体改为不对称履带机架：前右挖掘臂、后左传感桅杆、背部空模块插槽；
  方向帧 0/90/180/270。
- 电力塔保持用户认可的 v2 造型。
- 接触阴影沿主光反方向（左下）绘制软椭圆；电力/工体下再有八边形基座，
  不再使用 floating 大黑圆。
- 地形色调限制为冷灰蓝；板块、接缝、电路迹线、废墟块仍为程序化占位。

仍未解决：8 方向移动单位、核心四态动画、真实模块替换、正式地表材质、
法线/发光 mask、真实 `Main` 集成。

## 6. 资产契约与门禁

- `Art/manifest.json` schemaVersion 2：每个资产含 `pivot`、`footprintPx`、
  `directions`、逐帧 `alphaBBox` 与 SHA-256。
- `tools/build_art_manifest.py` 从 PNG 解码 alpha 包围盒并重写清单；
  防止手工抄哈希/锚点。
- `ArtAssetTests` 解码 PNG，强制：
  - 512×512、RGBA/8bit；
  - 覆盖率 4%–80%，四周至少 2 px 留边；
  - `alphaBBox` 与像素一致；
  - 斜俯视 pivot = 正面帧 alpha 底边中心，俯视 pivot = 0.5/0.5；
  - 方向帧数量/角度/文件名完整；
  - 路径安全、SHA-256、必需资产、无未登记 PNG。
- `RepositoryHygieneTests` 禁止跟踪 `__pycache__/`、`*.pyc`、`*.pyo`；
  `.gitignore` 已覆盖。

## 7. 验证命令与结果

```powershell
blender --background --factory-startup --python art/blender/render_style_assets.py -- `
  --route oblique --output art/generated/style_oblique --size 512
blender --background --factory-startup --python art/blender/render_style_assets.py -- `
  --route topdown --output art/generated/style_topdown --size 512
python tools/build_art_manifest.py
godot --headless --path game/SeedField.Game --import
godot --path game/SeedField.Game res://Scenes/StyleTest.tscn -- `
  --route=oblique --zoom=mid --overlay=tactical --capture-art
dotnet test SeedField.slnx
```

```text
SeedField.Repository.Tests: 18 passed
SeedField.Simulation.Tests: 223 passed
SeedField.Runtime.Js.Tests: 85 passed
Total: 326 passed, 0 failed
```

Blender 渲染、Godot 12 张截图、`tools/build_art_manifest.py --check` 均在本机运行；
未运行的项不得当作通过。

## 8. 未变更

- 未改 `SeedField.Simulation`、世界/游戏存档 schema、Jint 安全边界、离线包导入。
- `dotnet run --project src/SeedField.Headless -c Debug --no-build` 双跑哈希
  `34886c1bb93164a1`，与美术改动前一致；仿真未受影响。
- 本次没有运行性能基准或最低配置测试。

## 9. 下一步 Gate

1. 移动单位方向数验证：一台明显不对称的战构体做 4/8/16 方向对比，再锁图集。
2. 种子核心 `idle/regenerating/bootstrap/besieged` 四态。
3. 机架 + 至少两种模块，切换后剪影明显变化。
4. `EntityVisual`/`TacticalOverlay` 接进真实 `Main`；默认关闭范围圈/ID，
   保留 F 键调试层；事件只回放，不写状态。
5. 法线/发光 mask 与 `CanvasTexture` 实机验证；记录 1/10/50/100 设备 FPS。
