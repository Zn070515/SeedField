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

## 验证

- 顺序与并行执行同一脚本化战斗 20 Tick：事件序列与
  `WorldStateHasher` 哈希完全一致。
- 批次输出顺序、单控制器故障隔离、并行观测与串行观测一致性均有测试。
- 336+ 测试通过；黄金场景哈希不变。

基准（Release，AMD Ryzen 9 9950X，32 逻辑核，DOF 16）：

| 场景 | 前 | 后 | 提升 |
|---|---:|---:|---:|
| 100 台 | 33.96 ms | 7.46 ms | 4.6× |
| 1000 台 | 180.36 ms | 103.71 ms | 1.7× |

100 台约 134 ticks/s，1000 台约 9.6 ticks/s；1000 台峰值工作集约 253 MiB。

## 后果

- 控制器阶段会占用多个核心；默认 16 线程，可用 `--parallel-dof` 覆盖。
- 脚本若依赖真实墙钟或跨设备共享可变全局，本来就不满足确定性契约；并行不改变
  这一点，也不提供跨设备共享状态。
- WorkerController 走独立进程/IPC，不进入此批处理；其隔离边界保持不变。
- 性能门禁使用宽松阈值吸收 CI 共享 runner 噪声；开发机基线记录在证据文件。

## 回滚

回退 `ControllerBatchRunner`/Main 集成提交即可恢复到逐设备顺序执行；
Simulation、存档 schema 与脚本契约不变，无需迁移。
