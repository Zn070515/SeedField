# 证据：性能发行验收、GC 分档与超时边界

- 日期：2026-11-17
- 关联：审核 P1（GC 发行缺口、CI 门槛、墙钟超时）、ADR-0036、ADR-0028
- 机器：AMD Ryzen 9 9950X（16C/32T），Windows 11 Pro for Workstations

## 1. 长窗口基准（Release，Server GC）

命令：

```powershell
dotnet run --project src/SeedField.Headless -c Release -- --benchmark-jint-parallel 1000 --ticks 3000
dotnet run --project src/SeedField.Headless -c Release -- --benchmark-jint-parallel 100 --ticks 3000
```

| 设备 | avg | p50 | p95 | p99 | alloc/tick | gc0/gc1/gc2 | GC 暂停总/3000 | 峰值 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 1.24 ms | 0.97 | 3.31 | 4.21 | 4.13 MB | 282/113/4 | 207 ms | 164 MiB |
| 1000 | 7.55 ms | 5.83 | 11.53 | 22.77 | 50.85 MB | 1250/1231/5 | 3988 ms | 413 MiB |

- 1000 台 p99 约 22.8 ms，主要由 GC 暂停造成；仍远低于 6 Hz 的 167 ms 预算。
- 1000 台平均每 Tick 分配约 51 MB；这是后续 LOD/休眠/对象复用的主要目标。

## 2. 核心数 / GC 模式分档（1000 台，300 Tick）

| 核心 | GC | avg | p95 | p99 | 峰值 |
|---:|---|---:|---:|---:|---:|
| 4 | Server | 19.85 | 65.49 | 76.93 | 444 MiB |
| 4 | Workstation | 59.38 | 87.06 | 111.68 | 213 MiB |
| 6 | Server | 15.56 | 35.22 | 51.94 | 444 MiB |
| 6 | Workstation | 58.96 | 86.95 | 90.67 | 215 MiB |
| 8 | Server | 14.07 | 33.93 | 50.65 | 452 MiB |
| 8 | Workstation | 56.48 | 86.24 | 92.68 | 223 MiB |

结论：

- Server GC 在 4/6/8 核上均约 3× 快于 Workstation，代价是内存；
- 4 核 Server 的 p99 约 77 ms，仍在 6 Hz 预算内；60 Hz 不适合 1000 台；
- 正式客户端必须显式启用 Server GC，否则会落到 Workstation 档。

## 3. Godot 发行 GC 缺口

- Godot 宿主忽略项目 runtimeconfig；`Godot` 必须以 `DOTNET_gcServer=1` 启动；
- `Main._Ready` 现在打印 `gcServer`，Workstation 时 `GD.PushWarning`；
- `tools/run-game.ps1` 为开发/验收启动设置该环境变量；
- `godot-smoke.yml` 新增 “Verify GC mode switch”：无环境变量断言
  `gcServer=False`，设置后断言 `gcServer=True`；
- 正式导出/Steam 启动项仍需在发行验收中设置该环境变量（Issue #14）。

## 4. 性能门禁分层

- CI 保留宽松 `--budget-ms 60`（共享 Runner 噪声）；
- 固定机/开发机使用 `docs/engineering/evidence/perf-baseline.json` +
  `tools/check_perf.py`：
  - 长窗口（3000 Tick）；
  - 对 `avgTickMs` 与 `p95Ms` 与基线比较，默认允许 +25%；
  - 失败输出 `PERF_REGRESSION`；
- 基线 JSON 随版本更新，比较命令记录在 README。

## 5. 墙钟超时确定性边界

- 语句/内存限制：可复现（顺序/并行测试）；
- `SCRIPT_TIMEOUT`：宿主安全阀，受负载/调度/GC 影响，不作为游戏 C 预算；
- 回放记录 Intent + 世界哈希；重新执行脚本遇到超时需标记宿主非确定；
- 新增测试：
  - `StatementLimitFaultIsDeterministicAcrossExecutors`
  - `TimeoutFaultIsHostSafetyValve`

## 未验证与下一步

- 正式导出版本的实际 GC 模式与 Steam 启动项（Issue #14）。
- 4/6/8 核是 `DOTNET_PROCESSOR_COUNT` 模拟，不是真实物理机分档。
- 混合负载（Godot 渲染 + 100/500/1000 台 + 电网/通信/战斗）尚未统一基准。
- 跨进程 AppContainer 对手数量/IPC 开销尚未做规模化基准。
- 高负载下 Timeout 的频率与 Fault 一致性仍需要压力测试平台。
