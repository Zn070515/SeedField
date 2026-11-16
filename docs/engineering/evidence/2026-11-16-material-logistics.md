# 证据：跨区材料物流（2026-11-16）

## 实现

- `TransferMaterialIntent(EntityId, TargetRegionId, Amount)`。
- 发货前校验：运输单位有 `Drive`、目标区域存在且不同、存在链路路径、路径物资容量足够、源库存足够。
- 路由复用双向 BFS；发货立即扣源区域库存；`ArriveTick = Tick + 路径链路延迟之和`。
- 链路 `MaterialPerTick` 可限制每 Tick 过境批次。
- 在途物资不可用于目标区制造；到达后写入目标区域本地库存。
- 事件：`MaterialShipped` / `MaterialArrived`；统计：`MaterialShippedTotal` / `MaterialDeliveredTotal`。
- 观察、存档、哈希均携带在途运输；加载顺序不影响状态哈希。

## 构建与测试

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 56 passed
SeedField.Runtime.Js.Tests: 13 passed
```

新增覆盖：

| 用例 | 断言 |
|---|---|
| 直接延迟到达 | 发货后源 60/目标 0；第 3 Tick 目标 40，route `l01` |
| 多跳延迟 | 2t + 3t → 第 5 Tick 到达 r2，route `l01>l12` |
| 链路物资容量 | 每 Tick 1 批，第二单 `NoBandwidth` |
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
LOGISTICS shipped amount=40 arriveTick=5 route=l01>l12 source=60 target=0
LOGISTICS arrived tick=5 source=60 target=40 ok=True
```

## 未覆盖

- 中途仓储/装卸与换线。
- 运输单位实体位置/油耗/护航。
- 跨区能源传输。
- 运输途中的拦截/损失。
