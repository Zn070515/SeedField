# IMPLEMENTATION BASELINE — Seed Field M0

> 日期：2026-11-16  
> 依据：`docs/design-bible/v1.9-rev2/` 与用户当日指令  
> 状态：第一批 Headless 纵切片已可运行；Godot 客户端受环境阻塞

## 1. 对最终形态的理解

《种子域》是 Windows 优先、Steam 付费、完整离线的精致二维系统沙盒。玩家从不可摧毁的种子核心区出发，构造能源、感知、通信、状态、执行五分支设备，用真实 JavaScript 编写自主控制器，建立 E/M/C/S/B 约束下的网络、工业与军事体系，并与其他玩家留下的离线文明版本在同一套规则下竞争。

它不是联网 MMO、不是生物进化模拟、不是纯脚本 IDE、不是英雄微操 RTS。仿真核心是唯一真实状态源；表现层只回放事件。

## 2. 已确认 vs 未决

### 已实现所依赖的已确认规则

| ID | 规则 | 本轮对应 |
|---|---|---|
| T-01 | Godot 4.7.2 .NET + C# 仿真 | C# 核心已独立于 Godot 构建 |
| T-02 | JavaScript + Jint 4.18.0 | 已接入并测试 |
| G-06/G-07 | 自主程序行动；暂停编辑 | 控制器通过 Intent 行动 |
| E-02 | E 网络供给、M 简化物流 | 本轮仅本地 E 库存与消耗 |
| R-01/R-02/R-03 | 核心区不可毁、保底 E/M、外部可损失 | 已实现保底再生与最小复建测试 |
| P-01/P-02 | 离线单机、无开发者服务器 | 无任何网络依赖 |
| C-01 | 基础科技通过探索/研究（后续） | 未实现 |

### 仍未决、未擅自冻结

- 其他文明核心区是否同样不可毁。
- 核心区再生速率、储量、吞吐与防套利规则。
- 跨区通道类型、物流成本与时序。
- C 算力计费与超预算结果。
- 离线文明包的长期战略自治范围。
- 多种终局的具体条件。
- 正式 Tick 频率（本轮使用整数 Tick，未绑定 20 Hz）。

## 3. 现有工程真实状态

### 已实现并可运行

```text
src/SeedField.Simulation/     C# 权威仿真核心
src/SeedField.Runtime.Js/     Jint 4.18.0 适配
src/SeedField.Headless/       Headless 纵切片 Runner
game/SeedField.Game/          Godot 4.7.2 Mono 可视切片（net10.0）
tests/SeedField.Simulation.Tests/
tests/SeedField.Runtime.Js.Tests/
```

已实现的具体能力：

