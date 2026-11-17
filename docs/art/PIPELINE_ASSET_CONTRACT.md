# SeedField 美术生产管线与资源契约 v0.1

> 状态 `[PROPOSED]`，用于正式美术纵切片试制与数据格式讨论。以下字段是建议，不是对游戏存档/API 的变更。

## 1. 生产链

```text
形体语言 & 功能定义
  ↓
asset.source.blend（源模型、集合、固定摄影机、灯光、材质）
  ↓
Blender headless render（透明背景 RGBA，固定分辨率、统一镜头）
  ↓
PNG / normal(可选) / shadow(可选) / manifest.json
  ↓
Godot import → Sprite2D 或组合图层 → 仅表现层读取 State/Event
  ↓
实机截图（3 缩放）+ 视觉评审 + FPS/帧时记录
```

本轮不要求一次性实现整套美术编辑器；重要的是**最小资产可以被同一条命令稳定重导出**。

## 2. 推荐目录（可由 Agent 根据仓库工程规范调整）

```text
assets/
  source/
    core/seed_core.blend
    machines/worker_frame.blend
    infrastructure/power_connector.blend
    materials/material-library.blend (optional)
  runtime/
    core/seed_core.png
    machines/worker_frame.png
    infrastructure/power_connector.png
  manifests/
    seed_core.json
    worker_frame.json
    power_connector.json
  licenses/
    SOURCES.md
```

`assets/runtime` 是可再生导出物。只有经许可且能记录来源的资源可以进入发行包；无源文件的临时 AI 图只能留在参考目录，不能冒充正式可编辑资产。

## 3. Godot 视觉对象与资产映射

| 真实状态字段 | 只读视觉表现 | 不允许的行为 |
|---|---|---|
| `EntityState.BlueprintId/Version` | 机架 + 对应模块拼装 | 美术推断蓝图能力并写回状态 |
| `Health/MaxHealth` | 损伤贴图/部件缺失/指示器 | 粒子效果自己扣 HP |
| `Energy` / 电网诊断 | 指示灯、能量流、停机动画 | 根据光效决定是否供能 |
| `MessageQueued/Delivered` | 不泄密的发送/接收提示 | 画出未接收的真实消息内容 |
| `SeedCore` 只读状态 | 内核/保护层/复建状态视觉 | 让动画本身触发生成实体 |
| `ShotFired` / `EntityDestroyed` | 弹道、闪光、残骸 | VFX 自己做命中判定 |

材质和 Shader 仅决定显示；仿真与表现应保持可独立运行。

## 4. Asset Manifest 样例（仅展示设计，不改变 Save Schema）

```json
{
  "schemaVersion": 2,
  "assetId": "seed_core.oblique.v0",
  "kind": "style-test",
  "route": "oblique",
  "source": "art/blender/render_style_assets.py",
  "resolutionPx": [512, 512],
  "footprintPx": 420,
  "pivot": [0.5, 0.84],
  "directions": 2,
  "status": "style-test-not-final",
  "files": [
    {
      "path": "style_oblique/seed_core_oblique_000.png",
      "sha256": "...",
      "alphaBBox": [62, 138, 450, 431]
    }
  ],
  "states": ["idle", "regenerating", "bootstrap", "besieged"],
  "license": "original-procedural-source"
}
```

`pivot` 是归一化锚点：斜俯视取正面帧 alpha 底边中心，严格俯视取画布中心。
`alphaBBox`、尺寸、方向帧与状态必须和实际导出一致，由
`ArtAssetTests` 解码 PNG 校验；不要让 Agent 随机指定参考图片像素当实际资产坐标。
清单由 `python tools/build_art_manifest.py` 生成，不手工抄哈希。

## 5. 首批三资产要满足

1. **种子核心**：至少一套轮廓/材质样片，能表现围困但不误导为核心可死。
2. **工体**：一个机架 + 两个可交换模块，切换后轮廓明显变化；验证转向和透视。
3. **能源接口**：有可观察的输入、输出与断电状态；连接图形不必真实模拟物理电流。

建议将可组合结构拆为 `base / module / status_fx / selection` 等层，而不为每个组合直接手工合成一张成品图。形式要根据最终相机路线实测调整。

## 6. 贴图、法线与光照

Godot 4.7 的 2D 渲染提供 `CanvasTexture` 的 diffuse/normal/specular 贴图入口，适合在同一套预渲染精灵上实现少量实时光照。实际使用是否必要应通过截图与 GPU 帧时评估。

- 不必所有贴图都上法线，先在核心和工体样片验证。
- 光效优先说明当前状态：能量是否供应、警报、通信是否通畅。
- 避免多层 bloom 将机械细节淹没。
- 远景可退化细节、保持轮廓和状态符号；高频特效可降级。

参考官方：
- https://docs.godotengine.org/en/4.7/tutorials/2d/2d_lights_and_shadows.html
- https://docs.godotengine.org/en/4.7/tutorials/shaders/shader_reference/canvas_item_shader.html

## 7. 首个导出工具

`tools/art/render_asset.py` 为 **Blender 后台单图导出器**，强制调用用户指定的正交相机、透明背景 RGBA 和固定画布尺寸，同时生成哈希与配置记录；
它**不是**“自动设计优质机械外观”的模型生成器，真正的机械形体仍需在 .blend 中按 Art Bible 设计。

运行示例（Windows PowerShell，使用已安装的 Blender 路径替换 `blender`）：

```powershell
blender -b assets/source/core/seed_core.blend `
  -P tools/art/render_asset.py -- `
  --asset-id seed_core.v1 `
  --camera ART_CAMERA `
  --output assets/runtime/core/seed_core.png `
  --size 512
```

输出为 `seed_core.png` 与 `seed_core.png.meta.json`；必须在本机检查透明度、实际方向与边距。该工具不自动写入 Godot 工程状态。

## 8. 资产校验 / 可持续维护

- 资产 ID 唯一；版本变更可追踪；保留旧版本以便现有存档/重放一致显示。
- 源文件、导出脚本、许可证、Godot 用途全部可追溯。
- 所有可交互/可损毁设备必须具有可读状态，不只靠颜色区别。
- 后续引入 AI 工具时，记录原始生成方式、人工修改、商用授权与 Steam 对生成式内容的适用披露要求。
- 工具脚本本次仅可做 Python 语法检查；**缺少 Blender/Godot 不得标记完整视觉验收通过**。

## 9. 导出完整性门禁（不代替视觉验收）

```powershell
python tools/art/check_render_asset.py assets/runtime/core/seed_core.png
```

该命令检查：PNG 签名与 IHDR、RGBA/带 alpha 像素类型、画布分辨率、Manifest 与文件哈希一致。它**不能**证明贴图美观、构图合理、像素每处透明、模型与游戏坐标对齐或版权合法。
