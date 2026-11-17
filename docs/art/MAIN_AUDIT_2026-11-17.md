# SeedField `main` 美术强刷审查 / 2026-11-17

> 基准 SHA：`1d5e580eaeecbf3a4df681a1a6249c934c6bd3d3`；依据为实际 GitHub 文件树、源代码、`screenshots/` 四类画面及原仓库工程记录。
> **本审查仅讨论美术/交互表现，不对最新 CI 成败、性能帧率、构建结果做未经运行的断言。**

## A. 根本判断

当前显示层是**具有实际仿真输入的诊断可视化**，并非经过视觉设计的正式游戏画面。
这是符合前期 M0/M1 的工程状态，但不能继续直接增添更多同心圆、直线、深蓝浮层来承载后续系统。

## B. 源码证据

| 位置 | 已观察到的实现 | 美术风险 | 处置 |
|---|---|---|---|
| `game/SeedField.Game/Scripts/Main.Draw.cs` | `DrawGrid`, `DrawStagingZones`, `DrawControlPoint`, `DrawRanges`, `DrawTracers`, `DrawMessages`, `DrawEntities`, `DrawHud` | 主世界内容几乎全部由几何图元+文字构成 | 保留为调试覆盖层；世界/实体改为可复用视觉资源 |
| `DrawEntities` | 以 `DrawCircle`, `DrawArc`, `DrawBar`, `DrawString` 表现设备 | 不同模块、机架、文明缺少可辨识形态 | 用实体视觉节点/贴图表现并保留状态叠层 |
| `DrawRanges` | 为传感与武器常显圆环 | 大范围圆线覆盖地形/交火 | 默认隐藏，选中时或战术视图显示 |
| `DrawGrid` | 均匀背景网格/大边框 | 缺失地形、材料、尺度层级 | 真实区域地形；网格可切换 |
| `game/SeedField.Game/Scripts/WorkshopPanel.cs` | `ItemList` 展示蓝图、实例、订单 | 工坊像数据库管理器，不像构造台 | 添加实体大预览/插槽/状态差异；保留列表作为辅助 |
| `ProgramEditor.cs` | 文本编辑与部署按钮 | 功能能用，仍缺设备上下文和视觉反馈 | JS 编辑器暂缓全面重做，先改善状态/错误层级 |
| `PowerPanel.cs` / `RecoveryPanel.cs` / `DiplomacyPanel.cs` | 以原生列表、长文本与按钮为主 | 信息密度高，缺可视化结构 | 先建立统一控件语言，再按高频使用优先级重做 |
| `assets/README.md` | 仅声明 source/runtime 资源约定 | 没有实际可复用造型、材质与模型库 | 补真实 Blender 源资产及导出元数据 |
| `Scenes/Main.tscn` | 单根 Node2D 实例 | 视觉节点复用与层级管理不足 | 先提取 Presentation 模块，避免重构仿真 |

## C. 真实截图审查

- `m0-combat.png`：单位和地点均被圆形表示；中心只有小面积交火，射程圆和调试信息占据注意力；更像仿真仪表板。
- `m0-power.png`：长文本/列表表达断电状态，尚无法从世界里直接认出被断供设备及供电关系。
- `m0-recovery.png`：核心区有恢复操作界面，但战场上没有足够强的“不可摧毁核心建筑”视觉锚点。
- `m0-diplomacy.png`：交涉信息密集，缺少文明身份/信誉/交换关系的可读性结构。
- 程序编辑器和工坊截图仍是清晰的功能原型；本阶段先避免整体重写它们。

## D. 问题优先级

### P0 — 开工本轮必须解决

1. **视觉主体缺失**：只有“能打的彩色点”，没有玩家愿意建造、保护与升级的实体造型。
2. **世界场景缺失**：没有真实区域地表、设施基底、废墟、工业网络与局部光影层次。
3. **表现层耦合**：直接绘制既承担实体又承担战术标注，阻碍切换细节与缩放。
4. **资产生产零基线**：没有正式 3D/2D 可编辑源与固定相机、照明、导出管线。

### P1 — 本轮应展开样片验证

5. UI 信息层级：常驻 HUD、场景选择信息、详细面板和开发日志分离。
6. 五分支在远景下的形态语言，不依赖颜色区分。
7. 资源状态的“物理反馈”：断供、通信失联、过载、受损、建造进度。
8. 兼顾低缩放、多单位、色觉障碍和截屏一致性。

### P2 — 本轮禁止扩张

- 全部科技树设备、几十套战构体皮肤、完整地形生物群系、终局武器、新 UI 框架、全平台国际化重做。

## E. 关键设计判断

当前主地图使用 `16000 x 8000 mm`、画布 `1280x720`，此前单位以半径约 `6.5px` 的圆点显示。即使换成高分辨率材质，**若默认相机下主体仍然只有 13px 左右宽，实际改善也会很弱**。

因此应同时测试**镜头尺度/设备屏幕占比**，而非只替换图片。

调试层必须保留，可在正式画面上按键切换；但玩家默认看到的是设备与世界，而不是 `TICK / authoritative / faults`。

## F. 不得破坏的行为与边界

- `SimulationEvent` 依然只由权威 `SeedField.Simulation` 生成；光束、粒子、碰撞外观不决定战斗结果。
- 玩家观察权限仍通过 `Observation`，普通视图不能显示未被侦察的敌方真实位置。
- 固定 Tick 与画面帧率分离；动画应按事件时间、世界状态插值或渲染层时间驱动。
- `F1` / `F2` / `F3` / `F4` / `F6`、空格、单 Tick 与截图流程尽可能兼容，避免美术重构破坏实际功能。
- 无服务器、离线游戏、Jint 4.18.0 与存档体系均不在本轮改动范围。

## G. 原始证据链接

- 仓库： https://github.com/Zn070515/SeedField
- 绘制层： https://github.com/Zn070515/SeedField/blob/main/game/SeedField.Game/Scripts/Main.Draw.cs
- 战斗： https://github.com/Zn070515/SeedField/blob/main/game/SeedField.Game/screenshots/m0-combat.png
- 电网： https://github.com/Zn070515/SeedField/blob/main/game/SeedField.Game/screenshots/m0-power.png
- 恢复： https://github.com/Zn070515/SeedField/blob/main/game/SeedField.Game/screenshots/m0-recovery.png
- 外交： https://github.com/Zn070515/SeedField/blob/main/game/SeedField.Game/screenshots/m0-diplomacy.png

> 注意：这些链接随 `main` 后续变动；审查结论来自顶部锁定的 SHA。
