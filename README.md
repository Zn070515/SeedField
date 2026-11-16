<div align="center">

<h1>Seed Field · 种子域</h1>

<p><strong>可编程二维数字文明沙盒</strong> · <strong>A programmable 2D civilization sandbox</strong></p>

<p>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/C%23-SimulationCore-239120?logo=csharp&logoColor=white" alt="C#">
  <img src="https://img.shields.io/badge/Godot-4.7.2%20.NET-478CBF?logo=godotengine&logoColor=white" alt="Godot 4.7.2 .NET">
  <img src="https://img.shields.io/badge/JavaScript-Jint%204.18.0-F7DF1E?logo=javascript&logoColor=black" alt="Jint 4.18.0">
  <img src="https://img.shields.io/badge/Platform-Windows%20Steam-0078D4" alt="Windows Steam">
</p>

<p>
  <a href="#项目现状">项目现状</a> ·
  <a href="#工程结构">工程结构</a> ·
  <a href="#快速开始">快速开始</a> ·
  <a href="#文档入口">文档入口</a> ·
  <a href="#工程原则">工程原则</a>
</p>

</div>

玩家在中央主控崩溃已久的二维数字世界中，从不可摧毁的种子核心区出发，通过元件构造、JavaScript 编程、能源与物资网络和自主设备，建立可持续运行的数字文明，并与其他玩家留下的**离线文明版本**在同一套规则下探索、合作与竞争。

完整游戏离线可玩，不依赖开发者服务器；设计基线为 V1.9 Rev2。本仓库是正式工程，不是设计资料包。

## 项目现状

| 项目 | 说明 |
|---|---|
| 当前里程碑 | **M0 Headless 纵切片**：真实 JS 驱动单位、同规则仿真、可复现哈希 |
| 仿真核心 | 固定 Tick、意图验证、观察过滤、E/M、通信、直接交火、核心区保底复建 |
| 客户端 | **Godot 4.7.2 .NET 未安装，画面验收阻塞** |
| 验证 | 19 个仿真测试 + 7 个 Jint 测试通过；600 Tick 双跑哈希一致 |
| 技术栈 | .NET 10.0 / C# / JavaScript + Jint 4.18.0 / Godot 4.7.2 .NET |

## 工程结构

| 层 | 目录 | 职责 |
|---|---|---|
| 设计基线 | [`docs/design-bible/v1.9-rev2`](docs/design-bible/v1.9-rev2) | 产品、玩法、技术、路线图与决策登记 |
| 工程文档 | [`docs/engineering`](docs/engineering) | 实现基线、ADR、验收证据 |
| 仿真核心 | [`src/SeedField.Simulation`](src/SeedField.Simulation) | 唯一权威 `WorldState`、Tick、验证、事件、哈希 |
| 脚本运行时 | [`src/SeedField.Runtime.Js`](src/SeedField.Runtime.Js) | Jint 4.18.0；JSON 进/JSON 出，不暴露 CLR |
| Headless | [`src/SeedField.Headless`](src/SeedField.Headless) | 双文明纵切片 Runner |
| 客户端 | [`game`](game) | Godot 工程（待安装编辑器后建立） |

## 快速开始

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj
```

Runner 运行 600 Tick 双文明对战，输出事件统计、拒绝原因与最终状态哈希，并跑两次验证可复现性。

## 文档入口

| 文档 | 内容 |
|---|---|
| [`IMPLEMENTATION_BASELINE.md`](docs/engineering/IMPLEMENTATION_BASELINE.md) | 已确认规则、真实代码状态、风险与下一步 |
| [`DECISION_REGISTER.md`](docs/design-bible/v1.9-rev2/00-START-HERE/DECISION_REGISTER.md) | CONFIRMED / BASELINE / OPEN 决策主索引 |
| [`ADR-0001`](docs/engineering/adr/ADR-0001-dotnet10-runtime-target.md) | .NET 10.0 运行时目标 |
| [`evidence/`](docs/engineering/evidence/2026-11-16-m0-headless-slice.md) | 本轮命令、测试与运行证据 |

## 工程原则

| 规则 | 要求 |
|---|---|
| 单一真实状态源 | 只有 `SimulationCore` 修改世界；UI、Shader、脚本只能读观察、提交意图 |
| 固定 Tick 可复现 | 整数坐标、稳定 ID/事件顺序；不以 FPS 或真实 CPU 毫秒结算 |
| 离线优先 | 无账号、匹配、遥测、云推理或自建游戏服务器 |
| 核心区不可毁 | 外部全毁后必须能在有限步骤内复建最低实体 |
| 不可信脚本 | Jint 限制不是 OS 沙箱；未完成进程隔离前不开放第三方包执行 |
| 证据导向 | 不把未执行的测试、未验证的画面或演示数值写成已确认结论 |

## 当前限制

Godot 4.7.2 .NET 尚未安装，客户端未开始；多区域、存档、离线文明包、TypeScript `.d.ts`、正式美术与平衡数值均未实现。详细风险见[实现基线](docs/engineering/IMPLEMENTATION_BASELINE.md)。
