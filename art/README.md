# SeedField Art Vertical Slice (v0)

> 状态：`[PROPOSED]` Gate 2 样片完成，Gate 3 世界切片进行中。Gate 1 已选 **A 斜俯视 35°**；
> B 严格俯视保留为对照证据；仍不是正式美术验收。
> 不改 SimulationCore / 存档 / Jint / 离线包安全契约。
> 工作分支：`art/gate3-world`；决策见 ADR-0024、ADR-0025、ADR-0026。

## 1. 本轮交付

| 样片 | 内容 |
|---|---|
| Blender 资产 | 电力、核心四态、工体空插槽/钻臂/传感；A 斜俯视 + B 对照 |
| 方向 | 工体 8 方向；16 方向探针只用于 4/8/16 对比 |
| 真实 Godot | `StyleTest` 世界空间相机；真实 `Main` 精灵/核心/战术/调试层 |
| 截图 | `art-{route}-{zoom}-{overlay}.png`、`art-main-*.png`、`art-core-*.png` |
| 对比表 | `camera-ab-layers/zoom`、`direction-4-8-16`、`core-states`、`module-swap`、`mask-sheet` |
| 概念参考 | `art/reference/concepts/`（Flux.1-dev；只作参考） |
| 资产清单 | `Art/manifest.json`（schemaVersion 3）+ `ArtAssetTests` |
| 清单生成 | `python tools/build_art_manifest.py`（哈希/alpha/pivot/pass） |

## 2. A/B 结果与路线选择

| 项 | A 斜俯视 35° | B 严格俯视 90° |
|---|---|---|
| 方向帧 | 电力 4、核心 2、工体 4 = 10 | 各 1 = 3 |
| PNG 总量（512px） | 约 1.5 MB | 约 0.6 MB |
| 体量/材质 | 强：可见立面、铜线圈、投影 | 弱：靠环纹/材质区分 |
| 战术可读性 | 中：单位有朝向但遮挡更多 | 强：足迹/朝向/距离直观 |
| 单位旋转 | 需要多方向图集 | 单图旋转 |
| 本机渲染 | 10 帧约 8s（CPU Cycles 64spp） | 3 帧约 4s |

上表是 Gate 1 初始 4 方向成本；Gate 2 已按 ADR-0025 锁定移动工体 8 方向。

**决定（ADR-0024/0025）**：Gate 2 以 A 为主；移动单位锁定 **8 方向**。
16 方向探针用于比较后不量产：4 方向 422 KB、8 方向 882 KB、16 方向 1795 KB
（512px 子集），8 方向在旋转连续性与体积之间更合适。

**未测**：真实帧时/显存/最低配置；渲染耗时只证明管线可跑，不代表美术工时。

## 3. Gate 2 新增资产与状态

| 资产 | 内容 |
|---|---|
| `worker_frame` | 空模块插槽，8 方向 |
| `worker_frame_drill` | 挖掘臂模块，8 方向（Main 暂未使用） |
| `worker_frame_sensor` | 桅杆/传感阵列，8 方向 |
| `worker_frame_weapon` | 炮塔/炮管，8 方向 |
| `seed_core` | idle，2 方向 |
| `seed_core_regenerating/bootstrap/besieged` | 各 1 方向，共享 pivot/footprint |
| `direction_probe` | drill 工体 16 方向，只用于对比 |

- 模块切换走现有 `UpgradeCommand`：scout v1（sensor）→ Mk3（sensor+weapon），
  Main 精灵从 sensor 变为 weapon；`--module-swap-smoke` 断言。
  `screenshots/art-module-swap.png` 是升级后的 Main 截图；四种模块剪影见
  `art/renders/module-swap.png`。
- 核心四态由 `CoreVisualStateRules` 分类：Besieged > Bootstrap > Regenerating > Idle。
- 默认 PvE 场景真实处于 besieged；idle/regenerating/bootstrap 截图使用 peaceful 场景。

## 4. Gate 2 世界空间改动

- `StyleTest` 不再用屏幕空间 `_scale` 缩放贴图；世界固定 2400×1500，
  地形、设施、道路、网络、选中环都在世界坐标。
- 真实 `Camera2D`：远 0.62×、中 1.00×、近 1.65×；HUD 在独立 `CanvasLayer`。
- 接触阴影改为沿主光反方向的软椭圆；电力/工体下方增加八边形基座。
- 地形限制为冷灰蓝，不再出现随机绿/紫板块。
- 默认视图隐藏射程圈与敌情；`tactical` / `debug` 才显示。
- HUD 明确标注 `STATIC SAMPLE · DEMO DATA · DOES NOT READ WORLDSTATE`。

**轮廓分化**：

- 种子核心：低矮五折 cradle、开放护环、悬浮暖色种子；不再与电力塔同构。
- 模块工体：不对称履带机架、前右挖掘臂、后左传感桅杆、背部空模块插槽。
- 电力塔：保持用户认可的 v2 造型。

## 5. 复现命令

