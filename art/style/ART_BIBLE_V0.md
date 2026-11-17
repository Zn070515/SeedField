# SeedField Art Bible v0

> 状态：`[PROPOSED]` 视觉方向，用于样片验证；不是已冻结的美术验收。
> 依据：`docs/design-bible/v1.9-rev2/05-PRESENTATION/ART_DIRECTION_AND_VFX.md`、
> `UI_UX_AUDIO_AND_ACCESSIBILITY.md`；对标 Factorio / Shapez 2 / Cosmoteer /
> Desynced / Space Haven 的公开管线与美术方向。

## 1. 一句话方向

**冷静的工业数字遗迹 + 高辨识度模块化硬件 + 暖核心 / 冷网络的光色对比。**

不是霓虹赛博朋克，也不是纯像素复古；是“可维护的工业设备在数字废土上重新生长”。

## 2. 四条不可妥协的视觉原则

1. **可读性优先于炫技**：30% 缩放下仍能分辨能源、感知、通信、状态、执行五类设备。
2. **轮廓先于细节**：先做剪影，再加材质/贴花/发光；远看靠形状，近看靠材质。
3. **固定镜头、固定尺度**：设备在固定正交镜头下渲染；不允许每台设备不同透视。
4. **颜色不是唯一通道**：断供/过载/故障同时用形状、纹理、图标与文字表达。

参考：Shapez 2 的 Readability 原则；Factorio 的固定镜头 + 剪影/对比层级；
Desynced 的模块化机器人外观随模块变化；Cosmoteer 的 2D 模块/法线工作流。

## 3. 镜头与尺度

| 项 | 选择 `[PROPOSED]` |
|---|---|
| 设备镜头 | 正交/轻微 3/4：仰角约 50°，方位 45°；先做 1 方向，再验证 4/8 方向 |
| 世界镜头 | Godot 2D 俯视/等距叠加；与设备镜头一致的地面网格 |
| 源分辨率 | 512×512（后续 1024 备选）；游戏内 128/256 px 档 |
| 设备格 | 1 格约 64 px；大型设备 2×2/3×3 格 |
| 光源 | 固定世界主光 + 冷色补光 + 轮廓光；不随镜头旋转 |
| 背景 | 深蓝黑；设备阴影/接触暗部烘焙进 sprite 或单独阴影层 |

待验证：严格俯视 vs 3/4；4 方向是否足够；最低缩放下 2×2 设备的可辨性。

## 4. 色板（v0）

| 用途 | 色值 | 说明 |
|---|---|---|
| 世界底色 | `#0B1220` → `#111C2B` | 深蓝黑，低饱和 |
| 地形/遗迹 | `#2A3644`、`#3E4C5A` | 冷灰蓝，避免与设备抢焦点 |
| 玩家 A | `#43D9E8`（青） | 能源/己方高光 |
| 对手 B | `#F2A33A`（琥珀） | 不采用纯红，避免色盲/敌我误读 |
| 能源 | `#5FF2FF` + 白 | 连续光流 |
| 信息/通信 | `#B48CFF`（紫） | 脉冲/虚线；与能源区分 |
| 材料/物流 | `#D9A45B`（赭） | 实体箱/输送 |
| 警告 | `#FFB020` + 斜纹 | 过载/低储备 |
| 故障 | `#FF5A4E` + X/断线 | 必须加图标/文字 |
| 核心区 | `#F4E6C8`（暖象牙）+ `#E8B85C` | 永久、稳定、与设备区分 |
| 中性金属 | `#8A97A5` | 设备主体 |
| 深色橡胶 | `#121820` | 底座/线缆 |

## 5. 五分支剪影语言

| 分支 | 剪影母题 | 关键识别点 |
|---|---|---|
| 能源 Power | 竖向圆柱 + 散热鳍 + 顶部环 | 发光核心、环形指示灯 |
| 感知 Sensing | 碟/多面天线 + 细杆 | 朝向性、扫描弧 |
| 通信 Comm | 桁架/桅杆 + 多向天线 | 网格/脉冲、方向性 |
| 状态 State | 立方/堆叠散热片 | 密集栅格、冷却槽 |
| 执行 Execution | 角状炮/机械臂 + 外露关节 | 枪管/关节、朝向 |
| 核心 Core | 同心环 + 悬浮种子 | 暖色、慢脉冲、明显更大 |

模块安装位统一为底座上的凹槽/接口环；换模块时剪影必须变化，而不是只换颜色。
参考 Desynced 的“外观随模块变化”与 Cosmoteer 的模块网格。

## 6. 材质与光照

