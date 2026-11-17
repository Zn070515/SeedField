# ADR-0028：并行控制器批处理与观测批次

- 状态：**Accepted**
- 日期：2026-11-17
- 关联决策：PERF-01、PERF-03、SEC-01
- 前置：ADR-0027、ADR-0023

## 背景

ADR-0027 之后，`--benchmark-jint 100`（Release，开发机）仍是约 31–36
ms/tick；1000 台约 180 ms/tick。分阶段计时显示瓶颈已从观测构建转移到 Jint
执行：

| 设备 | 观测 | 序列化 | Jint |
|---:|---:|---:|---:|
| 100 | 1.3 ms | 2.3 ms | 28.7 ms |
| 1000 | 10.7 ms | 20.5 ms | 145.2 ms |

每台设备可见 1–65 个敌方实体（平均 29.1），观测 JSON 平均 6.9 KiB，最大
15 KiB。Jint 单线程逐设备执行，100 台已超过 60 Hz 的 16.7 ms 预算。

### 已评估但未采用

- **收紧 Jint 约束**：语句/超时/内存限制合计只占约 10–15%，且属于安全边界，
  不应为性能削弱。
- **直接把观测注入 Jint 对象图**：65 实体时约 16 ms/100 次，比
  `JSON.parse` 的 10.6 ms 更慢；`JsObject.Set` 的属性设置成本高于解析。
- **默认休眠/错峰**：会改变脚本被调用频率与内部状态推进节奏，违背“不牺牲
  正确语义”。当前并行已满足目标，不引入。
- **原生 C++ 重写观测/JSON**：这两段合计只占约 13%，单独重写不足以达到目标；
  瓶颈是 JS 解释执行。
- **替换为 QuickJS/V8**：会改变技术栈、沙箱与分发模型，需要单独 ADR，本轮不做。
- **Zen 5 CCD 绑定 / 伪共享填充 / 关闭分层编译 / GC 代大小调优**：9950X 上
  均未在稳态胜出（CCD 绑定更慢，关闭分层编译长期更慢）；保留默认分层 PGO
  与 Server GC。详见证据文件。

## 非目标

- 不改变每 Tick 调用一次 `decide` 的脚本契约，不引入默认休眠/错峰。
- 不替换 Jint 或引入原生 JS 引擎。
- 不改变 SavedGame / WorldState schema。
- 不把控制器批处理扩展到跨文明共享全局状态。

## 决定

### 1. 并行控制器批处理

新增 `ControllerBatchRunner`（`SeedField.Runtime.Js`）：

- 输入为按稳定设备顺序排列的 `(Index, DeviceControllerSlot, Observation)`；
- 每个设备已有独立 `JsController`/`Engine` 实例，可安全并行；
- 当批次 ≥ `MinBatchSize`（默认 8）且线程数 > 1 时使用 `Parallel.For`；
- 输出写到 `decisions[Index]`，调用方仍按设备顺序拼接 Intent；
- 单个控制器抛出的异常转为 `ControllerDecision.Fault`，不影响同批其他设备；
- 默认 `MaxDegreeOfParallelism = clamp(ProcessorCount, 1, 16)`：16 线程在
  1000 台时比 32 线程更省资源，且已满足 6 Hz 预算。

### 2. 并行观测批次

新增 `SimulationEngine.BuildControllerObservations`：

- 每个 worker 通过 `Parallel.For` 的 local-init 创建自己的 `ObservationCache`；
- 共享的每 Tick 缓存不是线程安全的，因此绝不在并行区共享；
- 结果按输入顺序返回；
- Main 与 Headless 基准均先并行构建观测，再进入控制器批处理。

### 3. 保守热路径

- `JsController` 缓存 `__seedfield_run` 函数值，避免每次按名字查找；
- `MaxObservationBytes` 检查使用 UTF-16 长度快速判定，仅中间区间调用
  `GetByteCount`；
- `ObservationCache` 缓存每区域/文明的消息、物流、控制点、条约、提议、
  贸易单切片，设备观测不再重复扫描收件箱/外交状态。

### 4. 语义边界

- 控制器仍然 **每 Tick 调用一次** `decide`；
- Intent 仍以设备稳定顺序提交给 `SimulationEngine.Step`；
- 并行只改变计算发生的线程，不改变世界状态的唯一写者；
- 部署/编辑只在 Tick 之间由主线程执行，不与批处理重叠；
- 脚本仍只收到 JSON 文本，worker 隔离路径不变。

### 5. Server GC

- `SeedField.Headless` 启用 `<ServerGarbageCollection>true</ServerGarbageCollection>`；
- Godot 宿主使用自己的 runtimeconfig，项目属性不生效；启动时需设置
  `DOTNET_gcServer=1`（`godot-smoke.yml` 已设置并验证 `gcServer=True`）；
- Jint 每设备独立堆、分配密集，server GC 的并行回收把 1000 台从约 104 ms
  降到约 60 ms；代价是 1000 台峰值工作集约 335 MiB（workstation 约 250 MiB）；
- 默认并行度改为 `Environment.ProcessorCount`；在 server GC 下 32 线程于
  1000 台明显快于 16 线程，`--parallel-dof` 仍可覆盖。

### 6. 预编译脚本

`JsController` 使用 `Engine.PrepareScript` 解析/分析一次控制器源码，
`Prepared<Script>` 跨引擎共享（线程安全）；按源码缓存，超过 128 条时清空。
1000 台控制器启动从约 205 ms 降到约 127 ms，峰值内存从约 77 MiB 降到
约 50 MiB；每 Tick 行为不变。

### 7. 观测传输：进程内直接对象，跨进程紧凑 JSON

