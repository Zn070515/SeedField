# 证据：跨区材料物流（2026-11-16）

## 实现

- `TransferMaterialIntent(EntityId, TargetRegionId, Amount)`。
- 发货前校验：运输单位有 `Drive`、目标区域存在且不同、两区存在直接链路、源库存足够。
- 发货立即扣源区域库存；生成 `MaterialShipment`，`ArriveTick = Tick + Link.LatencyTicks`。
- 在途物资不可用于目标区制造；到达后写入目标区域本地库存。
- 事件：`MaterialShipped` / `MaterialArrived`；统计：`MaterialShippedTotal` / `MaterialDeliveredTotal`。
- 观察、存档、哈希均携带在途运输；加载顺序不影响状态哈希。

## 构建与测试

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 54 passed
SeedField.Runtime.Js.Tests: 13 passed
```

新增覆盖：

| 用例 | 断言 |
|---|---|
| 延迟到达 | 发货后源 60/目标 0；第 3 Tick 目标 40 |
| 无链路 | `NoRoute`，库存不变 |
| 库存不足 | `InsufficientMaterial` |
| 无运输模块 | `NoDrive` |
| 观察 | 己方在途 shipment 出现在 Observation |
| 存档 | 在途 shipment 保留，加载后按原 Tick 到达，哈希一致 |
| 区域顺序 | 存档 Regions 数组倒序加载后哈希仍一致 |

## Headless 演示

```powershell
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --logistics
```

```text
LOGISTICS shipped amount=40 arriveTick=3 source=60 target=0
LOGISTICS arrived tick=3 source=60 target=40 ok=True
```

## 未覆盖

- 多跳运输与中转仓储容量。
- 运输单位实体位置/油耗/护航。
- 跨区能源传输。
- 运输途中的拦截/损失。
