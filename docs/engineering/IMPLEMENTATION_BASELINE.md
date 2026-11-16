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
- 多区域通信：显式 `RegionLink`、确定性 BFS 路由、跨区延迟、链路带宽与目标区域校验。

### 尚未实现

- TypeScript `.d.ts` 编辑契约。
- 跨区物流/能源传输、区域加载/卸载、跨文明通信。
- 存档迁移（v1 → 后续）、回放、离线文明包。
- OS 级进程隔离与 IPC（因此第三方 JS 导入保持关闭）。
- `DeterministicRandom`、随机命中与正式平衡数值。
- PvE 对手、教程/科技树/终局与正式美术。

## 4. 已实测的 API 与版本

| 项 | 实测结果 |
|---|---|
| .NET SDK | 10.0.404 |
| .NET Runtime | 10.0.15 |
| NuGet Jint | 4.18.0 可还原，net10.0 资产可用 |
| `Options.TimeoutInterval` / `MaxStatements` / `LimitMemory` | 可用 |
| `Options.Host.StringCompilationAllowed` | 可用；`eval` 被拒绝 |
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
- 实测：`SAVE_SMOKE ok=True before=ef6618... mutated=cd95ff... after=ef6618...`。

### 5.5 多区域与跨区消息

- `RegionLink` 是显式双向链路，带 `LatencyTicks` 与可选的每 Tick 消息上限。
- 跨区消息要求：目标区域存在、目标区域有己方 Comm 端点、存在链路路径。
- 路由为双向 BFS：每层扩展较小 frontier，同规模时正向优先；命中后反转正向半段并拼接反向半段。
- 总延迟 = Comm 延迟 + 路径链路延迟之和。
- 链路容量在发送验证阶段检查，成功后才计入本 Tick 用量。
- Headless 实测：`r0 → r1(2t) → r2(3t)` + Comm 1t → `deliverTick=6`，目标区域与投递 Tick 均正确。

## 6. 本轮工程假设（均为可替换原型决策）

1. 位置使用整数毫米，速度与射程使用整数；不使用浮点。
2. 动作成本为固定值（每次移动/开火/发信），未按距离或功率建模。
3. 消息暂只允许发送给同文明；跨文明路由待通信拓扑设计。
4. 实体死亡即从区域移除；其剩余能量不进入统计守恒池，因此守恒测试使用非摧毁场景。
5. 尚无随机数消费者；`WorldState` 也未包含 RNG 状态。
6. 核心区复建为脚本化 API，不是正式建造 UI。
7. 多区域链路延迟/带宽是场景配置；BFS 以跳数为序、链路 ID 破同分；`MessagesPerTick=0` 表示不限。
8. 跨区消息只允许同文明，且目标区域必须有己方 Comm 端点；跨文明路由尚未开放。

## 7. 风险与阻塞

| 风险 | 影响 | 处理 |
|---|---|---|
| Godot 画面仍为原型绘制 | 不能作为商店或正式美术验收 | 当前截图只证明事件可视化链路；正式美术、相机/UI 与动画需后续迭代 |
| mono 模板/导出模板未安装 | 无法导出 Windows 包 | M0 只做编辑器运行；导出阶段再装 mono 导出模板 |
| Jint 不是 OS 沙箱 | 恶意第三方脚本仍可能造成宿主风险 | 第三方可执行包导入保持关闭 |
| 跨区物流/能源未建模 | 只能验证通信因果，不能验证物资/电力跨区 | 下一阶段加入显式运输与能源链路 |
| 区域加载/卸载未实现 | 长期多区域世界的性能与一致性未验证 | 后续加入离屏推进与加载回归 |
| 制造时间未建模 | 部署即时完成，缺少生产节奏 | 后续加入制造队列与时间 |
| 跨文明通信未开放 | 无法验证协议、结盟或断联对抗 | 先完成路由权限与情报过滤 |
| 存档仅覆盖当前 schema | 尚未验证旧版本迁移 | 后续加入 v1→v2 迁移测试 |

## 8. 下一步（按依赖顺序）

1. 跨区物流与能源：显式运输队列、跨区供能/断供、区域加载/卸载一致性。
2. 跨文明协议与 PvE：目标区域权限、情报过滤、内置离线对手。
3. 经济闭环：制造队列/时间与断供/断联诊断。
4. `DeterministicRandom` 与种子状态进入哈希；为命中/损伤变体提供可复现实验。
5. 存档迁移/回放与离线文明包。
6. 可视切片：正式相机/缩放、核心区与网络表现、诊断面板。
7. 安全：进程隔离、IPC 校验、资源限额；完成前不开放第三方包执行。
