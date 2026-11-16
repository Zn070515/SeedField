# 证据：控制点 PvE 目标（2026-11-16）

## 实现

- `ControlPointState`：区域、位置、半径、驻留 Tick、当前占领文明、进度、owner。
- 世界支持多个控制点；`MatchRules.CapturedPointsToWin` 定义胜利阈值。
- 每个 Step 结算：
  - 只有**唯一文明**的实体在圈内时累计进度；
  - 争夺（多文明）或无人在圈内 → 进度清零；
  - 达到 `HoldTicksRequired` → `ControlPointCaptured`，记录 owner；
  - 任一文明 owner 数达到阈值 → `MatchEnded`。
- 控制点/胜负进入世界哈希、存档（schema v3）与加载校验；设备观察只向同区域设备暴露控制点。
- Godot HUD 绘制控制圈、当前占领/进度/争夺状态；`--capture-victory` 跑到占领后截图。

## 构建与测试

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 100 passed
SeedField.Runtime.Js.Tests: 23 passed
```

新增覆盖：

| 用例 | 断言 |
|---|---|
| 独占驻留 | 3 Tick 后 owner=A，事件产生 |
| 争夺 | 进度保持 0，不产生 owner |
| 离开半径 | 已有进度清零 |
| 多点击败 | 占 2 点后 `MatchEnded`，winner=A |
| 存档迁移 | v2 单点 JSON 迁移到 v3 点集合，哈希一致 |
| 设备观察 | 同区域设备能看到多个控制点 |

## Godot 实测

```powershell
godot --path game/SeedField.Game -- --capture-victory
```

```text
tick 51  victory A
MATCH · WINNER A
WEST · A / CENTER · A / EAST · B
```

![Control point victory](../../../game/SeedField.Game/screenshots/m0-victory.png)

## 未覆盖

- 跨区域目标与区域级胜负/外交/投降。
- 控制点被争夺后的衰减/锁定规则。
- 目标是否应始终对全文明可见（当前只对同区域设备可见）。
