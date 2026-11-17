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
  <a href="https://github.com/Zn070515/SeedField/actions/workflows/ci.yml"><img src="https://github.com/Zn070515/SeedField/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/Zn070515/SeedField/actions/workflows/dependency-review.yml"><img src="https://github.com/Zn070515/SeedField/actions/workflows/dependency-review.yml/badge.svg" alt="Dependency review"></a>
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

<p align="center"><img src="game/SeedField.Game/screenshots/m0-combat.png" alt="Seed Field M0 combat slice" width="760"></p>

## 项目现状

| 项目 | 说明 |
|---|---|
| 当前里程碑 | **M0 纵切片**：Headless 可复现 + Godot 真实画面，均由同一 C# 仿真与 JS 控制器驱动 |
| 仿真核心 | 固定 Tick、意图归属/可见性校验、观察过滤、E/M、通信、直接交火、核心区保护与保底复建 |
| 程序闭环 | **游戏内编辑 A 方 JavaScript → 热部署 → 观察行为变化**；编译失败保留旧版本 |
| 成长/工坊 | 五分支最低可用判定、蓝图版本 → 部署实例（扣本地 M）、显式升级、核心区最新产品登记 |
| 多区域 | 显式区域链路、双向 BFS（扩展较小 frontier）、跨区延迟、链路带宽、存档往返 |
| 跨区物流 | 多跳链路运输 M，途中不可用，按路径总延迟到达；链路物资容量可限制 |
| 跨区能源 | 区域电网库存；实体可存入/抽取；跨区运输带延迟与链路能源容量 |
| 区域加载 | 引擎级活动区域：非活动区拒绝意图，被动系统继续运行；活动集不写入存档 |
| 设备级知识 | 每实体独立观察/控制器；只感知本设备传感器与目标区域消息；不再文明级融合 |
| 工程收口 | 统一部署/升级命令、存档 schema v2 + v1 迁移、摧毁库存损失审计 |
| PvE 目标 | 多控制点：独占驻留、争夺重置、占领数量达到阈值即胜负；HUD 与胜利截图 |
| 通信/对手 | 跨文明消息要求目标区 Comm 端点 + 目标文明策略允许；内置对手包可选并持久化 |
| 消息 ACL | 发送方/话题白名单、黑名单优先；引擎派生发送者；重复消息 ID 拒绝；断联不扣能量 |
| 条约/贸易 | 互不侵犯/贸易协定；宿主命令签约；互不侵犯阻止攻击；贸易托管、原子交换与退款；存档 v7 |
| 对手外交 | B 按画像自动响应 A 的提议/贸易单：Skirmisher 拒绝 NAP、要求贸易利润；Turtle 接受 NAP/公平贸易 |
| 建造/恢复 | `BuildTicks` 队列、工坊 UI、取消/失败退款；核心区恢复统一命令入口 |
| 区域加载 | 非活动区跳过发电并在激活/保存前补算；消息/物流/核心再生继续 |
| 客户端 | **Godot 4.7.2 Mono 可视切片 + 程序编辑器 + 工坊 + 外交面板（net10.0）** |
| 验证 | 136 个仿真测试 + 37 个 Jint 测试通过；600 Tick 双跑哈希一致；战斗/编辑器/工坊/外交/胜利截图已提交；CI 构建/测试/提交规范 |
| 技术栈 | .NET 10.0 / C# / JavaScript + Jint 4.18.0 / Godot 4.7.2 .NET |

## 工程结构

| 层 | 目录 | 职责 |
|---|---|---|
| 设计基线 | [`docs/design-bible/v1.9-rev2`](docs/design-bible/v1.9-rev2) | 产品、玩法、技术、路线图与决策登记 |
| 工程文档 | [`docs/engineering`](docs/engineering) | 实现基线、ADR、验收证据 |
| 仿真核心 | [`src/SeedField.Simulation`](src/SeedField.Simulation) | 唯一权威 `WorldState`、Tick、验证、事件、哈希 |
| 脚本运行时 | [`src/SeedField.Runtime.Js`](src/SeedField.Runtime.Js) | Jint 4.18.0；JSON 进/JSON 出，不暴露 CLR |
| Headless | [`src/SeedField.Headless`](src/SeedField.Headless) | 双文明纵切片 Runner |
| 客户端 | [`game/SeedField.Game`](game/SeedField.Game) | Godot 4.7.2 Mono 可视切片、程序编辑器与工坊；只渲染 `SimulationEvent` |

## 快速开始

```powershell
pwsh tools/setup-git-hooks.ps1
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --multi-region
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --logistics
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --energy
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --region-load
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --construction
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --construction-cancel
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --message-acl
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --diplomacy
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --diplomacy-ai

godot --path game/SeedField.Game                      # 实时；F1 程序编辑器，F2 工坊，F3 外交
godot --path game/SeedField.Game -- --capture         # 战斗截图
godot --path game/SeedField.Game -- --capture-editor  # 程序编辑器截图
godot --path game/SeedField.Game -- --capture-workshop # 工坊截图
godot --path game/SeedField.Game -- --capture-diplomacy # 外交面板截图
godot --path game/SeedField.Game -- --capture-diplomacy-ai # B 方外交响应截图
godot --path game/SeedField.Game -- --capture-victory  # 控制点占领截图
godot --path game/SeedField.Game -- --diplomacy-smoke  # 外交面板按钮接线冒烟
godot --path game/SeedField.Game -- --diplomacy-ai-smoke # B 方自动响应冒烟
godot --path game/SeedField.Game -- --opponent-turtle  # 使用 Turtle 内置对手
```

