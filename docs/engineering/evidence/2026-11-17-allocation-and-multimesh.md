# 证据：混合客户端分配/GC 与 MultiMesh 批处理

- 日期：2026-11-17
- 机器：AMD Ryzen 9 9950X（16C/32T）
- 模式：Godot 4.7.2 windowed，1280×720，vsync off，Server GC
- 场景：真实 `program.scout.v1` + B 方 + 渲染 + 通信/战斗
- 命令：`--benchmark-mixed=1000 --benchmark-mixed-seconds=2`
- 关联：ADR-0039

## 优化链（1000 台）

| 阶段 | avgTick | FPS | Draw Calls | alloc/Tick | GC 暂停 |
|---|---:|---:|---:|---:|---:|
| ADR-0038 基线 | 10.24 ms | 372 | 722 | 88.6 MB | 104 ms/2 s |
| + MultiMesh 纹理分组 | 9.90 ms | 599 | 244 | 89.6 MB | 91 ms/2 s |
| + 共享观测缓存 | 8.69 ms | 607 | 244 | 77.0 MB | 71 ms/2 s |
| + 消息窗口免全量排序 | 8.65 ms | 605 | 244 | 76.6 MB | 67 ms/2 s |
| + 设备查询排除友军 | 7.65 ms | 603 | 244 | 46.8 MB | 61 ms/2 s |
| + Jint ThreadStatic 缓冲 | **7.07 ms** | **614** | 244 | **41.1 MB** | 61 ms/2 s |

## 最终分档（vsync off）

| 设备 | avgTick | p95Tick | FPS | Draw Calls | alloc/Tick | 峰值 |
|---:|---:|---:|---:|---:|---:|---:|
| 100（完整绘制） | 1.32 ms | 2.46 | 158 | 1169 | 10.3 MB | 373 MiB |
| 500（LOD+MultiMesh） | 4.99 ms | 14.29 | 684 | 244 | 22.6 MB | 487 MiB |
| 1000（LOD+MultiMesh） | 7.07 ms | 12.16 | 614 | 244 | 41.1 MB | 543 MiB |

## 60 FPS 预算（1000 台，3 s）

```text
MIXED_BENCH ... maxFps=60 fps=60.0 ticksPerSec=6.0
avgTickMs=7.22 p95TickMs=11.31
p95FrameMs=16.69 p99FrameMs=16.74
allocMBPerTick=32.22 gcPauseMs=64.2/3s drawCalls=244
```

## 分段（1000 台）

| 阶段 | 时间 | 分配/Tick |
|---|---:|---:|
| 观测 | 1.62 ms | 1.96 MB |
| 决策 | 2.85 ms | 15.78 MB |
| 结算 | 1.92 ms | 1.38 MB |
| 呈现 | 0.64 ms | 0.21 MB |

## 视觉验证

`--benchmark-mixed-capture` 输出 `game/SeedField.Game/screenshots/mixed-lod.png`：
1000 个实体可见，MultiMesh 批处理没有造成空白；背景/网络/UI 仍约 240
draw calls，实体分组约 4。

## 结论与剩余

- 分配从 88.6 → 41.1 MB/Tick（vsync off），GC 暂停 104 → 61 ms/2 s；
  60 FPS 上限下 32.2 MB/Tick、GC 64 ms/3 s。
- Draw Calls 722 → 244；实体批处理已压到个位数。
- 剩余最大分配是 Jint 决策对象（约 15.8 MB/Tick）与渲染/Godot 每帧
  （约 0.4 MB/帧）；纹理图集只能再省几条实体 draw calls，暂不做。
- 正式导出/Steam GC、真实低端硬件、10k Tick 稳定性仍未验收。
