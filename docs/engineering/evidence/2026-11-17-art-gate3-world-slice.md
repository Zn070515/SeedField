# 证据：Gate 3 工业废土世界切片（2026-11-17）

关联：ADR-0026、ADR-0024/0025、`docs/engineering/evidence/2026-11-17-art-gate2-complete.md`。

## 1. 范围

- 真实 `Main` 的表现层，不改 Simulation 世界状态、存档 schema、Jint、离线包。
- 本轮先解决审查指出的最大短板：环境、单位可读性、动态生命力。
- 状态仍为样片，`style-test-not-final`，不是正式美术验收。

## 2. 环境

新增 `Scripts/Presentation/WorldBackdrop.cs`，在 frontier 地图内绘制：

- 8×4 工业板块、接缝、冷灰蓝底色；
- 横向主路 + 三条纵向服务道、危险条纹；
- 26 个程序化遗迹块、5 个弹坑；
- 控制点八边形基座、危险环与边缘标记；
- `SECTOR 07 · INDUSTRIAL WASTE` 区域标签。

地形是程序化占位，不是正式地表材质；但它替代了此前的纯网格空白。

## 3. 动态表现

- `NetworkVisual.cs`：控制点到 A/B 出口的能量/信息连线；闪烁脉冲由 `World.Tick` 驱动，
  不使用真实时钟。核心 `EXIT` 线在核心有储备时出现金色脉冲。
- `EntityVisual`：
  - 单位屏幕尺度从 0.22 提升到 0.26；
  - 移动时按朝向绘制履带扬尘；
  - `ShotFired.TargetId` 命中后 6 Tick 闪红；`EntityDestroyed` 清理。
- `CoreVisual`：四态环 + 5 个随 tick 旋转的分支指示点。
- 默认画面仍只画 A 方自己与 `Observation` 已感知敌人；F10 全知调试保留。
- `PlayerEventFilter` 过滤 `ShotFired`/`MessageQueued`/`EntityDestroyed`/`DrawEventLog` 等
  事件流；未侦察敌人的弹道、通信线和日志不会出现。
- 网络供电脉冲要求 frontier 电网有能量且 `PowerGridState.BreakerOpen == false`；
  断路器手动打开后脉冲熄灭。
- `WorkshopPanel` 增加选中蓝图/实例的精灵预览；`m0-workshop.png` 显示 power 预览与 Mk3 升级。

## 4. 截图

```text
game/SeedField.Game/screenshots/art-main-combat.png
game/SeedField.Game/screenshots/art-main-tactical.png
game/SeedField.Game/screenshots/art-main-debug.png
game/SeedField.Game/screenshots/art-main-omniscient.png
game/SeedField.Game/screenshots/m0-workshop.png
```

## 5. 验证

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
python tools/build_art_manifest.py --check
godot --path game/SeedField.Game -- --capture-visual
godot --path game/SeedField.Game -- --capture-tactical
godot --path game/SeedField.Game -- --capture-debug
godot --path game/SeedField.Game -- --capture-omniscient
godot --path game/SeedField.Game -- --visibility-smoke
```

```text
SeedField.Repository.Tests: 18 passed
SeedField.Simulation.Tests: 230 passed
SeedField.Runtime.Js.Tests: 85 passed
VISIBILITY_SMOKE unseenB4=True omniscientB4=True ok=True
EVENT_VISIBILITY_SMOKE hiddenShot/visibleShot/destroyed/message ok=True
POWER_VISUAL_SMOKE before=True after=False energy=200 powered=False ok=True
```

Headless：

```text
Run 1 final hash: 34886c1bb93164a1
Run 2 final hash: 34886c1bb93164a1
```

## 6. 未完成

- 正式地表材质、建筑/墙体与运输网络资产。
- 工坊/程序编辑器大预览。
- 射击/断电/围困/恢复/模块升级的逐帧演出。
- 真实 Main 中 10/50/100 设备可读性与 FPS；VisualBench 只是开发机上限参考。
- 全方向法线/发光。