- 整数二维坐标（毫米）、区域边界、稳定 `EntityId`。
- 固定 Tick；实体按 ID、消息按 `DeliverTick + Id` 的稳定顺序处理。
- `WorldState → Observation → Controller → Intent → Validation → Events` 管线。
- 传感器过滤：仅己方实体与己方传感器范围内的敌方实体进入观察。
- 移动、直接命中武器、冷却、能量消耗与拒绝错误码。
- 消息队列：最短延迟 1 Tick，发送 Tick 不可读；当前仅允许同文明路由。
- 核心区保底 E/M 再生（带上限）与无外部工业条件下的最小实体复建。
- FNV-1a 64 状态哈希；5,000 Tick 双跑一致。
- Jint 沙箱：语句数、内存、超时限制；禁止 `eval`/`Function` 字符串编译；不暴露 CLR 宿主对象；观察与意图仅以 JSON 文本交换。
- Godot 4.7.2 Mono 可视切片：渲染实体、射程环、射击轨迹、消息脉冲与事件日志；两个单位由真实 Jint 脚本驱动，Godot 不承担任何战斗结算。
- 控制器热部署：`ControllerSlot` 原子替换控制器并递增版本；加载失败保留旧版本；Godot 内 `F1` 编辑器 + `Ctrl+Enter` 部署。
- 版本化存档：`WorldSaveSerializer` 保存世界；`GameSaveSerializer` 追加控制器脚本/版本；加载后状态哈希一致。
- 蓝图/实例：不可变 `DeviceBlueprint` 版本、`DeploymentService.Deploy/Upgrade`、五分支覆盖判定；Godot `F2` 工坊面板。
- 核心区最新产品：每个分支登记一个 `BlueprintId + Version`，可从核心区直接恢复。
- E/C/B 约束：`PowerModule` 本地发电、`StateModule` 每 Tick 意图预算、`CommModule.BandwidthPerTick` 发信上限。
- 多区域通信：显式 `RegionLink`、双向 BFS 路由、跨区延迟、链路带宽与目标区域校验。
- 跨区物流：`TransferMaterialIntent` 从本地库存发货，经多跳链路路径的总延迟到达目标区域库存；链路物资容量可限制，在途物资不可用。
- 跨区能源：区域电网库存；`SendEnergyIntent` 存入本地/远端电网，`DrawEnergyIntent` 抽取到实体本地存储；远端运输有延迟与链路能源容量。
- 区域活动：引擎级 active/inactive gating；非活动区拒绝意图并跳过发电，激活/保存前补算；消息/物流/核心再生继续。
- 意图归属与可见性：Jint 控制器写入 `IssuerCivilizationId`；跨文明实体控制与未感知目标攻击被拒绝，且不通过错误码泄漏隐藏 ID。
- 核心区保护：外文明无法部署进核心区、无法在核心区行动；存档加载拒绝核心区内的外文明实体。
- 设备级知识：`BuildForEntity` + `DeviceControllerSlot`；每实体独立观察/控制器，消息/运输按设备区域过滤。
- 统一部署命令：`DeployCommand`/`UpgradeCommand` 经 `ExecuteDeploymentCommand`，产出部署/升级事件；工坊 UI 不再直接调用低层服务。
- 存档 schema v2：接受 v1 并迁移；世界与游戏存档均有迁移测试。
- 摧毁库存审计：`EntityInventoryLost` 与 `EnergyLostTotal`/`MaterialLostTotal` 进入事件、统计、存档与哈希。
- 控制点目标：多控制点、独占驻留计时、争夺/离开重置、占领数量阈值胜负；Godot HUD 与 `--capture-victory`。
- 跨文明通信：目标文明必须在目标区域有 Comm 端点，且目标消息策略允许；消息进入目标文明收件箱。
- 内置离线对手：受信任包清单校验（schema/路径/大小/入口），Skirmisher/Turtle；Godot `--opponent-turtle` 可选；选择写入游戏存档并随读档恢复；第三方导入仍关闭。
- 建造队列：`BuildTicks` 蓝图先扣 M，到期由引擎生成实体；订单进入存档/哈希。
- 建造取消：`CancelConstructionCommand` 退款并产生 `ConstructionCancelled`；退款统计进入存档/哈希；工坊队列 UI 可选中取消。
- 建造失败：蓝图缺失/到期校验失败退还 `MaterialPaid`，事件携带退款额。
- 消息策略：每文明可拒绝发送者/话题或关闭入站；`MessageDenied`；策略进入存档 schema v5。
- 无状态消息 ACL：发送方/话题白名单、黑名单优先、同文明绕过；发送者由引擎从实体派生；重复消息 ID 读档拒绝；断联发送在扣能量前返回 `NoRoute`，已提交消息仍按 `DeliverTick` 投递；策略进入存档 schema v6 与哈希（ADR-0006）。
- 条约/贸易：互不侵犯/贸易协定；宿主 `ExecuteDiplomacyCommand` 独占签约与接单；互不侵犯在扣能量前阻止攻击；贸易单在指定区域托管 E/M、接受时原子交换、拒绝/取消/到期退款；撕毁贸易协定级联退款；状态进入存档 schema v7、哈希与观察（ADR-0007）。
- 外交面板：Godot `F3` 面板列出条约/提议/贸易单，A 方按钮经宿主命令签约、接受/拒绝/撕毁、创建/取消贸易单；面板与 F1/F2 互斥；`--capture-diplomacy` 与 `--diplomacy-smoke`。
- 对手外交响应：`OpponentDiplomacyPolicy` 纯函数按画像响应 B 的 incoming 提议/贸易单；Skirmisher 拒绝 NAP、要求 120% 贸易收益，Turtle 接受 NAP 与公平贸易；无通信/库存不足时拒绝或不响应；事件进入日志（ADR-0008）。
- 主动提议与多轮谈判：B 按 Tick 节奏主动提 NAP/Trade/贸易单；`CounterTradeCommand` 支持最多 3 轮反提议，`NegotiationId`/`Round` 进入观察、哈希与存档 v8；Skirmisher 每轮让步 10%；面板显示 `R{round}/3` 与 COUNTER（ADR-0009）。
- 自动电力潮流：`RegionLink.PowerPerTick` 定义自动潮流通量；按文明/连通分量把电网库存从盈余区经确定性 BFS 移到缺口区，受共享链路容量限制；非活动区也参与；`PowerFlowed`/`PowerShortfall`、停电诊断、F4 面板、存档 v9（ADR-0010）。
- 电力恢复与网络细化：本地满电后的发电溢出入网；设备移动/攻击/发信自动从电网取电，受每文明 `ReserveEnergy` 恢复储备约束；`PowerLatencyTicks`/`PowerLossPercent` 支持在途电力与损耗；诊断显示储备/负载抑制/在途；存档 v10（ADR-0011）。
- 电网容量与断路器：每区域/文明 `GridCapacity` 限制入网与到账，溢出/到账超限浪费；`PowerModule.Priority` + 策略阈值实现优先级取电；损耗按净额反推毛额；区域/文明持久 `BreakerOpen`/`ManualHold`，电网归零跳闸、储备恢复自动合闸或宿主手动合闸；F4 面板可开关断路器；存档 v11（ADR-0012）。
- 抢占式优先级与断路器维护：同一 Tick 按 `PowerModule.Priority` 降序处理意图，结果仍按输入顺序返回；`TripCount`/`MaintenanceRequired` 持久化，达到 `BreakerTripLimit` 后锁定所有合闸路径；`MaintainBreakerCommand` 扣材料并清除锁定；F4 显示 trips/MAINT 并提供 MAINTAIN；存档 v12（ADR-0013）。
- 确定性随机与随机故障：`WorldState.Random` 是 SplitMix64 流，种子/状态进入存档 v13 与哈希；`Percent(0)` 不消耗随机值；`BreakerFaultChancePercent` 命中后直接锁定断路器，`BreakerLockedOut.Reason` 区分 `fault`/`trips`；`--power-fault` 演示（ADR-0014）。
- 工程约束 Lint：`tests/SeedField.Repository.Tests` 校验 `.cs`/`.md` 600 行上限、`net10.0`、仿真核心无 Godot、ADR 命名、设计基线 MANIFEST 完整性。
- 核心区恢复配额：`SeedCoreConfig` 的窗口/配额/成本增长/债务衰减/透支字段；两个恢复入口共用配额与缩放成本；`RecoveryQuotaDenied`；慢速模式；存档 v14（ADR-0015）。
- 核心区外壳与围困：`IsCoreAnchor` 蓝图/实体标记；`AnchorTarget` 历史覆盖目标；缺口与直接链路敌对存在提高恢复成本、降低有效配额；保底再生不变；存档 v15（ADR-0016）。
- 设备残骸：`WreckRecoveryPercent` 场景规则；摧毁蓝图实例留下按成本比例计算的残骸；原主可回收，其他文明需在区域有实体；`MaterialSalvagedTotal`；存档 v16（ADR-0017）。
- 离线废墟快照：数据-only JSON、1MB/2000 条限额、非核心区导入、RNG 切片、老化/掠夺后生成残骸；未知蓝图只变残骸；`--ruins`（ADR-0018）。
- 恢复面板与核心维护：`CoreRecoveryDiagnosticsBuilder`；F6 面板显示配额/债务/锚点/围困并执行 RECOVER/MAINTAIN；`MaintainCoreCommand` 扣材料清配额/债务；存档 v17（ADR-0019）。
- 离线包安全门禁：第三方面向可执行包仍关闭；`ThirdPartyImportEnabled=false`；仓库 Lint 禁止对手包命名空间出现文件/网络/进程/程序集 API；威胁模型、目标工作进程/IPC/Job Object 架构与 12 项开放门禁见 ADR-0020。
- 离线包隔离 Phase 1：`SeedField.Worker` 独立进程、长度前缀 IPC、Windows Job Object（128 MiB/单进程/kill-on-close）、控制器工厂、Headless B 方 worker 双跑哈希一致；第三方导入仍关闭（ADR-0021）。
- 离线包信任 Phase 2a：`SeedField.PackageHost` 本地目录加载、ECDSA P-256 签名/内容哈希/信任库/显式同意、Job CPU 时间、磁盘看门狗、IPC fuzz、`--package-check`；AppContainer/OS 网络文件拒绝未做，导入仍关闭（ADR-0022）。
- 核心区恢复命令：`BootstrapCoreCommand`/`RestoreCoreProductCommand` 经 `ExecuteRecoveryCommand`。
- 确定性战斗随机：`CombatHitChancePercent`/`CombatDamageVariancePercent`/`CombatLootChancePercent`/`CombatLootMaterial` 场景配置；固定抽取顺序命中→伤害→掉落，0%/100% 不消耗随机值；`ShotMissed`/`CombatLootGranted` 事件与 `ShotsMissed`/`MaterialLootedTotal` 统计；存档 v18；`--combat-rng` 演示（ADR-0029）。
- 并行控制器批处理：`ControllerBatchRunner` 按输入索引并行执行独立 `JsController`，单控制器故障隔离；`BuildControllerObservations` 每 worker 独立观测缓存；Main/Headless 接入；默认 DOF = 逻辑核数；Headless Server GC；`Engine.PrepareScript` 跨引擎复用；进程内 `Decide` 用 `JsObject.Create` + 共享布局直接构造观测对象，worker/IPC 用完整对象 JSON（跨版本兼容），紧凑格式保留给带宽敏感路径/测试；100 台 33.96→1.18 ms/tick、1000 台 180.36→7.9 ms/tick（3000 Tick 稳态），1000 台启动 205→127 ms；`--benchmark-jint-parallel`、`--benchmark-jint-preserialized`、`--benchmark-startup`、`--ticks`、`--chunk-size` 与 `--budget-ms` 门禁（ADR-0028）。

