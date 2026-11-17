# 证据：美术 A/B 相机纵切片（2026-11-17）

关联：`docs/design-bible/v1.9-rev2/05-PRESENTATION/ART_DIRECTION_AND_VFX.md`、
`docs/art/VISUAL_LANGUAGE_V0_1.md`、`docs/art/VERTICAL_SLICE_EXECUTION.md`。

## 1. 范围

- 分支：`art/visual-slice`；基线 `1d5e580`。
- 只做表现层样片：不改 SimulationCore、存档 schema、Jint、离线包或 PvE 契约。
- 状态：`[PROPOSED]` Gate 1，等待用户选 A/B 相机路线；不是正式美术验收。

## 2. 源与导出

- 源脚本：`art/blender/seedfield_art.py`、`render_style_assets.py`。
- 两套路线：`oblique`（35° 正交）与 `topdown`（90° 正交）。
- 三件资产：`power_node`、`seed_core`、`worker_frame`。
- 方向：斜俯视 10 帧（4+2+4），严格俯视 3 帧。
- 512×512 RGBA/8bit；清单 `game/SeedField.Game/Art/manifest.json`。
- 运行时资产由 `ArtAssetTests` 校验：清单、SHA-256、尺寸、RGBA、
  必需资产齐全、无未登记 PNG。

## 3. 真实 Godot 场景

`Scenes/StyleTest.tscn` 使用 `Scripts/StyleTest.cs`：

- 程序化工业地形：板块、接缝、电路迹线、废墟块；
- 三件主体 + 接触阴影；
- 默认 / 选中 / 战术 / 调试四种叠层；
- 远 / 中 / 近三档缩放；
- 截图从真实 Godot Viewport 保存，不是 Blender 效果图。

截图：

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
art/renders/power_node_sheet.png
art/renders/concept_sheet.png
```

## 4. A/B 成本与观察

| 项 | 斜俯视 35° | 严格俯视 90° |
|---|---|---|
| 方向帧 | 10 | 3 |
| PNG 总量 | 1.63 MB | 0.65 MB |
| 本机 Cycles 渲染 | 10 帧约 7.6s | 3 帧约 3.8s |
| 体量/材质 | 强 | 弱（靠环纹/材质） |
| 战术可读性 | 中 | 强 |
| 单位旋转 | 多方向图集 | 单图旋转 |

未测：真实 FPS/帧时/显存/最低配置。渲染耗时只证明管线可跑，不代表美术工时。

当前未解决问题：

- 严格俯视的电力/核心更像同心圆，需靠环纹和材质区分；
- 斜俯视移动单位在远景偏小，默认相机尺度仍需调；
- 地形目前是程序化占位，不是正式地表材质；
- 尚未做 8 方向移动单位、法线/发光 mask、核心四态动画；
- 模块替换只做了灰模，未做真正的模块插槽验证。

## 5. 验证命令

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup `
  --python art/blender/render_style_assets.py -- --route oblique --output art/generated/style_oblique --size 512
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup `
  --python art/blender/render_style_assets.py -- --route topdown --output art/generated/style_topdown --size 512
godot --headless --path game/SeedField.Game --import
godot --path game/SeedField.Game res://Scenes/StyleTest.tscn -- --route=oblique --zoom=mid --overlay=tactical --capture-art
dotnet test SeedField.slnx
```

```text
SeedField.Repository.Tests: 18 passed
SeedField.Simulation.Tests: 223 passed
SeedField.Runtime.Js.Tests: 85 passed
```

## 6. 未变更

- 未改 `SeedField.Simulation`、世界/游戏存档 schema、Jint 安全边界、离线包导入。
- Headless 哈希不受美术层影响；本次没有运行 Headless 主切片以外的性能基准。

## 7. 待用户决定

- 选 A（斜俯视）还是 B（严格俯视）；或要求重做形体/材质再比选。
- 选定后再做移动单位 8 方向、核心四态、模块替换和真实 Main 集成。
