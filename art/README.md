# SeedField Art Vertical Slice (v0)

> 状态：`[PROPOSED]` Gate 1 样片，等待用户选择相机路线。
> 不改 SimulationCore / 存档 / Jint / 离线包安全契约。
> 工作分支：`art/visual-slice`；基线 `1d5e580`。

## 1. 本轮交付

| 样片 | 内容 |
|---|---|
| Blender 资产 | 电力设施、种子核心、模块工体；斜俯视 35° 与严格俯视 90° 两套 |
| 真实 Godot 场景 | `Scenes/StyleTest.tscn`：程序化工业地形 + 三件主体 + 默认/选中/战术/调试层 |
| 截图 | `game/SeedField.Game/screenshots/art-{route}-{zoom}-{overlay}.png` |
| 对比表 | `art/renders/camera-ab-layers.png`、`camera-ab-zoom.png` |
| 概念参考 | `art/reference/concepts/`（Flux.1-dev；只作参考，不是正式资产） |
| 资产清单 | `game/SeedField.Game/Art/manifest.json` + `ArtAssetTests` 门禁 |

## 2. A/B 结果

| 项 | A 斜俯视 35° | B 严格俯视 90° |
|---|---|---|
| 方向帧 | 电力 4、核心 2、工体 4 = 10 | 各 1 = 3 |
| PNG 总量（512px） | 1.63 MB | 0.65 MB |
| 体量/材质 | 强：可见立面、铜线圈、投影 | 弱：靠环纹/材质区分 |
| 战术可读性 | 中：单位有朝向但遮挡更多 | 强：足迹/朝向/距离直观 |
| 单位旋转 | 需要多方向图集 | 单图旋转 |
| 制作成本 | 约 3.3× 精灵数、2.5× 字节 | 低 |
| 本机渲染 | 10 帧约 7.6s（CPU Cycles 64spp） | 3 帧约 3.8s |

**未测**：真实帧时/显存/最低配置；当前只证明管线可跑、画面可比。
**观察**：A 更像“可建造的精密工业设备”，B 更像“可读的战术沙盘”。
**建议（不冻结）**：若优先“精密机械与文明感”，选 A，移动单位做 8 方向；
若优先低缩放战术清晰与制作成本，选 B，用材质/长阴影补体量。
混合相机（设施斜视 + 单位俯视）会造成透视不一致，不建议默认采用。

## 3. 复现命令

```powershell
# Blender A/B 资产
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup `
  --python art/blender/render_style_assets.py -- --route oblique --output art/generated/style_oblique --size 512
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup `
  --python art/blender/render_style_assets.py -- --route topdown --output art/generated/style_topdown --size 512

# 复制到 Godot 并导入
Copy-Item art/generated/style_oblique/*.png game/SeedField.Game/Art/style_oblique/ -Force
Copy-Item art/generated/style_topdown/*.png game/SeedField.Game/Art/style_topdown/ -Force
godot --headless --path game/SeedField.Game --import

# 真实 Godot 截图
godot --path game/SeedField.Game res://Scenes/StyleTest.tscn -- --route=oblique --zoom=mid --overlay=default --capture-art
godot --path game/SeedField.Game res://Scenes/StyleTest.tscn -- --route=topdown --zoom=near --overlay=tactical --capture-art
```

参数：`--route=oblique|topdown`、`--zoom=far|mid|near`、`--overlay=default|selected|tactical|debug`。

## 4. Flux 概念参考

本机 ComfyUI：`C:\Users\16275\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI`
实际 Python 环境：该目录下 `.venv`（torch 2.12.1+cu130；Desktop 的 `standalone-env` 不是运行环境）。
模型：`E:\AI_ComfyUI\Models`（Flux.1-dev、T5/CLIP、VAE）。

```powershell
# 启动 ComfyUI（本机已验证）
& "...\ComfyUI\.venv\Scripts\python.exe" main.py --listen 127.0.0.1 --port 8188 `
  --extra-model-paths-config "C:\Users\16275\AppData\Roaming\Comfy Desktop\shared_model_paths.yaml" `
  --disable-auto-launch --output-directory "E:\AI_ComfyUI\Output" --input-directory "E:\AI_ComfyUI\Input"

python art/comfy/generate_concept.py --prompt-file art/prompts/power_node_concept.txt `
  --seed 101 --width 1024 --height 1024 --name power_node_concept_101
```

- 概念图只用于形体/材质参考；正式资产必须回到 Blender 源。
- 尚未安装 LayerDiffuse/3D-Pack；当前 Flux 出图不带透明 alpha，也不需要。

## 5. 资产约定

- 源：`art/blender/*.py`；生成：`art/generated/`（gitignore）。
- 运行时：`game/SeedField.Game/Art/style_{route}/`；清单 `Art/manifest.json`。
- 所有运行时 PNG：512×512、RGBA/8bit、SHA-256 与清单一致；由 `ArtAssetTests` 自动检查。
- `status` 固定为 `style-test-not-final`；在 Gate 1 用户批准前不得当正式美术。
- 许可证：`original-procedural-source`；概念图放 `art/reference/concepts/`，不进入运行时。

## 6. 可借用的工具/技能

### 已在本机可用/免费
- Blender 5.2 Cycles + 本仓库程序化脚本（可复现、无插件依赖）。
- ComfyUI 0.30.2 + Flux.1-dev（概念/材质参考）。
- Godot 4.7 `CanvasTexture` + `PointLight2D` 可做 sprite normal/specular（下一步验证）。
- 社区 Blender：Sprite Sheet Maker、iso-render-pipeline、Spritify、KIT OPS Free、
  MACHIN3tools、Random Flow、BlenderKit、Node Wrangler。
- 社区 ComfyUI：LayerDiffuse（透明 PNG）、ControlNet Aux、IP-Adapter、
  ComfyUI-3D-Pack（StableFast3D/Hunyuan3D）、BiRefNet/RMBG、Ultimate SD Upscale。

### Agensi 付费技能（未购买，需用户批准）
- **Game Art Director Specialist**（$5）：把游戏承诺/镜头/参考转成可读视觉规则与资产 brief。
- **Game Sprite Sheet Pipeline**（$15）：帧对齐、图集、引擎 JSON 与一致性检查。
- **Local ComfyUI Image & Video Gen**（$10）：本机 ComfyUI 工作流执行。
- **comfyui-local-generator**（$5.9）：工作流/VRAM 审计与 OOM 修复。
- **Image to Game-Ready 3D**（$19）：概念图→优化 FBX，需 Blender MCP + Hyper3D。
- **Art Direction Style Bible**（$15）：品牌/受众→视觉路线与 shot-ready 规范。

当前没有购买任何技能；如需其一，告诉我，我会先确认价格/余额再加载。

## 7. 下一步 Gate

1. 用户在 A/B 对比表中选路线（或要求混合/重做形体）。
2. 选定后：给移动单位做 8 方向（若 A）或旋转 pivot（若 B）。
3. 补种子核心 `idle/regenerating/bootstrap/besieged` 四态。
4. 把 `StyleTest` 的验证结果迁移到真实 `Main` 的 `EntityVisual`/`TacticalOverlay`，
   默认关闭范围圈/ID，保留 F 键调试层。
5. 在真实战斗中接入 `ShotFired`/`EntityDestroyed`/断供事件；不改仿真。
