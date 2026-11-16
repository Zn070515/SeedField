# Godot 客户端（待建立）

本目录预留给 Godot 4.7.2 .NET 客户端。当前本机**未安装 Godot**，因此没有伪造 `project.godot` 或无法验证的 `.csproj`。

## 建立步骤

1. 安装 Godot 4.7.2 .NET 版本（需支持 .NET 10.0；不支持时先提交 ADR，不得静默改框架）。
2. 在 Godot 中创建项目到 `game/SeedField.Game/`，渲染器选择与 2D 美术路线一致。
3. 添加对 `src/SeedField.Simulation/SeedField.Simulation.csproj` 的项目引用。
4. 只通过 `SimulationEvent` 回放移动、射击、命中、消息与核心区状态；Godot 节点不得持有或修改 `WorldState`。
5. 首次验收必须附真实运行截图和一条与 Headless 相同种子的状态哈希对照。

## 禁止

- 用概念图、线框或伪造场景冒充可运行画面。
- 在 Godot 层建立第二套仿真、命中或资源逻辑。
- 把 `SeedField.Simulation` 改成依赖 Godot 的工程。