### 尚未实现

- TypeScript `.d.ts` 编辑契约。
- 离屏区域性能优化（当前 active 仅做意图 gating）、混合资源反提议与完整议价记忆、维护时长、多电压/潮流方程。
- 存档迁移（v1 → 后续）、回放、完整离线文明包（可执行 AI/脚本）。
- 废墟实体/占领/清理、快照签名/来源认证、Workshop 流程、残骸老化/掠夺。
- 离线包隔离剩余门禁：AppContainer/低完整性、OS 网络/文件系统拒绝、只读挂载、OS 磁盘配额、完整 fuzz/红队、发布签名密钥/Workshop、独立安全审查；第三方 JS 导入仍关闭。
- 正式平衡数值、掉落表/经济上限、暴击/穿甲等扩展。
- PvE 对手、教程/科技树/终局与正式美术。

## 4. 已实测的 API 与版本

| 项 | 实测结果 |
|---|---|
| .NET SDK | 10.0.404 |
| .NET Runtime | 10.0.15 |
| NuGet Jint | 4.18.0 可还原，net10.0 资产可用 |
| `Options.TimeoutInterval` / `MaxStatements` / `LimitMemory` | 可用 |
| `Options.Host.StringCompilationAllowed` | 可用；`eval` 被拒绝 |
| `Engine.PrepareScript` / `Prepared<Script>` | 4.18.0 可用；可跨引擎共享，按源码缓存 |
| Server GC | Headless csproj 生效；Godot 宿主需 `DOTNET_gcServer=1`（`GCSettings.IsServerGC` 已验证） |
| Godot 4.7.2 Mono | 已安装 `D:\Dev\Godot\4.7.2-mono`；`dotnet build` + headless 运行 net10.0 冒烟通过（`NET10_SMOKE_OK`） |

