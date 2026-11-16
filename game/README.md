# Godot 客户端

`game/SeedField.Game` 是 M0 可视切片：**Godot 4.7.2 Mono + .NET 10**，引用 `SeedField.Simulation` 与 `SeedField.Runtime.Js`。C# 仿真仍是唯一权威，Godot 只渲染 `SimulationEvent`；A 方 JavaScript 可在游戏内编辑并热部署。

## 运行

```powershell
dotnet build game/SeedField.Game/SeedField.Game.csproj
godot --path game/SeedField.Game                       # 实时运行
godot --path game/SeedField.Game -- --capture          # 战斗截图
godot --path game/SeedField.Game -- --capture-editor   # 编辑器截图
```

截图输出：`screenshots/m0-combat.png`、`screenshots/m0-program-editor.png`。

## 结构

| 路径 | 内容 |
|---|---|
| `Scenes/Main.tscn` | 单场景入口 |
| `Scripts/Main.cs` | 场景生命周期、仿真步进、事件 HUD、截图 |
| `Scripts/ProgramEditor.cs` | A 方 JavaScript 编辑器与部署按钮 |
| `Scripts/Scenarios.cs` | M0 世界与两个 JS 控制器脚本 |
| `screenshots/` | 真实运行截图 |

## 操作

`SPACE` 暂停 / 继续 · `N` 单 Tick · `R` 重置 · `F1` 程序编辑器 · `Ctrl+Enter` 部署 · `F12` 截图。

## 程序部署

- 编辑器打开时默认暂停；关闭后继续。
- 部署会新建 Jint 控制器并原子替换当前版本，版本号 +1。
- 编译/加载失败时保留旧控制器与旧版本，错误显示在编辑器状态栏。
- Godot 只提交脚本文本；控制器仍在 `SeedField.Runtime.Js` 中执行。

## 约束

- Godot 节点、动画、Shader 不得修改 `WorldState` 或自行判定命中。
- 观察与意图仍走 `Observation / Intent / SimulationCore`。
- 画面只是原型绘制，不是正式美术验收。
- 导出模板尚未安装，暂不产出 Windows 包。
