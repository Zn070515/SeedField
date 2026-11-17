# ADR-0025：方向数、模块剪影、核心四态与 mask 验证

- 状态：**Accepted（样片阶段）**
- 日期：2026-11-17
- 关联决策：ART-04、ART-05、ART-06、ART-07
- 前置：ADR-0024

## 背景

Gate 2 已完成世界空间相机、接触阴影、核心/工体轮廓分化与真实 Main 接入的
第一阶段。剩余五项需要一次收口：

1. 移动单位到底做 4、8 还是 16 方向；
2. 核心 `idle/regenerating/bootstrap/besieged` 四态；
3. 同机架两种可换模块；
4. `EntityVisual`/`TacticalOverlay` 进入真实 Main；
5. 法线/发光 mask 与 1/10/50/100 设备性能。

## 决定

### 1. 方向数

- 用同一台不对称 drill 工体渲染 16 个角度（22.5° 步进），生成
  `art/renders/direction-4-8-16.png`。
- 4 方向体积约 422 KB、8 方向约 882 KB、16 方向约 1795 KB（512px）。
- **移动工体锁定 8 方向**；建筑/核心保持 1–4 方向。16 只用于对比，不量产。
- 8 方向资产：`worker_frame`、`worker_frame_drill`、`worker_frame_sensor`、
  `worker_frame_weapon`。

### 2. 核心四态

- `CoreVisualStateRules` 在 `SeedField.Simulation` 中做纯分类，不改世界状态。
- 优先级：`Absent` → `Besieged` → `Bootstrap` → `Regenerating` → `Idle`。
- `Regenerating` = 任一储备低于上限；`Bootstrap` = `EntityBootstrapped` 后 12 Tick。
- 四张 512 RGBA 正面资产；共享 pivot/footprint，由 `ArtAssetTests` 校验。
- 默认 PvE 场景 frontier 有 B 实体，按现有 `CoreShellRules` 真实处于 besieged；
  其余三态用 peaceful 场景（不部署 B strike）做真实状态截图，不伪造状态。

### 3. 模块

- `worker_frame`：空插槽；`worker_frame_drill`：大型挖掘臂；
  `worker_frame_sensor`：桅杆/阵列；`worker_frame_weapon`：炮塔/炮管。
  四者同底盘、同 pivot、8 方向。
- 场景新增 `scout` Mk3 `Breacher`（同机架，Sensor + Weapon），通过现有
  `UpgradeCommand` 切换；`VisualEntitySnapshot.VisualAssetName` 由模块决定：
  Weapon→weapon，Sensor→sensor，否则 base。
- `--module-swap-smoke` 断言 sensor→weapon；Blender 表 `module-swap.png` 提供
  四态剪影。
- 武器外观独立为 `worker_frame_weapon`；不再用 `HasWeapon ? drill` 推断
  机体，drill 只留给未来采矿蓝图。

### 4. Main 接入

- `EntityVisual`、`CoreVisual`、`TacticalOverlay`、`DebugOverlay` 只接收
  `VisualEntitySnapshot`/帧数据，不接收可变 `WorldState`。
- 普通画面只画 A 方自己与 `ObservationBuilder.Build(world,"A")` 已感知的敌方；
  F10 / `--omniscient-debug` 才是显式全知。
- 核心在独立区域 `core-a`，Main 用左侧 `CORE-A REGION` 面板 + `EXIT` 链接展示，
  不把核心本体画在 frontier 坐标。
- 默认：精灵 + 血/能量条 + 核心状态；不画范围圈/实体 ID。
- F7：sensor/weapon 范围、控制点半径、ID；F8：sprite bounds/pivot、
  tick/FPS/坐标。
- `--classic-visuals` 保留旧圆点绘制作为回退；F1–F6/F12 不变。
- Main 仍是固定屏幕映射（1280×720、`MapScale`），不引入第二套 Camera2D；
  世界空间相机验证留在 StyleTest。

### 5. 法线与发光

- 法线：Cycles Normal pass 经 WORLD→CAMERA 变换、Raw 视图、Z 翻转后输出
  RGBA；emissive：纯黑遮蔽 + 纯 Emission 物体渲染，供 additive 叠层。
- 只做正面帧，控制体积；`ArtAssetTests` 校验 pass 路径/SHA/尺寸/alphaBBox。
- `VisualBench` 用 `CanvasTexture` + `PointLight2D` + `CanvasModulate` 验证；
  Main 暂不启用 2D 灯光，避免 UI/可读性回归。
- 开发机（RTX 4090）1/10/50/100 设备、masks on/off 结果见
  `docs/engineering/evidence/2026-11-17-art-gate2-masks-benchmark.md`；
  100 台 masks-on 约为 masks-off 的 56% FPS，但仍远高于 60。

## 非目标

- 不做 16 方向量产的完整游戏图集；不做全方向法线/发光。
- 不把 VisualBench 的 FPS 当最低配置；不测 4K/其他 GPU。
- 不改 Simulation 规则、存档 schema、Jint、离线包安全契约。
- 不合并 PR #4，不替换 main 渲染。

## 后果

- 工体资产从 4 方向增加到 8 方向，PNG 体积约翻倍；换取旋转连续性和模块剪影。
- 核心四态可在真实状态驱动下切换；默认 besieged 是当前 PvE 场景的真实结果。
- Main 默认更干净；F7/F8 可恢复完整诊断信息，旧圆点渲染仍可回退。
- mask 只验证正面帧；正式量产前需要补全方向、法线与性能门槛。
- 新增结构门禁：manifest schemaVersion 3、方向/状态/pass 校验，防资产漂移。