## 5. 首个可运行切片

`src/SeedField.Headless`：

- 区域 `frontier` 20,000 × 20,000 mm。
- 文明 A：侦察实体 1（传感器 + 驱动 + 通信）、火力实体 2（传感器 + 武器 + 驱动 + 通信）。
- 文明 B：火力实体 3（同规则）。
- 两个真实 JS 控制器：A 侦察发现目标后发送坐标消息并推进，火力按本地感知或最近消息目标移动/开火；B 按本地感知接近并开火。
- 600 Tick，两次完整运行比较最终状态哈希。

实测输出（2026-11-16）：

```text
ticks: 600
final: tick=600 entities=1 energySpent=423 shots=52 damage=414 destroyed=2 messagesDelivered=39
controller faults: 0
intent rejection reasons:
  UnknownEntity: 1
  WeaponCoolingDown: 50
Run 1 final hash: 813340ffafaffbb0
Run 2 final hash: 813340ffafaffbb0
REPRODUCIBLE: identical state hash across both runs.
```

`WeaponCoolingDown: 50` 是脚本每 Tick 尝试开火、仿真核按冷却规则拒绝的预期结果，不是缺陷；它证明拒绝原因可诊断。

### 5.1 Godot 可视切片

`game/SeedField.Game` 使用同一 `WorldState`、同一 `SimulationEngine` 和同一 Jint 控制器脚本，仅把 `SimulationEvent` 渲染成画面：

```powershell
dotnet build game/SeedField.Game/SeedField.Game.csproj
godot --path game/SeedField.Game -- --capture
```

实测（2026-11-16）：

```text
SEEDFIELD_PRERUN tick=41 shots=3 entities=3
SCREENSHOT_SAVED .../screenshots/m0-combat.png (Ok)
```

截图见 `game/SeedField.Game/screenshots/m0-combat.png` 与 `docs/engineering/evidence/2026-11-16-godot-visual-slice.md`。该画面证明“真实 JS 驱动 + Godot 事件回放”链路；它仍是原型绘制，不是正式美术验收。

### 5.2 游戏内程序编辑与热部署

Godot 内按 `F1` 打开 A 方 JavaScript 编辑器，修改后 `Ctrl+Enter` 部署：

- 编辑时暂停；关闭编辑器后继续。
- 部署新建 Jint 控制器并原子替换，版本号递增。
- 加载失败保留旧控制器与旧版本，错误显示在状态栏。
- `--capture-editor` 实测：编辑射程阈值 `3500 → 4200`，部署为 v2，推进到 Tick 53，截图 `screenshots/m0-program-editor.png`。
- `ControllerSlotTests` 覆盖成功替换、失败回退、脚本保留三类行为。

### 5.3 工坊、五分支与资源约束

- 场景现在由蓝图部署生成实例：A 侧 Scout Mk1、Strike Frame，B 侧 Strike Frame。
- 工坊面板列出每个设计的最新版本、五分支覆盖与 A 侧实例；部署产生新实例，升级必须显式执行。
- 实测 `--capture-workshop`：部署 `power v1`，将 Scout 从 v1 升级到 v2，状态栏显示实际结果；截图 `screenshots/m0-workshop.png`。
- 引擎新增：`PowerModule` 逐 Tick 发电、`StateModule` 限制每 Tick 意图数、`CommModule.BandwidthPerTick` 限制发信数。
- 测试覆盖：蓝图发布不改旧实例、显式升级、五分支覆盖、核心区最新产品恢复、发电上限、算力预算、带宽上限。

### 5.4 存档与控制器版本

- `WorldSaveSerializer` 保存区域、实体、蓝图注册表、核心区最新产品、在途消息、收件箱、统计与下一 ID。
- `GameSaveSerializer` 在外层追加每个文明的控制器脚本与版本；读取时重建 `ControllerSlot`。
- Godot `F5`/`F9` 使用 `user://seedfield-save.json`；`--save-smoke` 自动验证保存→变更→读取的哈希一致性。
- 实测：`SAVE_SMOKE ok=True before=0c2b29... mutated=9b3aab... after=0c2b29...`（Skirmisher）；Turtle `1964d5...`。

### 5.5 多区域与跨区消息

