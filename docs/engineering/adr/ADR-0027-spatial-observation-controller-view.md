# ADR-0027：空间索引、共享观测与控制器观测视图

- 状态：**Accepted**
- 日期：2026-11-17
- 关联决策：PERF-01、PERF-02、SEC-01
- 前置：ADR-0005、ADR-0023
- 编号说明：ADR-0024–0026 属于未合并的 Gate 3 PR #5；本性能分支独立使用 0027

## 背景

`MAIN_JINT_BENCH`/Headless 负载显示：100 台真实 Jint 设备约 200 ms/tick，
约 5 ticks/s；渲染侧 100 台仍有 360–390 FPS。瓶颈在仿真观测构建、JSON 序列化
和 Jint 执行，不在 GPU。

旧 `ObservationBuilder.BuildForEntity` 对每个设备扫描整个区域，复杂度约 O(n²)，
且每台设备重复生成自己的 `ObservedEntity` DTO。

## 决定

### 1. 每 Tick 空间索引

新增 `RegionSpatialIndex`：

- 2,000 mm 均匀网格；
- 每 Tick 构建一次，桶内按 EntityId 排序；
- 传感器查询只访问半径覆盖的格子。

### 2. 每 Tick 观测缓存

新增 `ObservationCache`：

- 按 World.Tick 构建/复用；
- 缓存区域索引、己方 `ObservedEntity` 列表和每实体 DTO；
- `SimulationEngine.BuildDeviceObservation` 使用该缓存。

### 3. 控制器观测视图

新增 `SimulationEngine.BuildControllerObservation`：

- 只包含 **viewer 自己 + 本设备传感器感知的敌方**；
- 不再把同文明其他设备塞进每个控制器的 JSON；
- `BuildDeviceObservation` 保持原契约（己方全部可见）供测试/工具使用；
- Main 的 `DeviceControllerSlot` 改用 `BuildControllerObservation`；
- 这是刻意的设备作用域视图，不改变仿真规则，也不改变文明级 Observation。

### 4. 不强行引入的算法

- Bitset：空间索引 + 共享 DTO 后，可见性查询不再是主要成本；没有新增位集热点。
- DSU：`PowerGridTopology` 已用确定性 BFS 构建区域连通分量，区域图很小。
- A*/JPS：当前移动是直线，没有障碍物/Collision；等地形阻挡落地再评估。
- 最大流/最小费用流、匈牙利：当前没有分配瓶颈，先不增加复杂度。
- 线段树/树状数组/Fenwick：配额/TTL 仍是 O(1) Tick 算术。

## 验证

基准命令：

```powershell
dotnet run --project src/SeedField.Headless -c Debug --no-build -- --benchmark-observation 100
dotnet run --project src/SeedField.Headless -c Debug --no-build -- --benchmark-jint 100
dotnet run --project src/SeedField.Headless -c Debug --no-build -- --benchmark-serialize 100
```

结果见 `docs/engineering/evidence/2026-11-17-performance-spatial-observation.md`。
Golden/Headless hash 与优化前一致，测试全绿。

## 非目标

- 不改变 `WorldState`、存档 schema、Jint 安全边界、离线包。
- 不把 `BuildControllerObservation` 当作文明级情报接口；文明级仍用 `BuildObservation`。
- 不做跨进程/共享 Jint engine 的架构改造。

## 后果

- 100 台控制器从约 200 ms/tick 降到约 26 ms/tick（开发机 Debug，Headless）。
- 观测构建本身约 5.8× 提速；控制器 JSON 体积随“只含自机 + 感知敌人”显著下降。
- 控制器脚本不能依赖同文明其他设备的实体列表；需要协同信息时必须通过消息/文明级接口。
- 后续若引入更复杂传感器/障碍物，可在现有 `RegionSpatialIndex` 上扩展，不需要重写观测层。
