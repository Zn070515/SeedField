# 证据：并行控制器批处理（2026-11-17）

- 分支：`perf/parallel-controller-batch`
- 基线：`main` @ `6719ab9`（含 ADR-0027）
- 机器：AMD Ryzen 9 9950X（16 核 / 32 逻辑核），Windows 11 Pro for Workstations
- 构建：`dotnet build SeedField.slnx -c Release`
- 运行：`dotnet run --project src/SeedField.Headless -c Release --no-build`

## 0. 第二轮结果（Server GC、默认 DOF、预编译脚本）

在 PR #7 的并行批处理之上继续实验，全部保持脚本调用频率与 Intent 顺序不变：

| 场景 | PR #7 | 本轮 | 提升 |
|---|---:|---:|---:|
| 100 台 | 7.73 ms | **4.12 ms** | 1.9× |
| 1000 台 | 118.42 ms | **60.12 ms** | 2.0× |
| 1000 台启动 | ~205 ms | **~127 ms** | 1.6× |
| 1000 台启动内存 | ~77 MiB | **~50 MiB** | -35% |

- **Server GC**：Headless 通过 csproj 启用；Godot 宿主忽略项目 runtimeconfig，
  需 `DOTNET_gcServer=1`（smoke workflow 已设置，`gcServer=True` 已验证）。
- **默认 DOF**：`Environment.ProcessorCount`；server GC 下 32 线程在 1000 台
  优于 16 线程，100 台也从 ~5.9 降到 ~4.1 ms。
- **预编译脚本**：`Engine.PrepareScript` 跨引擎共享，1000 台启动 205→127 ms，
  内存 77→50 MiB。
- 1000 台峰值工作集约 335 MiB（server GC 的代价；workstation 约 250 MiB）。

### 未采用的实验

- 直接读取 JS 返回值、跳过 `JSON.stringify` + `JsonDocument.Parse`：输出只有
  1–2 个 intent，收益小于复杂度与契约风险。
- 手写观察 JSON 序列化：序列化已随 decide 并行，wall-time 占比低；
  自定义转义/字段顺序的风险高于收益。
- 共享观察 DTO 快照：观测仅占 1000 台的约 7 ms，收益有限，暂不做。

## 0b. 硬件与运行时实验（Ryzen 9 9950X / Zen 5 / 双 CCD）

采样：`dotnet-trace collect --profile dotnet-sampled-thread-time`，1000 台、
3000 Tick。worker 侧热点（inclusive）：

| 方法 | inclusive |
|---|---:|
| `JsController.RunJson` | 50.8% |
| `DeviceControllerSlot.Decide` | 58.7% |
| `JsonParser.ParseJsonObject` | 28.4% |
| `ArrayInstance.Filter`（脚本） | 7.5% |
| `ObservationBuilder.BuildForEntity` | 5.2% |
| `JsonSerializer.Serialize` | 1.8% |

原始采样里 `Thread.PollGC` 与信号量等待占多数，是 Server GC 后台线程的空闲
等待，不是 worker 成本。结论：worker 侧由 **Jint JSON.parse + 解释执行** 主导，
观测与序列化已不是瓶颈。

稳态（3000 Tick）：**100 台 1.77–2.01 ms/tick（约 560 ticks/s）、约 150 MiB；
1000 台 15.7 ms/tick（约 64 ticks/s）、约 424 MiB**。10 Tick 的 4.1 / 50–60 ms
包含 JIT/GC 预热，游戏长时间运行应看稳态值。

| 实验 | 结果 | 决定 |
|---|---|---|
| Server GC | 1000 台 10 Tick ~104→60 ms；稳态 15.7 ms | 保留 |
| DOF=逻辑核数 | 1000 台 32 线程优于 16 | 保留 |
| 绑定单 CCD（0xFFFF / 0xFFFF0000） | 26.5 ms vs 未绑定 22.3 ms | 放弃（更慢） |
| 偶数/奇数逻辑核绑定 | 与未绑定持平 | 放弃 |
| 结果数组 64B 填充（伪共享） | 差异在噪声内 | 放弃 |
| `TieredCompilation=0` | 300 Tick 18.9 vs 20.8，但 3000 Tick 21.2 vs 15.7 | 放弃（PGO 长期胜出） |
| `GCgen0size=32MB` / 8MB | 26–28 / 20–21 ms | 放弃 |
| `GCHeapCount=16 + NoAffinitize` | 39 ms 但 735 MiB | 放弃（内存代价） |
| `gcConcurrent=0` | 方差大、暂停风险 | 放弃 |
| 大页 `GCLargePages=1` | 进程未正常启动（权限） | 未验证 |

