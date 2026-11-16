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

### 尚未实现

- Godot 工程、可视机体、网络/战斗表现。
- TypeScript `.d.ts`。
- 多区域、跨区物流/通信、区域加载。
- 存档、迁移、回放、离线文明包。
- OS 级进程隔离与 IPC（因此第三方 JS 导入保持关闭）。
- 随机数消费者、战斗命中随机性、正式平衡数值。
- C/S/B 容量、工坊、蓝图版本、科技树、教程、终局。

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

## 6. 本轮工程假设（均为可替换原型决策）

1. 位置使用整数毫米，速度与射程使用整数；不使用浮点。
2. 动作成本为固定值（每次移动/开火/发信），未按距离或功率建模。
3. 消息暂只允许发送给同文明；跨文明路由待通信拓扑设计。
4. 实体死亡即从区域移除；其剩余能量不进入统计守恒池，因此守恒测试使用非摧毁场景。
5. 尚无随机数消费者；`WorldState` 也未包含 RNG 状态。
6. 核心区复建为脚本化 API，不是正式建造 UI。

## 7. 风险与阻塞

| 风险 | 影响 | 处理 |
|---|---|---|
| Godot 画面仍为原型绘制 | 不能作为商店或正式美术验收 | 当前截图只证明事件可视化链路；正式美术、相机/UI 与动画需后续迭代 |
| mono 模板/导出模板未安装 | 无法导出 Windows 包 | M0 只做编辑器运行；导出阶段再装 mono 导出模板 |
| Jint 不是 OS 沙箱 | 恶意第三方脚本仍可能造成宿主风险 | 第三方可执行包导入保持关闭 |
| 单区域/单一路由 | 无法验证跨区因果 | 下一阶段建立多区域与队列测试 |
| 无存档 | 无法验证迁移与长期运行 | 在 M2 前引入版本化存档 |

## 8. 下一步（按依赖顺序）

1. 迭代可视切片：正式相机/缩放、核心区与通信网络表现、诊断面板；补装导出模板后再做 Windows 包。
2. 程序版本持久化：把 `ControllerSlot` 的部署脚本与版本写入存档，重启后仍可恢复。
2. 引入 `DeterministicRandom` 与种子状态，进入哈希；为命中/损伤变体提供可复现实验。
3. 建立版本化 `WorldSave` 最小 schema 与迁移测试。
4. 多区域：区域索引、跨区消息队列、离屏区推进一致性测试。
5. 蓝图/实例分离与工坊最小闭环。
6. 核心区完整五分支产品登记与复建链（当前只有最小实体）。
7. 安全：进程隔离、IPC 校验、资源限额；完成前不开放第三方包执行。
