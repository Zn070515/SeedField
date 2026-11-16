# 证据：跨区能源与区域电网（2026-11-16）

## 实现

- `RegionStock.Energy`：每个区域、每个文明独立电网库存。
- `SendEnergyIntent(entityId, amount, targetRegionId?)`：
  - 需要 `PowerModule`；目标省略或同区时立即存入本地电网；
  - 远端目标经链路路径运输，`ArriveTick = Tick + 路径延迟和`；
  - `RegionLink.EnergyPerTick` 可限制每 Tick 过境批次。
- `DrawEnergyIntent(entityId, amount)`：从本区电网抽取到实体本地存储，受本地容量上限约束。
- 事件：`EnergyStored` / `EnergyShipped` / `EnergyArrived` / `EnergyDrawn`。
- 统计：`EnergyStoredTotal` / `EnergyShippedTotal` / `EnergyDeliveredTotal` / `EnergyDrawnTotal`。
- 电网库存、能源在途、下一 ID 进入 Observation、存档与哈希。

## 构建与测试

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 62 passed
SeedField.Runtime.Js.Tests: 13 passed
```

新增覆盖：

| 用例 | 断言 |
|---|---|
| 本地存入/抽取 | 实体 100→60，电网 40；抽取 30 后实体 90、电网 10 |
| 远端运输 | 40 E，`arriveTick=3 route=l01`，到达前目标电网 0 |
| 电网不足 | `NoEnergy`，实体能量不变 |
| 无 Power 模块 | `NoPower` |
| 链路能源容量 | 每 Tick 1 批，第二单 `NoBandwidth` |
| 存档 | 电网库存 + 在途能源保留，加载后按原 Tick 到达，哈希一致 |

## Headless 演示

```powershell
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --energy
```

```text
ENERGY shipped amount=40 arriveTick=3 route=l01 sourceLocal=60 targetGrid=0
ENERGY drew targetLocal=25 targetGrid=15 sourceLocal=60 ok=True
```

## 未覆盖

- 自动电网潮流、断供传播与优先供电。
- 发电设备直接写入区域电网（当前仍需显式 SendEnergy）。
- 能源在途拦截/损耗。
- 电网容量上限。