- 主材质：拉丝金属（低粗糙度变化）、深色橡胶、磨砂塑料、少量玻璃/发光条。
- 倒角/边缘高光用于读体积；AO 用于接触阴影；不要用纯描边替代体积。
- 发光只用于状态（能源、通信、故障），不用于装饰性满屏霓虹。
- 设备底座与地面有明确接触暗部；悬浮设备（核心）用反向光/粒子区分。
- PBR 源文件保留；导出 sprite 时同时保留 albedo/emission/roughness/法线候选。

## 7. 网络与状态视觉语言

| 事件 | 形状/运动 | 颜色 | 非颜色冗余 |
|---|---|---|---|
| 能源流 | 连续脉冲沿线前进 | 青 | 箭头/亮度节拍 |
| 信息流 | 小 packet 跳跃/虚线 | 紫 | 方块/信封图标 |
| 带宽拥堵 | 队列堆叠/脉冲变慢 | 紫 + 警告边 | 队列数字/堆叠条 |
| 物流 | 实体箱沿链路移动 | 赭 | 箱形/重量刻度 |
| 断联 | 线断裂 + 两端闪烁 | 红/灰 | 断口 X、NO LINK |
| 过载 | 线宽/亮度抖动 + 热纹 | 橙 | 斜纹、HEAT 标签 |
| 控制器故障 | 方形 glitch + 停顿 | 红/品红 | 故障码/行号 |
| 核心恢复 | 同心环由外向内充能 | 暖象牙/金 | 百分比、阶段文字 |

## 8. UI / HUD

- 深色工业面板、细线、等宽数字；少用大色块。
- 关键数字（E/M/C/S/B、配额、断供）使用高对比；状态标签始终带文字。
- 工坊/程序编辑器保留“真实截图”验收，不用概念图代替。
- 色盲模式：切换调色板 + 形状/图标；不依赖红绿。

## 9. 制作管线

```text
Art Bible → Blender 灰模/剪影 → 材质/灯光 → 渲染 passes
→ sprite atlas + pivot JSON → Godot CanvasTexture/Shader
→ 事件驱动动画 → 截图/性能回归
```

- 源文件：`art/blender/*.py`、`.blend`（可选）、材质/灯光参数。
- 中间产物：`art/generated/`（gitignore）。
- 交付/证据：`art/renders/`（contact sheet、最终 sprite）。
- 每个资产记录：源脚本、渲染命令、许可证、分辨率、pivot、方向数。
- 不用 AI 生成图直接当最终 sprite；AI 只做概念/材质参考，最终必须回到
  Blender/手绘/可复现管线。

## 10. 样片验收清单

1. 一台模块可替换的能源设备：远/近景、1 方向 + 4 方向。
2. 一段能源/通信网络：供能、断供、拥堵、故障四态可一眼分辨。
3. 一次武器/护盾命中 + 断供反馈。
4. 10–50 台设备同屏：最低缩放下可读、GPU/显存有数据。
5. 工坊/程序编辑器真实截图，不用概念图。

## 11. 当前样片（本轮）

- Blender 能源节点 `power_node`：4 方向 + 1 断电视觉，512×512。
- 输出：`art/generated/power_node/`；contact sheet：`art/renders/power_node_sheet.png`。
- 下一步：法线/发光 mask、4/8 方向 sprite atlas、Godot CanvasTexture 验证。

## 12. 参考与来源

- Factorio FFF #146 GFX workflow：Blender → Photoshop → After Effects → spritesheet；
  固定正交镜头与剪影/对比层级。<https://factorio.com/blog/post/fff-146>
- Factorio Art Direction & Visual Language 分析：
  <https://teemo.dev/game-design/factorio/systems/art-direction-and-visual-language/>
- Shapez 2 visual devlogs（Readability / open buildings / 四原则）：
  <https://steamcommunity.com/games/2162800/announcements/detail/3815172611073666621>
- Cosmoteer 风格指南（模块 sprite、法线、参考取色）：
  <https://cosmoteer.wiki.gg/wiki/Making_Cosmoteer-style_visuals>
- Desynced 美术/模块化机器人参考：
  <https://illogicoma.artstation.com/projects/x3lWoW>
- Space Haven（2D 等距/俯视、复古未来、模块化船舱）：
  <https://store.steampowered.com/app/979110/Space_Haven/>
- Blender sprite 工作流/插件：Sprite Sheet Maker
  <https://extensions.blender.org/add-ons/sprite-sheet-maker/>；iso-render-pipeline
  <https://github.com/craigrmccown/iso-render-pipeline>
- Godot 2D normal map/CanvasTexture：
  <https://forum.godotengine.org/t/render-only-the-normal-maps-of-a-2d-scene/82354/11>
- ComfyUI LayerDiffuse / 3D Pack：
  <https://github.com/huchenlei/ComfyUI-layerdiffuse>、
  <https://github.com/mrforexample/ComfyUI-3D-Pack>
