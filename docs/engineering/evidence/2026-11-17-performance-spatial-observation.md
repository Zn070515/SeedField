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
| 10 | 0.008 ms | 0.004 ms | 1.63 ms | 0.89 ms | 1.8× |
| 50 | 0.047 ms | 0.010 ms | 46.56 ms | 9.86 ms | 4.7× |
| 100 | 0.089 ms | 0.015 ms | 178.37 ms | 29.87 ms | 6.0× |

说明：表格中的“优化后”耗时已重新测量，计时区间只包含观测构建。旧的基准把
`engine.Step()` 也计入了同一 stopwatch，因此“优化前”列略偏高，6.0× 是上限估计。

## 4. 真实 Jint 前后

10 Tick、真实 `JsController`、每 Tick 所有 A 设备：

| 设备 | 优化前 ms/tick | 优化后 ms/tick | 优化前 ticks/s | 优化后 ticks/s |
|---:|---:|---:|---:|---:|
| 10 | 3.26 | 0.66 | 306.9 | 1505.5 |
| 50 | 87.56 | 11.50 | 11.4 | 87.0 |
| 100 | 200.72 | 31.10 | 5.0 | 32.2 |

100 台拆分（优化后）：

```text
obsMs=2.25  serMs=2.53  runMs=26.14  avgTickMs=31.10
```

同一 `PerfDeviceScript`、同一缓存下，100 台 full vs compact：

```text
full view:    avgTickMs=101.29  ticks/s=9.9   obs 2.56  ser 11.14  run 87.40
compact view: avgTickMs=33.04   ticks/s=30.3  obs 1.26  ser 2.28   run 29.32
```

compact view 自身约 3.1×；再加上旧实现没有观测缓存，旧 `ScriptA + full observation`
路径的 200.72 ms/tick 到 33.04 ms/tick 约 6.1×。两者都保留，避免把“缓存”和
“视图裁剪”混成一个数字。

Release 复测（100 台，当前设备作用域基准）：

```text
JINT_BENCH avgTickMs=33.42 ticksPerSec=29.9
obsMs=2.16 serMs=2.56 runMs=28.52
```

说明：优化后 Jint 数字使用真实 Main 的 compact controller view；旧数字使用
完整 device observation。旧基准对每个设备都复用文明级 `ScriptA`，在 compact
视图下除设备 1/2 外几乎不产生 intent，会让“优化后 runMs”偏低；现在基准改用
设备作用域的 `PerfDeviceScript`，每个设备都执行真实的感知/选择/移动工作，
因此上表的优化后数字比早先记录更高（100 台 26.24 → 31.10 ms/tick），但仍为
6.5× 提升。严格的前后对比仍受脚本工作量差异影响，只能作为量级参考。

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
- 新增宽传感器（`int.MaxValue`）跨格可见性测试，覆盖空间索引范围溢出。
- 新增同 Tick 部署后缓存刷新测试：`ExecuteDeploymentCommand` 不推进 Tick，
  观测缓存必须失效，否则新设备在同一 Tick 内不可见。
- 336 tests passed（18 repository / 233 simulation / 85 Runtime.Js）。
- Headless 双跑哈希：`34886c1bb93164a1`，与优化前一致。注意：默认 Headless
  场景使用文明级 `BuildObservation`，不经过设备观测/空间索引路径；该哈希只证明
  仿真规则未变，不覆盖本次新增缓存与索引。设备路径由上面的单元测试覆盖。
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
- 空间索引为每 Tick 全量重建；区域实体数很大时可改为增量更新。索引现按区域惰性
  构建，单区域观测不再为全世界建索引。
- 观测缓存以 Tick 为键；所有不推进 Tick 的主机命令（部署/恢复/外交/电力/废墟/
  遗迹/区域激活）都会显式失效缓存。绕过 `SimulationEngine` 直接调用
  `DeploymentService` 的宿主需自行保证不在同一 Tick 内复用观测。
