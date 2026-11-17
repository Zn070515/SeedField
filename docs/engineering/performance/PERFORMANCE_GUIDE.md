# SeedField 性能工程与发行验收指南

> 状态：**Accepted（测量方法/已测数据）；发行导出与混合负载仍为 OPEN**  
> 日期：2026-11-17  
> 关联：ADR-0027、ADR-0028、ADR-0029、ADR-0030、ADR-0033、ADR-0035、ADR-0036  
> 跟踪 Issue：#14、#15、#16、#17

## 1. 目的

本文件回答三个问题：

1. 当前 Headless 仿真 + JS 控制器能跑多快、支撑多少设备；
2. 这些数字如何复现、如何防止回退；
3. 哪些结论还没有在“普通玩家 + 正式 Godot 客户端”环境里验证。

**这不是渲染性能报告。** 文中毫秒数都是仿真的每 Tick 权威计算耗时，不是
FPS；Godot 渲染、动画、UI、地形与未来游戏系统另算。

## 2. 结论摘要

Release、AMD Ryzen 9 9950X（16C/32T）、Server GC、3000 Tick 稳态：

| 场景 | avg | p50 | p95 | p99 | alloc/tick | GC 暂停/3000 | 峰值 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 100 台 | **1.24 ms** | 0.97 | 3.31 | 4.21 | 4.13 MB | 207 ms | 164 MiB |
| 1000 台 | **7.55 ms** | 5.83 | 11.53 | 22.77 | 50.85 MB | 3988 ms | 413 MiB |

- 100 台约 809 ticks/s；1000 台约 132 ticks/s（Headless 控制器负载）。
- 1000 台 p99 约 22.8 ms，主要来自 GC 暂停，不是脚本单次执行。
- 平均每 Tick 分配约 **50.85 MB**（1000 台）；这是下一阶段最重要的优化目标。
- 当前游戏 `TicksPerSecond = 6`：1000 台在 6 Hz 的 167 ms 预算内余量很大；
  60 Hz（16.7 ms）不适用于 1000 台，也不应作为近期目标。

### 与早期基线的对比

| 阶段 | 100 台（10 Tick） | 1000 台（10 Tick） |
|---|---:|---:|
| ADR-0027 空间索引后 | 33.96 ms | 180.36 ms |
| 并行批处理（PR #7） | 7.73 | 118.42 |
| Server GC + 预编译（PR #8） | 4.12 | 60.12 |
| 紧凑观测（PR #9） | 1.67（稳态） | 11.6（稳态） |
| 直接 JS 对象（PR #9） | **1.18（稳态）** | **7.9（稳态）** |

早期与后期的脚本工作量、预热窗口不同，不能把最初 200 ms/Tick 直接当作
严格的 25× 单变量提升；上表用于说明方向与量级。

## 3. 测量方法

### 3.1 环境

- Release 构建；
- Server GC（Headless 项目属性）；
- 记录 CPU 型号、逻辑核数、内存、OS；
- 不混用 Debug/Release 数据。

### 3.2 命令

```powershell
dotnet build SeedField.slnx -c Release

# 100 台长窗口
dotnet run --project src/SeedField.Headless -c Release --no-build -- `
  --benchmark-jint-parallel 100 --ticks 3000 > logs/perf-100.log 2>&1

# 1000 台长窗口
dotnet run --project src/SeedField.Headless -c Release --no-build -- `
  --benchmark-jint-parallel 1000 --ticks 3000 > logs/perf-1000.log 2>&1

# 写入本地目录（可选）
dotnet run --project src/SeedField.Headless -c Release --no-build -- `
  --benchmark-jint-parallel 1000 --ticks 3000 --catalog-db "$env:TEMP/seedfield-perf.db"

# 固定机严格基线
python tools/check_perf.py `
  --baseline docs/engineering/evidence/perf-baseline.json `
  --current logs/perf-1000.log --devices 1000

# 正式客户端混合负载（Godot windowed，真实 scout + 渲染）
godot --path game/SeedField.Game -- --benchmark-mixed=1000 --benchmark-mixed-seconds=10
# 对照：不发消息的 quiet 脚本
godot --path game/SeedField.Game -- --benchmark-mixed=1000 --benchmark-mixed-seconds=10 `
  --benchmark-mixed-quiet
```