- `RegionLink` 是显式双向链路，带 `LatencyTicks` 与可选的每 Tick 消息上限。
- 跨区消息要求：目标区域存在、目标区域有己方 Comm 端点、存在链路路径。
- 路由为双向 BFS：每层扩展较小 frontier，同规模时正向优先；命中后反转正向半段并拼接反向半段。
- 总延迟 = Comm 延迟 + 路径链路延迟之和。
- 链路容量在发送验证阶段检查，成功后才计入本 Tick 用量。
- Headless 实测：`r0 → r1(2t) → r2(3t)` + Comm 1t → `deliverTick=6`，目标区域与投递 Tick 均正确。

### 5.6 跨区材料物流

- `TransferMaterialIntent`：需要运输单位有 `Drive`、源区域库存足够、存在链路路径。
- 路由复用双向 BFS；`ArriveTick = 发送 Tick + 路径链路延迟之和`，在途物资不可用于目标区制造。
- `RegionLink.MaterialPerTick` 可限制每 Tick 过境批次；容量在扣库存前统一校验。
- 到达时写入目标区域该文明库存，产生 `MaterialArrived`；Route、在途状态全程进入哈希与存档。
- Headless 实测：`r0→r1(2t)→r2(3t)`，100 M 库存发 40 M，`arriveTick=5 route=l01>l12`；到达后目标 40。

### 5.7 跨区能源

- 区域库存新增 Energy 池；实体需要 `PowerModule` 才能存取。
- `SendEnergyIntent`：目标省略/同区 → 立即存入本地电网；远端 → 经链路路径运输，`ArriveTick = Tick + 路径延迟和`。
- `DrawEnergyIntent`：从本区电网抽取到实体本地存储，受本地容量上限约束。
- `RegionLink.EnergyPerTick` 可限制每 Tick 过境批次。
- Headless 实测：40 E 从 r0 发往 r1，`arriveTick=3 route=l01`；到达后抽取 25 E，目标实体 25、目标电网 15、源实体 60。

### 5.8 区域活动与加载

- `SimulationEngine.DeactivateRegion/ActivateRegion/SetActiveRegions` 控制会话级活动区。
- 非活动区：控制器意图被拒绝（`RegionInactive`），避免离屏单位被脚本操作。
- 被动系统继续运行：发电、消息/物资/能源投递、核心再生；因此有/无活动标记的被动状态哈希一致。
- 活动集只属于会话，不写入存档，也不进入世界哈希。
- Headless 实测：非活动区移动返回 `RegionInactive`；激活后同一意图成功到 `(500,500)`。

### 5.9 核心区保护

- `CoreProtection.IsProtectedFrom`：核心区只允许拥有该区核心的文明。
- `DeploymentService` 拒绝外文明部署进核心区（`CORE_PROTECTED`）。
- `SimulationEngine` 拒绝外文明实体在核心区内的任何意图（`CoreProtected`）。
- 存档加载拒绝核心区内的外文明实体（`FOREIGN_ENTITY_IN_CORE`）。
- 仍开放：锚点占领/仓储/产出加成、多跳围困、残骸回收与正式费率。

### 5.10 控制点目标

- 多个控制点，每个可配置区域/位置/半径/驻留 Tick；同一 Tick 只有一个文明在圈内才累计，争夺或离开清零。
- 达到驻留时长后产生 `ControlPointCaptured` 并记录 owner；达到 `MatchRules.CapturedPointsToWin` 后产生 `MatchEnded`。
- 控制点/胜负状态进入观察、存档（schema v3）与哈希；设备只看到同区域控制点。
- Godot HUD 绘制每个点与比分；`--capture-victory` 跑到比赛结束。
- Godot 实测：A 在 Tick 51 占 2 点（west/center）获胜；`m0-victory.png`。

### 5.11 无状态消息 ACL 与身份边界

- 策略新增发送方/话题白名单；判定顺序为黑名单 > 白名单 > 默认开关，同文明消息绕过。
- 消息信封发送文明由引擎从实体派生；脚本无法自带发送者字段，伪造 issuer 按 `UnknownEntity` 拒绝。
- 消息 ID 在在途与所有收件箱中必须唯一；读档重复返回 `DUPLICATE_MESSAGE_ID`。
- 无链路发送在扣能量前返回 `NoRoute`；已提交消息在链路删除后仍按 `DeliverTick` 投递。
- 存档 schema v6；v5 策略按 `AllowAllTopics=true`、空允许列表迁移。
- Headless 实测：
  `MESSAGE_ACL senderAllowed=True senderDenied=True topicAllowed=True topicDenied=True noRoute=True energy=99 inboxB=1 inboxC=0 hashOk=True ok=True`
- 测试：113 仿真 + 24 Jint；600 Tick 双跑哈希 `4ca65e0d83768a38` 一致。

### 5.12 条约与贸易协议

- 两种条约：`NonAggression`、`Trade`；提议/响应要求通信路径，撕毁为单边行为。
- 互不侵犯在可见性之后、冷却/能量之前阻止攻击；`TreatyDenied`。
- 贸易单指定单区域，提案方给出物托管；接受原子交换，不足不改变库存；拒绝/取消/到期退款；撕毁贸易协定级联退款。
- 权限：宿主 `ExecuteDiplomacyCommand`；脚本只读观察。
- 存档 schema v7；观察新增 `treaties`/`treatyProposals`/`tradeOffers`，设备观察按区域过滤贸易单。
- Headless 实测：
  `DIPLOMACY napBlocked=True napBreakAttacked=True tradeSwapped=True refundOk=True hashOk=True ok=True`
