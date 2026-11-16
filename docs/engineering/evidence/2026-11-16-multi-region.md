# 证据：多区域链路与跨区消息（2026-11-16）

## 实现

- `RegionLink`：双向链路，`LatencyTicks` 与可选 `MessagesPerTick`。
- 跨区发送验证：
  1. 目标区域必须存在；
  2. 目标区域必须有己方 `Comm` 端点；
  3. 必须存在链路路径；
  4. 路径上每条链路的本 Tick 用量不得超过容量。
- 路由：双向 BFS，每层只扩展较小的 frontier；同规模时正向优先，邻居按链路 ID 排序。
- 命中后把正向半段反转、再接上反向半段，得到真正的行进顺序；总延迟 = Comm + 路径链路延迟之和。
- `MessageEnvelope` / `DeliveredMessage` / Observation / 事件 / 存档均携带 `TargetRegionId`。
- 链路与在途消息进入状态哈希、存档与加载校验。

## 构建与测试

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 48 passed
SeedField.Runtime.Js.Tests: 13 passed
```

新增 `MultiRegionTests`：

| 用例 | 断言 |
|---|---|
| 直接链路 | Comm 1 + Link 3 → 第 4 Tick 投递，目标区域 r1 |
| 无链路 | `NoRoute`，不产生在途消息 |
| 目标区无己方 Comm 端点 | `NoRoute`，防止全图广播 |
| 两跳链路 | Comm 1 + 2 + 3 → 第 6 Tick 投递，路由 `l01>l12` |
| 反向两跳 | r2→r0 路由重建为 `l12>l01`，延迟 6 |
| 链路带宽 | 每 Tick 1 条，第二条 `NoBandwidth` |
| 存档往返 | 链路与在途消息保留，加载后按原 Tick 投递，哈希一致 |

## Headless 演示

```powershell
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --multi-region
```

```text
MULTI_REGION queued id=1 sentTick=0 deliverTick=6 target=r2 route=l01>l12
MULTI_REGION delivered id=1 tick=6 target=r2 ok=True
```

## 未覆盖

- 跨区物流/能源传输。
- 跨文明通信与情报权限。
- 区域加载/卸载与离屏推进。
- 链路经济成本/拥塞的非线性模型。
