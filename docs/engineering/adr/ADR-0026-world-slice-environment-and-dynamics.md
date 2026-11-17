# ADR-0026：Gate 3 工业废土环境与动态表现

- 状态：**Accepted（样片阶段）**
- 日期：2026-11-17
- 关联决策：ART-08、ART-09、ART-10
- 前置：ADR-0024、ADR-0025

## 背景

Gate 2 已把设备、核心四态、模块和 mask 接入真实 `Main`，但实际战斗截图仍以
网格、空白地图和少量圆点为主。审查确认三个问题：设备在正常视距偏小、环境
落后于设备、动态表现不足。

## 决定

### 1. 程序化工业废土底图

新增 `WorldBackdrop`：可平铺工业混凝土地表/道路材质、板块、主路/服务道、遗迹、
弹坑、控制点基座、危险条纹。它只读地图常量和控制点位置，不修改世界状态。
`tools/generate_terrain_tiles.py` 生成 512px seam-free albedo + normal；
第一批 Blender topdown 世界道具（`ruin_block`、`pipe_junction`、`wreck_hauler`）
叠在底图上，减少纯矩形板块感。

### 2. 单位尺度、镜头与细节层级

- 设备屏幕尺度从 0.22 提升到 0.26。
- Main 支持滚轮缩放 0.70–1.35、中键拖拽平移；`--view=far|mid|near` 用于截图。
- 细节层级：near 显示状态条，far 隐藏；HUD 显示当前 view/tier。
- 默认 HUD 不再显示 LEGEND/EVENT LOG；F7/F8 才打开战术/调试信息。
- 不引入 Main 的第二套 `Camera2D`；仍是对世界坐标做统一变换，HUD 固定在屏幕空间。

### 3. 状态驱动而非循环特效

- 能量/信息连线由 `World.Tick` 推进脉冲；核心 `EXIT` 线由真实储备决定是否亮起。
- 命中闪红来自 `ShotFired.TargetId`，持续 6 Tick；销毁事件清理。
- 移动扬尘来自实体位置变化（表现层跟踪上一帧位置），不修改仿真状态。
- 核心分支指示点由 tick 旋转；四态仍由 `CoreVisualStateRules` 分类。
- 不使用 `DateTime`/真实时钟/随机每帧动画。
- `VisualEffectFeed` 增加断电/恢复/施工/部署/升级/摧毁的短时 tick 环。
- `WorkshopPanel` 增加选中蓝图/实例的精灵预览，减少“只看文字”的装配感。
- `--benchmark-main=N` 在真实 Main 渲染路径中加入 N 个纯表现 decoy 精灵，
  记录 10/50/100 的 FPS/process/draw calls；不改世界状态。
- `--benchmark-jint=N` 在 peaceful 场景部署 N 个真实设备控制器，执行 30 Tick，
  记录 avgTickMs/ticksPerSec/faults；这是真实 Jint 混合负载。

### 4. 情报边界保持

- 普通画面只画己方 + `Observation` 已感知敌方；F10 / `--omniscient-debug`
  才是显式全知调试。
- 地形/网络/控制点属于公共地图信息；不通过连线或标签泄露未感知实体。
- 事件流也走 `PlayerEventFilter`：未侦察敌人的射击、通信和日志不进入普通画面；
  F10 全知模式才显示完整事件。
- 网络脉冲除库存外还检查断路器是否闭合；断路器打开后不再显示供能。

## 非目标

- 不做完整建筑群/运输网络实体；地表目前是可平铺材质 + 道具层。
- 不做程序编辑器大预览；工坊只做选中项的小精灵预览。
- 不做全部事件（断电、围困、恢复、模块升级）的逐帧演出。
- 最低配置未验证：只有 RTX 4090 / OpenGL compatibility；`--benchmark-main` 是表现层负载，
  `--benchmark-jint` 是真实 Jint 混合负载，但都不是最低配置结论。

## 后果

- 默认画面从“仿真监视器”更接近“工业废土战场”，但仍保持样片标记。
- 程序化底图增加了每帧绘制量，但远小于精灵/UI；正式性能仍需在目标硬件复测。
- 动态效果完全由仿真 Tick/事件驱动，可回放、可截图，不引入非确定性。
- 后续替换正式地形/建筑资产时，`WorldBackdrop` 是单一回退点。