- 测试：136 仿真 + 25 Jint；600 Tick 双跑哈希 `6c8134c8f3981da1` 一致。

### 5.13 Godot 外交面板

- `F3` 面板：条约操作、生效条约、待处理提议、贸易单、创建贸易单与状态行。
- 面板只操作 A；所有按钮经 `ExecuteDiplomacyCommand`，不直接改世界。
- B 的待处理提议/贸易单由场景/宿主预置；B 自动外交未实现。
- 截图：`game/SeedField.Game/screenshots/m0-diplomacy.png`。
- 实测：
  `DIPLOMACY_SMOKE treaties=2 offers=0 A=M380/E210 B=M410/E190 settled=M70/E30 ok=True`
- `--diplomacy-smoke` 覆盖接受 NAP、接受贸易单、取消贸易单三条面板接线。

### 5.14 对手外交响应

- `OpponentDiplomacyPolicy.Decide` 纯函数，按画像响应 B 的 incoming 提议/贸易单；不修改世界、无隐藏状态。
- 画像：Skirmisher 拒绝 NAP、接受 Trade 条约、贸易需 ≥120% 收益；Turtle 接受 NAP/Trade、公平贸易 ≥100%；未知包默认 Cautious。
- Godot `StepOnce` 在仿真 Step 后执行策略，事件进入日志；`--diplomacy-ai-smoke` 与 `--capture-diplomacy-ai` 为证据。
- Headless 实测：
  `DIPLOMACY_AI turtleNap=True aggressiveNapRejected=True fairAccepted=True unfairRejected=True ok=True`
- Godot 实测：
  `DIPLOMACY_AI_SMOKE opponent=builtin.skirmisher ... napAccepted=False expectNapAccepted=False napHandled=True settledM=50 ok=True`
  `DIPLOMACY_AI_SMOKE opponent=builtin.turtle ... napAccepted=True expectNapAccepted=True napHandled=True settledM=50 ok=True`
- 测试：136 仿真 + 37 Jint；截图 `m0-diplomacy-ai.png`。

### 5.15 主动提议与多轮反提议

- B 按 `Tick % 240 == 0` 主动提 NAP/Trade/贸易单；有响应命令时跳过主动动作。
- `CounterTradeCommand`：目标方可反提议；退款原单、托管新给出物、`Round + 1`；最多 3 轮，超出返回 `NegotiationExhausted`。
- B 反提议只处理单资源报价；Skirmisher 阈值 120% 起每轮 -10%，Turtle 100% 不让步。
- 面板显示 `R{round}/3` 与 `COUNTER SELECTED`。
- 存档 schema v8；观察/Jint 暴露 `negotiationId`、`round`。
- Headless 实测：
  `DIPLOMACY_NEGOTIATION proactiveNap=True napActive=True proactiveTrade=True tradeActive=True proactiveOffer=True rounds=2 counterAccepted=True ok=True`
- Godot 实测：
  `DIPLOMACY_COUNTER_SMOKE round=2 settledM=42 ok=True`
- 测试：143 仿真 + 48 Jint；截图 `m0-diplomacy.png`（R1/3 + COUNTER）与 `m0-diplomacy-ai.png`（B 反提议 R2/3）。

### 5.16 自动电力潮流与断供诊断

- `PowerPerTick` 自动潮流通量与显式 `EnergyPerTick` 运输批次数分离。
- 每 Tick 在发电后、意图前按文明/连通分量均衡电网库存；共享链路容量；非活动区参与。
- 诊断：发电潜力、电网库存、本地储能、连通区域、分量库存/区域数、停电状态。
- `PowerShortfall` 在 local/grid `NoEnergy` 失败时产生；F4 面板与事件日志展示。
- 存档 schema v9；旧链路默认 `PowerPerTick = 0`。
- Headless 实测：
  `POWER_GRID balanced=True flowed=60 grid=90 regions=3 components=1 blackout=True shortfall=True ok=True`
- 测试：156 仿真 + 48 Jint；600 Tick 双跑哈希 `84a14f0fc04a1201` 一致；截图 `m0-power.png`。

### 5.17 发电溢出、自动取电与潮流时序

- 发电先填本地，满后溢出入区域电网；非活动区激活/保存前按同规则补算。
- 移动/攻击/发信本地不足时自动从电网取电，受每文明恢复储备限制；显式 `DrawEnergyIntent` 同样受储备限制。
- `PowerLatencyTicks` > 0 时创建在途 `PowerShipment`；`PowerLossPercent` 逐跳损耗；在途参与缺口估算。
- 事件：`EnergyAutoDrawn`、`PowerShipped`、`PowerArrived`；诊断显示储备/`IsLoadShed`/在途。
- 存档 schema v10；旧 v9 默认延迟/损耗 0、储备 0、无在途。
- Headless 实测：
  `POWER_RECOVERY overflow=True autoDraw=True reserveShed=True latency=True latencyArrived=True lossArrived=True ok=True`