Zen 5 的 CCD 绑定没有收益；AVX-512 对 Jint 的标量解释器没有可接入的热点。
硬件层面的有效杠杆是 **Server GC + 默认分层 PGO + 逻辑核并行度**；进一步
提升需要改脚本模型或换成原生 JS 引擎，属于独立 ADR。

## 0c. 紧凑观测与可信展开器

控制器边界 JSON 从对象属性改为位置数组 + 字符串表，可信 harness 展开回原对象：

| 指标 | 完整 JSON | 紧凑观测 |
|---|---:|---:|
| 平均 payload | 8424 B | **1966 B**（0.23×） |
| Jint decide（1000 台，DOF 32，隔离） | 7.06 ms | **2.25 ms**（3.1×） |
| Jint decide（DOF 1） | 65.9 ms | **24.3 ms** |
| 真实 tick 稳态 1000 台 | 15.7 ms | **11.6 ms**（86 ticks/s） |
| 真实 tick 稳态 100 台 | 1.8 ms | **1.67 ms**（600 ticks/s） |
| 峰值工作集 1000 / 100 台 | 424 / 150 MiB | 650 / 200 MiB |

- 契约验证：脚本 `JSON.stringify(obs)` 与 `SerializeFullObservation` 语义相等；
  worker 双跑哈希一致；Godot `--save-smoke` 通过。
- 10 Tick 短窗口 100 台约 4.7–5.5 ms，因展开预热略慢于完整路径；长跑才体现收益。
- `--chunk-size` 分块（500/250/128/64）与预序列化均更慢，未采用。

## 0d. QuickJS 原生引擎可行性

用 QuickJS-NG（CMake Release，`libqjs.dll`）建立 1000 个独立 runtime，同一
脚本与观测，P/Invoke 调用 `JSON.parse`/`JSON.stringify`：

| DOF | Jint ms/1000 | QuickJS ms/1000 |
|---:|---:|---:|
| 1 | 63.5 | 85.5 |
| 8 | 10.6 | 89.8 |
| 16 | 7.3 | 252.4 |
| 32 | 6.3 | 232.7 |

QuickJS 单线程即慢于 Jint，且多 runtime 并发下分配器/全局竞争导致几乎不缩放，
不适合替换；V8/ClearScript 需要大依赖与 JIT/沙箱重做，留作独立 ADR。

## 0e. 9950X 缩放与 GC

- 32 线程相对 1 线程仅 2.5×（1000 台 60 Tick：97.6 → 38.9 ms）。
- `dotnet-counters`：分配率约 **8.5 GB/s**，Gen0/Gen1 约 65 次/秒，
  **约 27% 时间在 GC**；全核约 122% 基频（约 5.25 GHz），不是降频问题。
- 缓存/拓扑：16 核、SMT 相邻（0/1）、每核 L2 1 MiB、双 CCD 各 32 MiB L3、
  DDR5-6000 96 GiB、AVX-512 F/BW/DQ/CD/VBMI2。

## 1. 探针结论（仓库外，Jint 4.18.0）

在 `%TEMP%` 的独立探针中复现 100 个独立 `Engine` 与真实观测尺寸：

| 方案 | 65 实体 / 15 KiB | 结论 |
|---|---:|---|
| 当前 JSON 文本 + `JSON.parse` | 10.6 ms / 100 次 | 基线 |
| 直接注入 `JsObject` 图 | 16.1 ms / 100 次 | 更慢，放弃 |
| 去掉 Jint 约束 | 9.1 ms / 100 次 | 仅省 14%，且削弱安全边界 |

