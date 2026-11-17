# 证据：空间索引与控制器观测优化（2026-11-17）

关联：ADR-0027。分支 `perf/spatial-observation`，性能改动分支，不改变仿真规则。

## 1. 问题

100 台真实 Jint 设备在 Headless 中约 200 ms/tick（约 5 ticks/s），而渲染侧
100 台仍有 360–390 FPS。瓶颈在：

- 每台设备重扫区域实体（旧 `BuildForEntity` 约 O(n²)）；
- 每台设备重复生成自己的 `ObservedEntity` DTO；
- 每个控制器 JSON 包含全部同文明设备；
- Jint JSON.parse/脚本执行。

## 2. 改动

- `RegionSpatialIndex`：2,000 mm 均匀网格，每 Tick 构建一次，桶内按 EntityId 排序。
- `ObservationCache`：每 Tick 缓存区域索引、己方 DTO 列表、实体 DTO。
- `BuildControllerObservation`：控制器只收到“自己 + 本设备感知的敌方”，
  不再收到同文明其他设备；`BuildDeviceObservation` 完整视图保留给测试/工具。
- Main 的 `DeviceControllerSlot` 改用 compact controller view。

## 3. 观测构建前后

Headless Debug、20 Tick、每 Tick 对所有 A 设备构建观测：

| 设备 | 优化前 avg/obs | 优化后 avg/obs | 总耗时前 | 总耗时后 | 提升 |
|---:|---:|---:|---:|---:|---:|
| 10 | 0.008 ms | 0.005 ms | 1.63 ms | 0.92 ms | 1.8× |
| 50 | 0.047 ms | 0.010 ms | 46.56 ms | 10.35 ms | 4.5× |
| 100 | 0.089 ms | 0.015 ms | 178.37 ms | 30.69 ms | 5.8× |

## 4. 真实 Jint 前后

10 Tick、真实 `JsController`、每 Tick 所有 A 设备：

| 设备 | 优化前 ms/tick | 优化后 ms/tick | 优化前 ticks/s | 优化后 ticks/s |
|---:|---:|---:|---:|---:|
| 10 | 3.26 | 0.54 | 306.9 | 1854.3 |
| 50 | 87.56 | 8.41 | 11.4 | 118.9 |
| 100 | 200.72 | 26.24 | 5.0 | 38.1 |

100 台拆分（优化后）：

```text
obsMs=1.31  serMs=3.11  runMs=21.74  avgTickMs=26.24
```

Release 复测（100 台）：

```text
JINT_BENCH avgTickMs=21.42 ticksPerSec=46.7
obsMs=1.13 serMs=2.93 runMs=17.29
```

说明：优化后 Jint 数字使用真实 Main 的 compact controller view；旧数字使用
完整 device observation。对比的是实际游戏路径，而不是把两个不同 API 混在一起。

## 5. 命令

```powershell
dotnet run --project src/SeedField.Headless -c Debug --no-build -- --benchmark-observation 100
dotnet run --project src/SeedField.Headless -c Debug --no-build -- --benchmark-jint 100
dotnet run --project src/SeedField.Headless -c Debug --no-build -- --benchmark-serialize 100
dotnet test SeedField.slnx
```

## 6. 正确性

- `DeviceObservationTests` 新增 compact view 测试：自己 + 感知敌人，不含同文明盟友；
  完整 `BuildDeviceObservation` 仍包含盟友。
- 334 tests passed（18 repository / 231 simulation / 85 Runtime.Js）。
- Headless 双跑哈希：`34886c1bb93164a1`，与优化前一致。
- Golden scenario 未变。

## 7. 未采用的算法

- Bitset：空间索引 + 共享 DTO 后，可见性查询已不是主要成本；没有新增位集热点。
- DSU：`PowerGridTopology` 已用确定性 BFS 构建连通分量，区域图很小。
- A*/JPS：移动仍是直线，没有障碍物/Collision；等地形阻挡落地再做。
- 最大流/最小费用流、匈牙利：当前没有分配瓶颈。
- 线段树/Fenwick：配额/TTL 仍是 O(1) Tick 算术。

## 8. 风险

- 控制器观测不再包含同文明其他设备；依赖盟友列表的脚本需要改用消息/文明级接口。
- 这是 Debug 构建的开发机数据；Release 和不同硬件需复测。
- 空间索引为每 Tick 全量重建；区域实体数很大时可改为增量更新。