- **进程内 `Decide`**：`ObservationJsValue` 用 `JsObject.Create` + 共享
  `JsObjectLayout` 直接构造观测对象，没有 JSON 字符串、`JSON.parse` 或展开器。
  隔离测试 1000 台 decide 约 2.46 ms（DOF 32），每次调用分配约 23.8 KiB，
  低于 JSON 路径的约 49 KiB；
- **worker/IPC**：仍走紧凑 JSON（位置数组 + 字符串表），可信 harness 用
  `__seedfield_expand` 还原对象；跨进程不传 CLR 对象；
- **脚本契约**：两条路径都提供同一对象形状；`ObservationJsonTests` 用脚本
  `JSON.stringify(obs)` 与 `SerializeFullObservation` 做语义对比；
  worker 双跑哈希 `34886c1bb93164a1` 一致；Godot `--save-smoke` 通过；
- **真实 tick 稳态**：100 台 **1.18 ms**（约 845 ticks/s），1000 台
  **7.9 ms**（约 125 ticks/s）；1000 台峰值约 450 MiB、100 台约 145 MiB；
- 短窗口 1000 台 10 Tick 约 24–33 ms（JIT/GC 预热），CI 60 ms 门禁通过；
- `SerializeFullObservation` 保留对象形状，供测试/工具/调试；
- `--chunk-size`（分块）与预序列化实验均更慢，未采用。

### 8. 底层引擎与 CPU 实验结论

- **QuickJS 重写可行性**：1000 个 runtime 的单线程 decide 为 85.5 ms，Jint
  为 63.5 ms；QuickJS 在 8/16/32 线程下几乎不缩放（89.8 / 252 / 233 ms），
  不适合当前多引擎模型，未替换。
- **9950X 拓扑**：16 核 / 32 线程、SMT 相邻（0/1）、每核 L2 1 MiB、
  双 CCD 各 32 MiB L3、DDR5-6000 96 GiB、AVX-512 F/BW/DQ/CD/VBMI2。
- **缩放与 GC**：1000 台从 1 线程 97.6 ms 到 32 线程 38.9 ms（60 Tick，
  仅 2.5×）；分配率约 8.5 GB/s、约 27% 时间在 GC，瓶颈是分配/GC 与
  缓存局部性，不是核数或频率（全核约 122% 基频）。
- **未采用的硬件调优**：CCD/核绑定更慢；64B 伪共享填充无收益；关闭分层
  编译短期快、长期慢；`GCHeapCount`/`gen0size`/`gcConcurrent`/关闭 AVX-512
  均未在稳态胜出；大页因权限未验证。

## 验证

- 顺序与并行执行同一脚本化战斗 20 Tick：事件序列与
  `WorldStateHasher` 哈希完全一致。
- 批次输出顺序、单控制器故障隔离、并行观测与串行观测一致性均有测试。
- 单测通过注入确定性执行器验证索引回填与故障隔离；真实 `Parallel.For` 由
  Headless 基准覆盖，避免测试自身阻塞线程池。
- 342+ 测试通过；黄金场景哈希不变。

基准（Release，AMD Ryzen 9 9950X，32 逻辑核，默认 DOF = 逻辑核数）：

| 场景 | 前（ADR-0027） | 并行批次 | + Server GC/预编译 | + 紧凑观测 | + 进程内直接对象 |
|---|---:|---:|---:|---:|---:|
| 100 台 | 33.96 ms | 7.46 ms | 4.12 ms | 1.67 ms | **1.18 ms** |
| 1000 台 | 180.36 ms | 103.71 ms | 60.12 ms | 11.6 ms | **7.9 ms** |

100 台约 845 ticks/s，1000 台约 125 ticks/s；1000 台稳态峰值约 450 MiB、
100 台约 145 MiB；1000 台控制器启动约 127 ms / 50 MiB。
短窗口包含 JIT/PGO/GC 预热，门禁数字因此偏保守。

## 后果

- 控制器阶段会占用多个核心；默认 `Environment.ProcessorCount`，可用
  `--parallel-dof` 覆盖。CI 门禁用 100 台 `--budget-ms 60` 并断言
  `gcServer=True`，既防时间回退也防 Server GC 配置丢失。
- 脚本若依赖真实墙钟或跨设备共享可变全局，本来就不满足确定性契约；并行不改变
  这一点，也不提供跨设备共享状态。
- WorkerController 走独立进程/IPC，不进入此批处理；其隔离边界保持不变。
- Jint 的 `Timeout` 是墙钟安全阀，不是结算规则。并行负载下同一脚本可能更早触发
  超时；`MaxStatements` 仍是确定性的语句上限。超时只产生 Fault，不改变世界规则。
- 不要在已有的并行区域里再调用批处理；嵌套并行会放大线程争用。
- 若调用方本身是线程池线程，批处理与并行观测自动回退串行，避免
  `Parallel.For` 阻塞造成线程池饥饿（xUnit 在小型 CI runner 上最容易触发）。
  游戏主线程与 Headless 主线程不受影响。
- 性能门禁使用宽松阈值吸收 CI 共享 runner 噪声；开发机基线记录在证据文件。
- 进程内直接对象路径同时降低时间与内存（无 JSON 文本/展开器双对象图）；
  worker 仍用紧凑 JSON，跨进程边界不传 CLR 对象。
- 若目标是极低内存或调试，可保留 `SerializeFullObservation` 并走完整 JSON；
  当前默认是最快的直接对象路径。

## 回滚

回退 `ControllerBatchRunner`/Main 集成提交即可恢复到逐设备顺序执行；
Simulation、存档 schema 与脚本契约不变，无需迁移。