约束不是瓶颈；解析与脚本执行随可见实体数线性增长。

## 2. 并行批处理

命令：

```powershell
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint 100
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint-parallel 100 --parallel-dof 16
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint 1000
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint-parallel 1000 --parallel-dof 16
```

三次运行的中间值：

| 场景 | 观测 | 序列化 | decide | step | 总/tick | ticks/s | 峰值工作集 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 100 台顺序 | 1.25–2.35 | 2.28–2.51 | 28.37–30.10 | — | 33.96 ms | 29.4 | — |
| 100 台并行 | 0.96 | 含在 decide | 6.34 | 0.21 | **7.46 ms** | **134.0** | 78 MiB |
| 1000 台顺序 | 10.71 | 20.46 | 145.20 | — | 180.36 ms | 5.5 | — |
| 1000 台并行 | 8.76–10.22 | 含在 decide | 86.38–89.65 | 3.72–6.55 | **103.71 ms** | **9.6** | 253 MiB |

- 100 台：33.96 → 7.46 ms（4.6×），低于 60 Hz 的 16.7 ms。
- 1000 台：180.36 → 103.71 ms（1.7×），低于 6 Hz 的 167 ms。

## 3. 并行度

1000 台、并行观测后的对比：

| DOF | 总/tick（两次） |
|---:|---:|
| 8 | 112.6 / 114.5 ms |
| 16 | 104.0 / 119.8 ms（多次中位约 104–114） |
| 24 | 98.9 / 99.9 ms |
| 32 | 95.6 / 99.6 ms |

默认取 16：已满足 6 Hz，并给主线程渲染留出核心；`--parallel-dof` 可覆盖。

## 4. 正确性

```text
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

结果：

- 234 simulation + 18 repository + 89 Runtime.Js = **341 passed**。
- 新增测试：
  - `ControllerBatchRunnerTests.ReturnsDecisionsInInputIndexOrder`
  - `ControllerBatchRunnerTests.ParallelBatchKeepsOrderAndIsolatesFaults`
  - `ControllerBatchRunnerTests.ParallelBatchMatchesSequentialOnScriptedCombat`
  - `DeviceObservationTests.ParallelObservationBatchMatchesSerial`
  - `JsControllerTests.RejectsObservationAboveByteLimitBeforeParsing`
- 顺序与并行 20 Tick 脚本化战斗：事件序列与 `WorldStateHasher` 一致；
  黄金场景哈希未改变（本轮不新增世界状态字段）。
- Godot headless save smoke（真实 Main 的 `StepOnce` 走并行批处理）：

```text
SEEDFIELD_PRERUN tick=34 shots=4 entities=4
SAVE_SMOKE ok=True before=59610b21d94d1119 mutated=d02ed2e97f69178f after=59610b21d94d1119
```

## 5. 预算门禁

```powershell
dotnet run --project src/SeedField.Headless -c Release --no-build -- --benchmark-jint-parallel 100 --budget-ms 0.01
# BUDGET_EXCEEDED ... ; exit code 2
```

CI 使用 100 台 `--budget-ms 60` 并断言 `gcServer=True`：阈值吸收共享 runner
的噪声，`gcServer` 断言防止 Server GC 配置丢失；开发机基线以上表为准。
夜间/本地可用 `--parallel-dof` 覆盖并行度做进一步实验。

线程池调用方会自动回退串行：xUnit 在 4 核 runner 上并发跑测试时，
`Parallel.For` 阻塞测试线程会造成线程池饥饿；守护后 CI 测试不再挂起。

## 6. 未验证与风险

- CI runner 的核数/负载与开发机不同；阈值只防明显回退，不是精确性能断言。
- 脚本若依赖同一设备跨 Tick 的内部状态，仍按每 Tick 调用，语义不变；但若依赖
  墙钟，则本来就不确定。
- WorkerController 的独立进程路径未纳入并行批处理。
- 1000 台峰值工作集约 253 MiB；更大规模需要重新评估引擎数量与观测缓存生命周期。
