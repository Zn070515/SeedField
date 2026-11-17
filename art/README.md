# SeedField Art Vertical Slice (v0)

> 状态：`[PROPOSED]` Gate 2 进行中。Gate 1 已选 **A 斜俯视 35°** 作为主路线，
> B 严格俯视保留为对照证据；仍不是正式美术验收。
> 不改 SimulationCore / 存档 / Jint / 离线包安全契约。
> 工作分支：`art/visual-slice`；决策见 ADR-0024。

## 1. 本轮交付

| 样片 | 内容 |
|---|---|
| Blender 资产 | 电力设施、种子核心、模块工体；斜俯视 35° 与严格俯视 90° |
| 真实 Godot 场景 | `Scenes/StyleTest.tscn`：世界空间地形 + 真实 Camera2D + 默认/选中/战术/调试层 |
| 截图 | `game/SeedField.Game/screenshots/art-{route}-{zoom}-{overlay}.png` |
| 对比表 | `art/renders/camera-ab-layers.png`、`camera-ab-zoom.png` |
| 概念参考 | `art/reference/concepts/`（Flux.1-dev；只作参考，不是正式资产） |
| 资产清单 | `game/SeedField.Game/Art/manifest.json`（schemaVersion 2）+ `ArtAssetTests` 门禁 |
| 清单生成 | `python tools/build_art_manifest.py`（哈希/alpha 包围盒/pivot） |

## 2. A/B 结果与路线选择

| 项 | A 斜俯视 35° | B 严格俯视 90° |
|---|---|---|
| 方向帧 | 电力 4、核心 2、工体 4 = 10 | 各 1 = 3 |
| PNG 总量（512px） | 约 1.5 MB | 约 0.6 MB |
| 体量/材质 | 强：可见立面、铜线圈、投影 | 弱：靠环纹/材质区分 |
| 战术可读性 | 中：单位有朝向但遮挡更多 | 强：足迹/朝向/距离直观 |
| 单位旋转 | 需要多方向图集 | 单图旋转 |
| 本机渲染 | 10 帧约 8s（CPU Cycles 64spp） | 3 帧约 4s |

**决定（ADR-0024）**：Gate 2 以 A 为主。理由是玩家长期在建/升级/装配设备，
需要看见机械立面、模块和材质；B 的电力/核心容易都变成同心圆。
A 的方向帧成本真实存在，移动单位先验证 4/8/16 方向再锁定图集。

**未测**：真实帧时/显存/最低配置；渲染耗时只证明管线可跑，不代表美术工时。

## 3. Gate 2 世界空间改动

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

## 4. 复现命令

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
```

参数：`--route=oblique|topdown`、`--zoom=far|mid|near`、`--overlay=default|selected|tactical|debug`。

## 5. 资产约定与门禁

- 源：`art/blender/*.py`；生成：`art/generated/`（gitignore）。
- 运行时：`game/SeedField.Game/Art/style_{route}/`；清单 `Art/manifest.json`。
- 所有运行时 PNG：512×512、RGBA/8bit、SHA-256 与清单一致。
- `ArtAssetTests` 解码 PNG，检查非空 alpha、4%–80% 覆盖率、四周留边、
  `alphaBBox`、斜俯视 pivot 对齐 alpha 底边、俯视 pivot 居中、方向帧完整。
- `status` 固定 `style-test-not-final`；不得当正式美术。
- `.gitignore` 覆盖 `__pycache__/`、`*.pyc`、`*.pyo`；Python 缓存不得入库。

## 6. Flux 概念参考与许可

本机 ComfyUI：`C:\Users\16275\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI`
实际 Python 环境：该目录下 `.venv`（torch 2.12.1+cu130；Desktop 的 `standalone-env` 不是运行环境）。
模型：`E:\AI_ComfyUI\Models`（Flux.1-dev、T5/CLIP、VAE）。

- 概念图只用于形体/材质参考；正式资产必须回到 Blender 源。
- 当前概念图不进入 `game/SeedField.Game/Art`，不参与运行时。
- FLUX.1-dev 模型许可含非商业限制；若未来把生成输出用于商业生产，
  需先核实 Black Forest Labs 的具体授权条件并记录在 `docs/art/SOURCE.md`。
- 尚未安装 LayerDiffuse/3D-Pack；当前 Flux 出图不带透明 alpha，也不需要。

## 7. 可借用的工具/技能

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

## 8. 下一步 Gate

1. 移动单位方向数验证：先做一台明显不对称的战构体 4/8/16 方向对比，再锁图集。
2. 种子核心 `idle/regenerating/bootstrap/besieged` 四态；断电、围困来自真实状态。
3. 模块插槽可见：机架 + 至少两种模块，切换后剪影明显变化。
4. 把 `EntityVisual`/`TacticalOverlay` 接进真实 `Main`；默认关闭范围圈/ID，
   保留 F 键调试层；事件只回放，不写状态。
5. 法线/发光 mask 与 `CanvasTexture` 实机验证；记录 1/10/50/100 设备 FPS。
