# M1 教程准入证据（2026-11-18）

## 变更

- 新增 `TutorialProgress`：把五分支最低可用从“蓝图覆盖”改为“仿真事实”。
- 新增 `TutorialSteps`：五步标题/要求/分支映射，供 Headless 与 HUD 共用。
- 新增 Headless `--tutorial` 演示。
- Godot HUD 新增教程面板与 `TUTORIAL n/5 · PvE READY/LOCKED` 摘要。
- 决策：ADR-0040。

## 判定规则

| 步骤 | 事实 |
|---|---|
| Energy | 己方存活设备有 `PowerModule.EnergyPerTick > 0` 且 `Energy > 0` |
| Sensing | 己方传感器通过 `VisibilityRules.IsSensedBy` 看到外国实体 |
| Communication | 收到 `MessageDelivered` 且接收文明为己方 |
| State | 带 `StateModule` 的己方设备产生移动/攻击/发信事件 |
| Execution | 己方存活设备产生 `EntityMoved` / `ShotFired` / `ShotMissed` |

效率、精度与构造复杂度不参与判定；其他文明事件不计入。

## 测试

```powershell
dotnet test tests/SeedField.Simulation.Tests/SeedField.Simulation.Tests.csproj `
  --filter "FullyQualifiedName~TutorialProgressTests"
```

结果：**10/10 通过**。覆盖：

- 只有 `PowerModule` 但未充电不算 Energy；`EnergyPerTick=0` 不算。
- 目标在传感器范围外不算 Sensing；己方实体不算。
- 发给其他文明的消息不算 Communication。
- 没有 `StateModule` 的设备行动不算 State。
- 其他文明的事件不计入。
- 五步完成后 `PveUnlocked=True`，首次完成 Tick 不被后续观察改写。
- 相同事实两次观察结果一致。

全量回归：

```powershell
dotnet test SeedField.slnx
```

结果：**396 通过 / 0 失败**（仿真 267 · Jint 102 · 仓库门禁 19 · Catalog 8）。

## Headless 演示

```powershell
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --tutorial
```

实际输出：

```text
TUTORIAL completed=5/5 energy=1 sensing=1 communication=2 state=2 execution=2 pveReady=True
```

## Godot 画面

```powershell
$env:DOTNET_gcServer = "1"
godot --path game/SeedField.Game -- --capture
```

实际输出：

```text
SEEDFIELD_READY args=--capture gcServer=True
SEEDFIELD_PRERUN tick=34 shots=4 entities=4
SCREENSHOT_SAVED .../screenshots/m0-combat.png (Ok)
```

截图 `game/SeedField.Game/screenshots/m0-combat.png` 显示左下教程面板：
五步全部勾选、Energy t1、Sensing t26、Communication t27、State t1、Execution t1、
`PVE READY · free world unlocked`；右上摘要为 `TUTORIAL 5/5 · PvE READY`。

## 未覆盖

- 教程进度尚未写入 GameSave；重开/读档会重新计算。
- 尚未实现非程序员默认控制器模板与第一组带目标/前置的构造关卡。
- 面板未做键盘开关与无障碍检查。