不要把长命令接 `| tail` / `| head`；重定向到日志后读文件。

### 3.3 输出字段

`JINT_PARALLEL` 行包含：

- `avgTickMs`、`ticksPerSec`；
- `p50Ms/p95Ms/p99Ms`（每 Tick 墙钟）；
- `obsMs/decideMs/stepMs`；
- `allocMBPerTick`；
- `gc0/gc1/gc2`、`gcPauseMs`；
- `peakWorkingSetMb`、`dof`、`gcServer`。

### 3.4 核心数档位

```powershell
$env:DOTNET_PROCESSOR_COUNT = "4"   # 4/6/8
$env:DOTNET_gcServer = "1"          # 或 0 比较 Workstation
dotnet run --project src/SeedField.Headless -c Release --no-build -- `
  --benchmark-jint-parallel 1000 --ticks 300
```

`DOTNET_PROCESSOR_COUNT` 是逻辑核模拟，不是真实物理机分档；正式验收要在
4/6/8 核实机复测（Issue #14/#15）。

## 4. 已测分档

1000 台、300 Tick；每格为 avg / p95 / p99（ms）：

| 核心 | Server GC | Workstation GC |
|---:|---|---|
| 4 | **19.85 / 65.49 / 76.93** | 59.38 / 87.06 / 111.68 |
| 6 | **15.56 / 35.22 / 51.94** | 58.96 / 86.95 / 90.67 |
| 8 | **14.07 / 33.93 / 50.65** | 56.48 / 86.24 / 92.68 |

- Server GC 在 4/6/8 核上约 **3×** 快于 Workstation，代价是内存约 2 倍。
- 4 核 Server 的 1000 台平均约 19.9 ms，仍在 6 Hz 预算内；p99 约 77 ms
  主要来自 GC，需要后续 LOD/休眠/对象复用。
- 60 Hz 的 16.7 ms 预算不适合 1000 台；100 台是更现实的高频档位。

### 4.1 混合客户端（Godot windowed）

Godot 4.7.2、1280×720、vsync off、Server GC、真实 `program.scout.v1` +
渲染 + 通信/战斗，测量 2 s（跳过 1.5 s 预热）：

| 设备 | avgTick | p95Tick | FPS | alloc/Tick | Fault |
|---:|---:|---:|---:|---:|---:|
| 100 | 1.91 ms | 3.24 | 154 | 11.9 MB | 0 |
| 500 | 9.82 ms | 15.48 | 62 | 42.0 MB | 0 |
| 1000 | 27.96 ms | 34.38 | 28 | 92.1 MB | 0 |

修复前（完整收件箱进入每个观测）：

| 设备 | avgTick | p95Tick | FPS | alloc/Tick | Fault |
|---:|---:|---:|---:|---:|---:|
| 500 | 137.03 ms | 174.95 | 14 | 1087 MB | 24 timeout |
| 1000 | 309.32 ms | 372.63 | 5 | 2891 MB | 24 timeout |

根因与修复见 ADR-0037：观测消息历史限制为最近 32 条；收件箱容量/世界状态/
存档/哈希不变。quiet 脚本 1000 台约 5.2 ms/Tick、33 FPS，证明剩余成本在
渲染/呈现层。

## 5. GC 发行要求

Godot 宿主使用自己的 runtimeconfig，项目里的
`<ServerGarbageCollection>true</ServerGarbageCollection>` 对游戏进程无效。

- 启动游戏必须设置 `DOTNET_gcServer=1`；
- `tools/run-game.ps1` 已封装该环境变量；
- `Main._Ready` 打印 `gcServer`，Workstation 时 `GD.PushWarning`；
- `godot-smoke.yml` 的 “Verify GC mode switch” 验证：
  - 无 env → `gcServer=False`；
  - 有 env → `gcServer=True`；
- 正式 Windows 导出/Steam 启动项仍需设置该环境变量并记录实际模式
  （Issue #14）。不要用 Benchmark 的 Server GC 数据直接代表玩家环境。

## 6. 优化清单与保留理由

| 优化 | 结论 |
|---|---|
| 2,000 mm 空间索引 | 保留；避免每设备全区域扫描 |
| 每 Tick ObservationCache | 保留；缓存失效边界明确 |
| 设备作用域观测 | 保留；性能 + 情报模型双收益 |
| 并行控制器批处理 | 保留；决策并行、Intent 按稳定顺序串行提交 |
| Server GC | 保留，但必须分档验收 |
| Prepared Script 跨引擎缓存 | 保留；不共享设备运行状态 |
| 进程内直接 JsObject | 保留；避免 JSON 文本/parse，需边界测试 |
| 离屏控制器跳过 | 有条件保留；脚本计时器暂停，见 Issue #17 |

**架构边界**：并行只发生在控制器决策；`SimulationEngine.Step` 仍是唯一权威
世界结算者，Intent 仍按设备稳定顺序提交。

## 7. 确定性与墙钟超时

- 固定 Tick、整数坐标、稳定 ID、世界哈希与回放 Intent 记录保持不变。
- `SCRIPT_STATEMENT_LIMIT` / `SCRIPT_MEMORY_LIMIT` 是确定性边界；
- `SCRIPT_TIMEOUT`（默认 50 ms）是**宿主安全阀**，受负载/调度/GC 影响，
  不属于游戏内 C 算力结算；
- 回放记录已经发生的 Intent 并验证世界哈希；它不能证明重新执行玩家脚本
  一定得到相同 Intent；
- 新增测试覆盖顺序/并行语句上限一致、Timeout Fault 分类（ADR-0036）。

## 8. 门禁

两层：

1. **CI 宽松门禁**：`--benchmark-jint-parallel 100 --budget-ms 60` +
   `gcServer=True` 断言；吸收 GitHub 共享 Runner 噪声，只防灾难性退化。
2. **固定机严格基线**：
   - `docs/engineering/evidence/perf-baseline.json`；
   - `tools/check_perf.py` 对 `avgTickMs`、`p95Ms` 默认 +25% 回归失败；
   - 长窗口 3000 Tick，多轮取中位数；
   - 结果可写入本地 Catalog（ADR-0035）或证据文件。

## 9. 未验证与下一轮

按优先级：

1. **正式客户端混合负载**：已加入 `--benchmark-mixed`，并修复观测消息放大
   （ADR-0037）；1000 台约 28 ms/Tick、28 FPS。仍需正式导出、真实 GPU/
   低端硬件与更长窗口验收。
2. **真实 4/6/8 核硬件**：当前只有 `DOTNET_PROCESSOR_COUNT` 模拟；
   需要低端 CPU、不同内存档位的真实数据。
3. **长时间稳定性**：10k+ Tick 的内存增长、P99 尾部、GC 暂停与泄漏。
4. **跨进程 PvE**：AppContainer worker 多对手数量与 IPC 开销；不要把
   进程内 Jint 结果当作社区包性能结论。
5. **确定性压力测试**：高负载/低负载下 Fault、Intent、世界状态与回放一致性。
6. **离屏 Catch-up**：非活动区脚本暂停的语义、离线文明发展与哈希一致性
   （Issue #17）。

## 10. 常见误读

- **7.9 ms/Tick 不是 125 FPS 游戏**：它是 Headless 仿真 + 控制器负载；
  渲染和未来系统另算。
- **1000 个 B 方实体不等于 1000 个 JS 控制器**：基准中 A 方 1000 台可编程
  设备，B 方并非全部独立执行脚本。
- **QuickJS 数据只适用于本轮 spike**：多 runtime/P/Invoke 方式下 Jint 更快；
  不能推出“QuickJS 永远比 Jint 慢”。
- **早期 200 ms 不是严格 25× 基线**：脚本工作量/预热窗口不同。
- **Server GC 不是默认发行行为**：Godot 必须显式设置环境变量。

## 11. 相关文件

- ADR-0027：空间索引与控制器观测
- ADR-0028：并行控制器批处理
- ADR-0029：确定性战斗随机/掉落
- ADR-0030：确定性回放
- ADR-0033：离屏控制器抑制
- ADR-0035：本地 SQLite 派生目录
- ADR-0036：墙钟超时宿主安全阀
- ADR-0037：观测消息历史确定性上限
- 证据：`docs/engineering/evidence/2026-11-17-perf-release-acceptance.md`
- 证据：`docs/engineering/evidence/2026-11-17-mixed-client-message-cap.md`
- 基线：`docs/engineering/evidence/perf-baseline.json`
- 工具：`tools/check_perf.py`、`tools/run-game.ps1`
