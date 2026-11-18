# Godot 客户端

`game/SeedField.Game` 是 M0/M1 可视切片：**Godot 4.7.2 Mono + .NET 10**，引用 `SeedField.Simulation` 与 `SeedField.Runtime.Js`。C# 仿真仍是唯一权威，Godot 只渲染 `SimulationEvent`；A 方 JavaScript 可编辑热部署，工坊可部署蓝图并显式升级实例。

## 运行

```powershell
dotnet build game/SeedField.Game/SeedField.Game.csproj
godot --path game/SeedField.Game                        # 实时运行
godot --path game/SeedField.Game -- --capture            # 战斗截图
godot --path game/SeedField.Game -- --capture-editor     # 编辑器截图
godot --path game/SeedField.Game -- --capture-workshop   # 工坊截图
godot --path game/SeedField.Game -- --opponent-turtle    # 使用 Turtle 内置对手
godot --path game/SeedField.Game -- --benchmark-mixed=1000 --benchmark-mixed-seconds=10
```

截图输出：`game/SeedField.Game/screenshots/` 下的 `m0-*` 与 `art-*` 文件。
正式运行建议用 `pwsh tools/run-game.ps1`（设置 `DOTNET_gcServer=1`）。

## 结构

| 路径 | 内容 |
|---|---|
| `Scenes/Main.tscn` | 单场景入口 |
| `Scenes/MindustryStyle.tscn` | 废土正交俯视美术样片（独立，无仿真） |
| `Scripts/Main.cs` | 场景生命周期、仿真步进、事件 HUD、保存/读取、截图 |
| `Scripts/ProgramEditor.cs` | A 方 JavaScript 编辑器与部署按钮 |
| `Scripts/WorkshopPanel.cs` | 五分支覆盖、蓝图部署与实例升级 |
| `Scripts/DiplomacyPanel.cs` / `PowerPanel.cs` / `RecoveryPanel.cs` | F3 外交 / F4 电网 / F6 核心恢复 |
| `Scripts/Scenarios.cs` | M0/M1 世界、蓝图与两个 JS 控制器脚本 |
| `screenshots/` | 真实运行截图 |

## 操作

`SPACE` 暂停 / 继续 · `N` 单 Tick · `R` 重置 · `F1` 程序编辑器 · `F2` 工坊 · `F3` 外交 · `F4` 电网 · `F5` 保存 · `F6` 核心恢复 · `F9` 读取 · `F12` 截图 · 滚轮缩放 · 中键拖拽平移。

## 程序部署

- 编辑器打开时默认暂停；关闭后继续。
- 部署会新建 Jint 控制器并原子替换当前版本，版本号 +1。
- 编译/加载失败时保留旧控制器与旧版本，错误显示在编辑器状态栏。
- Godot 只提交脚本文本；控制器仍在 `SeedField.Runtime.Js` 中执行。

## 工坊与蓝图

- `F2` 打开工坊；编辑/浏览时暂停。
- 每个蓝图版本不可变；发布 v2 不会改变已部署实例。
- 部署生成新实例并记录 `BlueprintId + Version`；升级必须显式执行。
- 五分支覆盖由蓝图注册表计算；`5/5` 时 HUD 显示 `PvE READY`。
- 部署从区域本地库存扣除蓝图 M 成本；不足时拒绝并显示原因。
- 收件箱受己方 `StateModule.StorageCapacity` 总和控制，超限时最旧消息过期。

## 美术样片（废土正交俯视）

- 运行：`godot --path game/SeedField.Game res://Scenes/MindustryStyle.tscn`
- 截图：`... -- --capture-art` → `screenshots/art-mindustry-style.png`
- 严格 90° 俯视、64 px 地块、四类地貌、四种矿脉、块状锈蚀建筑与旋转单位；全部为程序化占位。
- 这是 ADR-0041 的风格样片；`Main` 仍是旧斜俯视样片，迁移顺序见 ADR。

## 教程准入（M1）

- 左下 `TUTORIAL` 面板列出 Energy / Sensing / Communication / State / Execution 五步，
  以及 `AND · Minimum Network` / `OR · Field Awareness` 两个构造挑战。
- 完成条件来自仿真事实（发电充电、传感器看到外国实体、消息送达、带 State 的设备行动、移动/射击），不是蓝图字段或面板勾选。
- 五步全部完成后显示 `PVE READY · free world unlocked`；右上摘要同步为 `TUTORIAL 5/5`。
- 内置策略模板 `builtin.idle/scout/sentry/relay` 让非程序玩家直接部署可用设备；
  蓝图 `ControllerProgramId` 显式选择，未知 id 回退文明程序。
- 正常启动从 Tick 0 开始；截图/冒烟/基准仍预跑到战斗。`--tutorial-smoke` 打印首启状态。
- 进度随游戏存档 v4 持久化（完成步骤 + 完成 Tick）；旧档无教程字段时按空进度处理（ADR-0040/0042）。

## 存档

- `F5` 保存到 `user://seedfield-save.json`，包含世界、蓝图注册表、在途消息、核心区最新产品、A/B 控制器版本与教程进度（游戏存档 v4）。
- `F9` 读取；失败保留当前世界并显示错误。
- `--save-smoke` 自动执行保存→变更→读取并打印哈希一致性。

## 约束

- Godot 节点、动画、Shader 不得修改 `WorldState` 或自行判定命中。
- 观察与意图仍走 `Observation / Intent / SimulationCore`。
- 画面只是原型绘制，不是正式美术验收。
- 导出模板尚未安装，暂不产出 Windows 包。