Headless Runner 运行 600 Tick 双文明对战并验证哈希；Godot 切片渲染同一套仿真事件。

## 文档入口

| 文档 | 内容 |
|---|---|
| [`IMPLEMENTATION_BASELINE.md`](docs/engineering/IMPLEMENTATION_BASELINE.md) | 已确认规则、真实代码状态、风险与下一步 |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | 分支、提交格式、验证门禁与 PR 流程 |
| [`ENGINEERING_STANDARDS.md`](docs/engineering/ENGINEERING_STANDARDS.md) | 权威来源、确定性、schema、依赖与发布标准 |
| [`DECISION_REGISTER.md`](docs/design-bible/v1.9-rev2/00-START-HERE/DECISION_REGISTER.md) | CONFIRMED / BASELINE / OPEN 决策主索引 |
| [`ADR-0001`](docs/engineering/adr/ADR-0001-dotnet10-runtime-target.md) | .NET 10.0 运行时目标 |
| [`evidence/`](docs/engineering/evidence/2026-11-16-m0-headless-slice.md) | Headless 测试、哈希与运行证据 |
| [`visual slice`](docs/engineering/evidence/2026-11-16-godot-visual-slice.md) | Godot 画面、截图与 net10.0 运行证据 |
| [`program deployment`](docs/engineering/evidence/2026-11-16-program-deployment.md) | 游戏内 JS 编辑、热部署与失败回退证据 |
| [`workshop & economy`](docs/engineering/evidence/2026-11-16-workshop-and-economy.md) | 蓝图/实例/升级、五分支、存档与 E/C/B 约束证据 |
| [`multi-region`](docs/engineering/evidence/2026-11-16-multi-region.md) | 区域链路、BFS 路由、跨区延迟与链路带宽证据 |
| [`material logistics`](docs/engineering/evidence/2026-11-16-material-logistics.md) | 跨区材料运输、在途与到达证据 |
| [`cross-region energy`](docs/engineering/evidence/2026-11-16-energy-grid.md) | 区域电网、抽取/存入、跨区运输证据 |
| [`region activation`](docs/engineering/evidence/2026-11-16-region-activation.md) | 活动区域、意图 gating 与被动系统连续性证据 |
| [`semantic review`](docs/engineering/reviews/2026-11-16-semantic-review.md) | 实现 vs 设计基线的语义审查与未闭合缺口 |
| [`core protection`](docs/engineering/evidence/2026-11-16-core-protection.md) | 外文明部署/行动拒绝与存档校验证据 |
| [`review closeout`](docs/engineering/evidence/2026-11-16-semantic-review-closeout.md) | 设备级知识、部署命令、存档 v2、资源损失收口证据 |
| [`control point`](docs/engineering/evidence/2026-11-16-control-point.md) | 控制点占领、争夺重置与胜利截图证据 |
| [`four-track batch`](docs/engineering/evidence/2026-11-17-four-track-batch.md) | 跨文明通信、内置对手、恢复命令、建造队列证据 |
| [`cancel & opponents`](docs/engineering/evidence/2026-11-17-cancel-and-opponents.md) | 取消退款、对手选择与协议边界证据 |
| [`persistence & queue UI`](docs/engineering/evidence/2026-11-17-persistence-and-queue-ui.md) | 对手元数据存档、建造队列 UI 证据 |
| [`policy/refund/offscreen`](docs/engineering/evidence/2026-11-17-policy-refund-offscreen.md) | 消息权限、失败退款、离屏发电预算证据 |
| [`message ACL`](docs/engineering/evidence/2026-11-17-message-acl.md) | 发送方/话题白名单、身份边界、重复消息与断联证据 |
| [`treaty & trade`](docs/engineering/evidence/2026-11-17-treaty-trade.md) | 互不侵犯、贸易托管/原子交换/退款、存档 v7 证据 |
| [`diplomacy panel`](docs/engineering/evidence/2026-11-17-diplomacy-panel.md) | F3 外交面板、按钮接线、截图与 smoke 证据 |
| [`opponent diplomacy`](docs/engineering/evidence/2026-11-17-opponent-diplomacy.md) | B 方画像化自动响应、Headless/Godot 证据与截图 |

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

Godot 画面仍是原型绘制；区域活动只是意图 gating（ADR-0004）；文明级 legacy 观察 API 仍保留给工具；核心区恢复配额未统一；跨文明协议已有消息 ACL、条约/贸易原型、F3 外交面板与 B 方自动响应，但 B 不会主动谈判/下单，也没有消息签名/信誉/关税；PvE 对手、离线文明包、TypeScript `.d.ts`、导出模板与平衡数值均未实现。详细风险见[实现基线](docs/engineering/IMPLEMENTATION_BASELINE.md)。