- 测试：168 仿真 + 48 Jint；600 Tick 双跑哈希 `0e53707713539498` 一致；截图 `m0-power.png`。

### 5.18 电网容量、优先级与断路器

- `GridCapacity` 每区域/文明；溢出与到账超限浪费，显式存能超限返回 `GridFull`。
- `PowerModule.Priority` + `HighPriorityThreshold`/`PriorityReserveEnergy` 实现低优先级负载切除。
- 损耗按路径反向 `ceil` 计算毛额，再正向扣损；100% 损耗链路不参与自动潮流。
- `PowerGridState { BreakerOpen, ManualHold }` 持久化；电网 ≤0 跳闸，储备恢复自动合闸，宿主可手动分/合闸。
- F4 面板新增断路器按钮、容量/优先级/在途显示；`--power-breaker-smoke` 验证接线。
- 存档 schema v11；旧 v10 默认容量无限、优先级 0、断路器关闭。
- Headless 实测：
  `POWER_LIMITS capacityWaste=True priority=True netCompensation=True ok=True`
  `POWER_BREAKER tripped=True drawBlocked=True heldBelowReserve=True autoClosed=True manualHeld=True manualClosed=True ok=True`
- 测试：177 仿真 + 48 Jint；600 Tick 双跑哈希 `7a2114e30b9bdd38` 一致；截图 `m0-power.png`。

## 6. 本轮工程假设（均为可替换原型决策）

1. 位置使用整数毫米，速度与射程使用整数；不使用浮点。
2. 动作成本为固定值（每次移动/开火/发信），未按距离或功率建模。
3. 消息暂只允许发送给同文明；跨文明路由待通信拓扑设计。
4. 实体死亡即从区域移除；其剩余能量不进入统计守恒池，因此守恒测试使用非摧毁场景。
5. 尚无随机数消费者；`WorldState` 也未包含 RNG 状态。
6. 核心区复建为脚本化 API，不是正式建造 UI。
7. 多区域链路延迟/带宽是场景配置；BFS 以跳数为序、链路 ID 破同分；`MessagesPerTick=0` 表示不限。
8. 跨区同文明消息要求目标区域有己方 Comm 端点；跨文明消息要求目标文明端点和入站策略允许；条约/贸易是原型，签约与接单由宿主命令触发，贸易在单区域结算。
9. 材料运输支持多跳路径但无中途仓储；无运输单位实体位置/油耗建模；链路同时承载消息与物资，容量分别限制。
10. 自动潮流默认 `PowerLatencyTicks=0`、`PowerLossPercent=0`；容量按文明共享。发电先入本地储能，满后溢出入电网（受区域容量限制）；设备可在储备/优先级下限以上自动取电。断路器跳闸后需电网达到储备或手动合闸。

## 7. 风险与阻塞

| 风险 | 影响 | 处理 |
|---|---|---|
| Godot 画面仍为原型绘制 | 不能作为商店或正式美术验收 | 当前截图只证明事件可视化链路；正式美术、相机/UI 与动画需后续迭代 |
| mono 模板/导出模板未安装 | 无法导出 Windows 包 | M0 只做编辑器运行；导出阶段再装 mono 导出模板 |
| Jint 不是 OS 沙箱 | 恶意第三方脚本仍可能造成宿主风险 | 第三方可执行包导入保持关闭 |
| 多跳物流无中转仓储 | 货物整段在途，不能在中途装卸/换线 | 后续加入中转节点与容量 |
| 电网模型仍简化 | 已有维护/随机故障/抢占式优先级；仍无多电压/潮流方程与自动化策略 | 后续做电压层/潮流方程；共享链路容量需平衡验证 |
| 文明级 legacy 观察仍保留 | 工具/旧测试可看到全量传感器融合 | 游戏运行路径已用设备观察；后续标记/移除 legacy API |
| 核心区恢复费率未定 | 成本仍由调用方传入；配额/债务/维护已统一，但维护成本默认 0，正式费率 OPEN | 以场景配置验证后再定 R-04 数值；正式版必须配置维护成本 |
| 离屏只延迟发电 | 战斗/制造/物流仍全局结算，性能收益有限 | 后续加入分区 Tick 预算与增量加载 |
| 控制点只在单区域 | 不能验证跨区目标/区域级胜负 | 后续加入跨区目标与区域连接权重 |
| 制造时间未建模 | 部署即时完成，缺少生产节奏 | 后续加入制造队列与时间 |
| 跨文明协议是原型 | 反提议只支持单资源；B 无议价记忆/信誉；无关税或消息签名；贸易只在单区域结算 | 后续做混合资源条款与议价记忆；签名/信誉/跨区贸易需独立 ADR |
| 残骸/废墟模型是原型 | 设备残骸与离线快照切片已可回收；无残骸老化/消失，无废墟实体/占领，快照无签名 | 后续 ADR 定义残骸老化、废墟实体与来源认证 |
| 废墟快照是数据-only | 不支持导入可执行 AI/脚本；第三方可执行包仍关闭 | 完整离线文明包必须等进程隔离/IPC |
| 外壳围困是原型 | 锚点目标只增不减；多跳围困、锚点占领、仓储/产出加成未做 | 先验证单跳围困手感，再扩展拓扑与占领 |
| 第三方对手包未导入 | 内置包 + Phase 1 worker/IPC/Job Object + Phase 2a 签名/同意/CPU/磁盘/fuzz 已可用；AppContainer/OS 网络文件拒绝/独立审查未完成 | 继续按 ADR-0020 的 12 项门禁推进 Phase 2b，未全绿前保持关闭 |
| 存档仅覆盖当前 schema | 尚未验证旧版本迁移 | 后续加入 v1→v2 迁移测试 |

