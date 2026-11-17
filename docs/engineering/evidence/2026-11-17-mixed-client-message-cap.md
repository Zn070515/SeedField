# 证据：正式客户端混合负载与观测消息上限

- 日期：2026-11-17
- 机器：AMD Ryzen 9 9950X（16C/32T），Windows 11 Pro for Workstations
- 模式：Godot 4.7.2 windowed，1280×720，vsync off，Server GC，Release/Debug 构建
- 场景：真实 `program.scout.v1`（A 方 N 台）+ B 方实体 + 渲染 + 通信/战斗
- 测量：`--benchmark-mixed=N --benchmark-mixed-seconds=2`，跳过 1.5 s 预热，
  真实墙钟帧/ Tick 计时

## 修复前（完整收件箱进入每个观测）

| 设备 | avgTick | p95Tick | FPS | alloc/Tick | 脚本 Fault | 峰值 |
|---:|---:|---:|---:|---:|---:|---:|
| 100 | 6.90 ms | 11.02 | 150.6 | 58.5 MB | 0 | 447 MiB |
| 500 | 137.03 ms | 174.95 | 14.4 | 1087 MB | 24 `SCRIPT_TIMEOUT` | 927 MiB |
| 1000 | 309.32 ms | 372.63 | 5.3 | 2891 MB | 24 `SCRIPT_TIMEOUT` | 1002 MiB |

## 修复后（`ObservationLimits.MaxMessages = 32`）

| 设备 | avgTick | p95Tick | FPS | alloc/Tick | 脚本 Fault | 峰值 |
|---:|---:|---:|---:|---:|---:|---:|
| 100 | **1.91 ms** | 3.24 | 154.3 | **11.9 MB** | 0 | 376 MiB |
| 500 | **9.82 ms** | 15.48 | 62.2 | **42.0 MB** | 0 | 519 MiB |
| 1000 | **27.96 ms** | 34.38 | 28.4 | **92.1 MB** | 0 | 596 MiB |

## 对照：quiet 脚本（不发消息/不攻击）

| 设备 | avgTick | p95Tick | FPS | alloc/Tick | 峰值 |
|---:|---:|---:|---:|---:|---:|
| 500 | 3.12 ms | 8.68 | 64.9 | 27.8 MB | 452 MiB |
| 1000 | 5.22 ms | 6.68 | 32.7 | 64.5 MB | 498 MiB |

结论：

- 完整观测消息是 500/1000 台的主要放大器；上限后 1000 台从 309 ms 降到
  28 ms/Tick，且脚本超时消失。
- 1000 台混合负载仍在 6 Hz 的 167 ms 预算内（avg 28 ms，p95 34 ms），
  但渲染只有约 28 FPS；60 FPS 的呈现目标仍未达到。
- 500 台平均 10 ms/Tick、62 FPS；100 台 1.9 ms/Tick、154 FPS。
- 收件箱容量/过期、世界状态、存档与哈希未改；只限制观测中的最近 32 条。

## 剩余风险

- 32 条上限是固定常量，尚未做成场景/脚本可配置；
- 旧脚本若依赖更早消息会行为变化（ADR-0037）；
- 渲染 1000 台约 28 FPS，需要 LOD/批处理/呈现层优化；
- 正式 Windows 导出/Steam GC 与真实 4/6/8 核硬件仍未验收（Issue #14）。
