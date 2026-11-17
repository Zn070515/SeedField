# 证据：并行控制器批处理（2026-11-17）

- 分支：`perf/parallel-controller-batch`
- 基线：`main` @ `6719ab9`（含 ADR-0027）
- 机器：AMD Ryzen 9 9950X（16 核 / 32 逻辑核），Windows 11 Pro for Workstations
- 构建：`dotnet build SeedField.slnx -c Release`
- 运行：`dotnet run --project src/SeedField.Headless -c Release --no-build`

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

CI 使用 `--parallel-dof 8 --budget-ms 120`，阈值远高于开发机基线，吸收共享
runner 的噪声；开发机基线以上表为准。

## 6. 未验证与风险

- CI runner 的核数/负载与开发机不同；阈值只防明显回退，不是精确性能断言。
- 脚本若依赖同一设备跨 Tick 的内部状态，仍按每 Tick 调用，语义不变；但若依赖
  墙钟，则本来就不确定。
- WorkerController 的独立进程路径未纳入并行批处理。
- 1000 台峰值工作集约 253 MiB；更大规模需要重新评估引擎数量与观测缓存生命周期。