```powershell
# Blender A/B 资产
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup `
  --python art/blender/render_style_assets.py -- --route oblique --output art/generated/style_oblique --size 512
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup `
  --python art/blender/render_style_assets.py -- --route topdown --output art/generated/style_topdown --size 512

# 复制到 Godot、重建清单并导入
Copy-Item art/generated/style_oblique/*.png game/SeedField.Game/Art/style_oblique/ -Force
Copy-Item art/generated/style_topdown/*.png game/SeedField.Game/Art/style_topdown/ -Force
python tools/build_art_manifest.py
godot --headless --path game/SeedField.Game --import

# 真实 Godot 截图
godot --path game/SeedField.Game res://Scenes/StyleTest.tscn -- --route=oblique --zoom=mid --overlay=default --capture-art
godot --path game/SeedField.Game res://Scenes/StyleTest.tscn -- --route=topdown --zoom=near --overlay=tactical --capture-art

# Main 视觉层 + 核心/模块 smoke
godot --path game/SeedField.Game -- --capture-visual
godot --path game/SeedField.Game -- --capture-tactical
godot --path game/SeedField.Game -- --capture-debug
godot --path game/SeedField.Game -- --visibility-smoke
godot --path game/SeedField.Game -- --module-swap-smoke
godot --path game/SeedField.Game -- --core-state-smoke
godot --path game/SeedField.Game -- --core-state=bootstrap
godot --path game/SeedField.Game -- --capture-omniscient
godot --path game/SeedField.Game -- --classic-visuals --capture

# mask / 性能
godot --path game/SeedField.Game res://Scenes/VisualBench.tscn -- --devices=4 --masks=on --seconds=3 --shot=art-mask-normal.png
godot --path game/SeedField.Game res://Scenes/VisualBench.tscn -- --devices=100 --masks=on --seconds=3
```

参数：`--route=oblique|topdown`、`--zoom=far|mid|near`、`--overlay=default|selected|tactical|debug`。

## 6. 资产约定与门禁

- 源：`art/blender/*.py`；生成：`art/generated/`（gitignore）。
- 运行时：`game/SeedField.Game/Art/style_{route}/`；清单 `Art/manifest.json`。
- 所有运行时 PNG：512×512、RGBA/8bit、SHA-256 与清单一致。
- schemaVersion 3 增加 `state` 与逐帧 `normal`/`emissive` pass。
- `ArtAssetTests` 解码 PNG，检查非空 alpha、4%–80% 覆盖率、四周留边、
  `alphaBBox`、斜俯视 pivot 对齐 alpha 底边、俯视 pivot 居中、方向帧完整、
  core 四态共享 pivot/footprint、pass 哈希/尺寸/alphaBBox。
- `status` 固定 `style-test-not-final`；不得当正式美术。
- `.gitignore` 覆盖 `__pycache__/`、`*.pyc`、`*.pyo`；Python 缓存不得入库。

## 7. Flux 概念参考与许可

本机 ComfyUI：`C:\Users\16275\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI`
实际 Python 环境：该目录下 `.venv`（torch 2.12.1+cu130；Desktop 的 `standalone-env` 不是运行环境）。
模型：`E:\AI_ComfyUI\Models`（Flux.1-dev、T5/CLIP、VAE）。

- 概念图只用于形体/材质参考；正式资产必须回到 Blender 源。
- 当前概念图不进入 `game/SeedField.Game/Art`，不参与运行时。
- FLUX.1-dev 模型许可含非商业限制；若未来把生成输出用于商业生产，
  需先核实 Black Forest Labs 的具体授权条件并记录在 `docs/art/SOURCE.md`。
- 尚未安装 LayerDiffuse/3D-Pack；当前 Flux 出图不带透明 alpha，也不需要。

## 8. 可借用的工具/技能

### 已在本机可用/免费
- Blender 5.2 Cycles + 本仓库程序化脚本（可复现、无插件依赖）。
- ComfyUI 0.30.2 + Flux.1-dev（概念/材质参考）。
- Godot 4.7 `CanvasTexture` + `PointLight2D` 可做 sprite normal/specular（下一步验证）。
- 社区 Blender：Sprite Sheet Maker、iso-render-pipeline、Spritify、KIT OPS Free、
  MACHIN3tools、Random Flow、BlenderKit、Node Wrangler。
- 社区 ComfyUI：LayerDiffuse（透明 PNG）、ControlNet Aux、IP-Adapter、
  ComfyUI-3D-Pack（StableFast3D/Hunyuan3D）、BiRefNet/RMBG、Ultimate SD Upscale。

### Agensi 付费技能（未购买，需用户批准）
- **Game Art Director Specialist**（$5）
- **Game Sprite Sheet Pipeline**（$15）
- **Local ComfyUI Image & Video Gen**（$10）
- **comfyui-local-generator**（$5.9）
- **Image to Game-Ready 3D**（$19）
- **Art Direction Style Bible**（$15）

当前没有购买任何技能；如需其一，告诉我，我会先确认价格/余额再加载。

## 9. Gate 3 世界切片（第一轮）

- `WorldBackdrop`：工业板块、道路、遗迹、弹坑、控制点基座与危险条纹；不再只有网格。
- `NetworkVisual`：控制点到 A/B 出口的能量/信息连线与 tick 脉冲；核芯 `EXIT` 线也由真实储备驱动。
- `EntityVisual`：单位放大到 26%，移动时产生履带扬尘，`ShotFired` 命中后 6 Tick 闪红。
- `CoreVisual`：四态环 + 5 个随 tick 旋转的分支指示点。
- 默认仍只画已感知敌人；事件流（弹道/通信/日志）同样过滤，F10 全知调试保留。
- 网络脉冲同时要求电网库存与断路器闭合；断路器打开即熄灭。
- 镜头：滚轮 0.70–1.35 缩放、中键平移；`art-main-far/mid/near.png`。
- 默认 HUD 隐藏 LEGEND/EVENT LOG；F7/F8 才显示。
- 截图：`art-main-combat/tactical/debug/omniscient.png`；工坊预览 `m0-workshop.png`。

## 10. 下一步 Gate

1. 继续补工业建筑/墙体和正式地表材质，减少程序化占比。
2. 工坊/程序编辑器的大预览使用真实模块组合，而不是单张概念图。
3. 射击、断电、围困、核心恢复、模块升级的逐帧事件演出。
4. 目标硬件 1/10/50/100 设备的真实 Main 可读性与 FPS；VisualBench 只是上限参考。
5. 正式量产前补齐工体 8 方向法线/发光。