## ADR 索引

| ADR | 主题 |
|---|---|
| [ADR-0001](adr/ADR-0001-dotnet10-runtime-target.md) | .NET 10 运行时目标 |
| [ADR-0002](adr/ADR-0002-m0-headless-slice-assumptions.md) | M0 切片简化假设 |
| [ADR-0003](adr/ADR-0003-prototype-economy-semantics.md) | 原型经济语义 |
| [ADR-0004](adr/ADR-0004-region-activity-and-offscreen-simulation.md) | 区域活动与离屏仿真 |
| [ADR-0005](adr/ADR-0005-cross-civilization-message-boundary.md) | 跨文明消息边界 |
| [ADR-0006](adr/ADR-0006-stateless-message-acl.md) | 无状态消息 ACL |
| [ADR-0007](adr/ADR-0007-treaty-and-trade-protocol.md) | 条约与贸易协议 |
| [ADR-0008](adr/ADR-0008-opponent-diplomacy-responder.md) | 对手外交响应 |
| [ADR-0009](adr/ADR-0009-proactive-and-counter-negotiation.md) | 主动提议与多轮反提议 |
| [ADR-0010](adr/ADR-0010-power-flow-and-diagnostics.md) | 自动电力潮流与诊断 |
| [ADR-0011](adr/ADR-0011-power-overflow-autodraw-latency-recovery.md) | 电力溢出、自动取电与恢复 |
| [ADR-0012](adr/ADR-0012-grid-capacity-priority-loss-breaker.md) | 电网容量、优先级与断路器 |
| [ADR-0013](adr/ADR-0013-power-preemption-breaker-maintenance.md) | 抢占式取电与断路器维护 |
| [ADR-0014](adr/ADR-0014-deterministic-random-breaker-fault.md) | 确定性随机与随机故障 |
| [ADR-0015](adr/ADR-0015-core-recovery-quota.md) | 核心区恢复配额 |
| [ADR-0016](adr/ADR-0016-core-shell-anchors-and-siege.md) | 外壳锚点与围困 |
| [ADR-0017](adr/ADR-0017-device-wrecks-and-salvage.md) | 设备残骸与回收 |
| [ADR-0018](adr/ADR-0018-offline-ruin-snapshots.md) | 离线文明废墟快照 |
| [ADR-0019](adr/ADR-0019-recovery-panel-and-core-maintenance.md) | 恢复面板与核心维护 |
| [ADR-0020](adr/ADR-0020-offline-package-isolation-gate.md) | 离线包隔离门禁与威胁模型 |
| [ADR-0021](adr/ADR-0021-worker-isolation-phase1.md) | 离线包隔离 Phase 1 |
| [ADR-0022](adr/ADR-0022-package-trust-and-phase2-gates.md) | 签名包与 Phase 2a 门禁 |
| [ADR-0023](adr/ADR-0023-test-strategy-and-structural-gates.md) | 测试分层、黄金场景与结构门禁 |
| [ADR-0024](adr/ADR-0024-art-camera-route-world-space.md) | 美术相机路线 A 与世界空间样片 |
| [ADR-0025](adr/ADR-0025-art-gate2-direction-modules-masks.md) | 方向数、模块剪影、核心四态与 mask 验证 |
| [ADR-0026](adr/ADR-0026-world-slice-environment-and-dynamics.md) | Gate 3 工业废土环境与动态表现 |
| [ADR-0027](adr/ADR-0027-spatial-observation-controller-view.md) | 空间索引、共享观测与控制器观测视图 |
| [ADR-0028](adr/ADR-0028-parallel-controller-batch.md) | 并行控制器批处理与观测批次 |
| [ADR-0029](adr/ADR-0029-deterministic-combat-randomness-and-loot.md) | 确定性战斗随机、伤害变体与掉落 |

## 8. 下一步（按依赖顺序）

1. 离线包隔离 Phase 2b：AppContainer/低完整性、OS 网络/文件拒绝、只读挂载、OS 磁盘配额、完整 fuzz/红队、发布签名密钥与独立安全审查（仍不开放导入）；废墟实体/占领/清理；锚点占领/仓储/产出加成；清理文明级 legacy 观察 API。
2. 混合资源反提议、议价记忆/信誉；关税/跨区贸易与消息签名单独立 ADR。
3. 电网后续：维护时长、多电压/潮流方程与断路器自动化策略。
4. 用 `DeterministicRandom` 实现可复现的命中/损伤变体与掉落；补随机流回放测试。
5. 存档迁移/回放与离线文明包。
6. 可视切片：正式相机/缩放、核心区与网络表现、诊断面板。
7. 安全：进程隔离、IPC 校验、资源限额；完成前不开放第三方包执行。
