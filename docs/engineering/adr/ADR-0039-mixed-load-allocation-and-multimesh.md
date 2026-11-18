# ADR-0039：混合客户端分配/GC 优化与 MultiMesh 批处理

- 状态：**Accepted**
- 日期：2026-11-17
- 关联：ADR-0037（观测消息窗口）、ADR-0038（人群 LOD/投递缓存）、Issue #19
- 触发：1000 台混合负载约 88.6 MB/Tick 分配、约 104 ms/2 s GC 暂停、
  722 Draw Calls

## 背景

ADR-0038 后 1000 台混合基准约 10.2 ms/Tick、372 FPS，但每 Tick 仍分配约
88.6 MB，GC 暂停约 104 ms/2 s。分段统计显示：

- 观测约 19 MB/Tick：每个并行 worker 各建一份空间索引/ObservedEntity，
  且设备查询把同文明的 1000 台友军也塞进候选再过滤；
- 决策约 19 MB/Tick：Jint 观测对象与短生命周期值数组；
- 渲染每帧约 0.9 MB：LOD 仍是每个实体一条纹理绘制命令；
- 结算约 1.4 MB/Tick。

## 决定

### 1. 共享只读观测缓存

`ObservationCache.PrepareForControllers` 在每个 Tick 并行执行前预热：

- 每区域空间索引；
- 所有实体 × 所有文明的 `ObservedEntity`；
- 消息/运输/控制点/条约/提案/贸易报价窗口。

预热后缓存只读，所有 worker 共享同一份索引和 DTO，而不是每 worker 重建。
`BuildControllerObservations` 不再创建 per-worker cache。

### 2. 设备查询先排除本文明

`RegionSpatialIndex.Query` 增加 `excludeCivilizationId`；设备观测只收集外国
实体。密集 1000 台友军不再被每个传感器查询扫描后再丢弃。语义不变：
设备观测本来只加入 `!isSelf` 的 sensed 实体。

### 3. 消息窗口不全量排序

`ObservationBuilder.MessageWindow` 直接遍历收件箱两次（计数 + 取最后 N），
不再 `OrderBy().ToList()` 复制/排序最多 64k 条消息。窗口语义改为稳定的
投递顺序（ADR-0037 已同步）。

### 4. Jint 观测值裁剪缓冲

`ObservationJsValue` 用 `[ThreadStatic]` 的 `JsValue[]` 填充
`JsObject.Create` 的槽位。`JsObject.Create` 在创建时复制 span，因此同一
worker 可复用数组；Jint 对象本身仍每 Tick 创建。每个对象不再分配短命
数组。

### 5. LOD 使用 MultiMesh 按纹理批处理

人群 LOD 不再对每个实体调用 `DrawTextureRect`，而是：

- 按 `Texture2D` 分组（朝向/单位类型自然形成少量纹理）；
- 每组一个 `MultiMesh`（Transform2D、QuadMesh、`DrawMultimesh`）；
- 视口裁剪后更新实例变换。

1000 台 Draw Calls 从 722 降到约 244；其中约 240 是背景/网络/UI，
实体批处理只占数条。**暂不引入纹理图集**：现有纹理分组已把实体降到个位数
draw calls，atlas 需要自定义 shader/UV 且只能再省几条，收益不足。

### 6. 测量工具

- `MIXED_BENCH` 增加 `obs/decide/step/present` 的分配 MB/Tick；
- `--benchmark-mixed-max-fps=60` 用于真实 60 FPS GC 预算；
- `--benchmark-mixed-capture` 输出 LOD 截图供视觉检查。

## 非目标

- 不引入纹理图集/自定义 shader；不替换渲染器。
- 不改观测消息上限（仍 32），不改收件箱容量/过期。
- 不改脚本 Intent 语义、世界状态、存档或哈希。
- 不把共享缓存暴露给可变调用方；预热后必须保持只读。

## 后果

1000 台真实 scout 混合（vsync off，2 s）：

| 指标 | ADR-0038 | 本 ADR |
|---|---:|---:|
| avgTick | 10.24 ms | **7.07 ms** |
| p95Tick | 12.05 ms | 12.16 ms |
| FPS | 372 | **614** |
| Draw Calls | 722 | **244** |
| alloc/Tick | 88.6 MB | **41.1 MB** |
| GC 暂停/2 s | 104 ms | **61 ms** |

60 FPS 上限（3 s）：7.22 ms/Tick、p95 11.31 ms、32.2 MB/Tick、
GC 暂停 64 ms/3 s、p99 帧 16.74 ms。

- 共享缓存要求“预热后只读”；未来新增懒加载段必须加入预热。
- 设备查询的文明排除必须与可见性规则保持一致。
- 消息窗口是投递顺序而非 MessageId 排序；脚本契约需注明。
- Jint 观测对象仍占决策分配大头（约 15.9 MB/Tick），下一阶段可评估对象复用；
  渲染/背景仍有约 240 draw calls，atlas 收益低。

## 验证

- 257 Simulation / 102 Runtime.Js / 8 Catalog / 19 Repository，共 386 tests；
- `MIXED_BENCH` 100/500/1000 前后对照；
- `mixed-lod.png` 截图确认 MultiMesh 实际可见；
- `--benchmark-mixed-max-fps=60` 显示 60 FPS 帧预算内。

## 回滚

- 恢复 per-worker `ObservationCache`；
- 删除 `Query` 的文明排除；
- 恢复 `MessageWindow` 的 `OrderBy`；
- 删除 ThreadStatic 缓冲，改回 `new JsValue[]`；
- LOD 回退到 `DrawLod` 逐实体纹理。

世界/存档/哈希无需迁移。
