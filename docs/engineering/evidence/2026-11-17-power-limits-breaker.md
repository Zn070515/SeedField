# 证据：电网容量、优先级、净额补偿与断路器（2026-11-17）

关联：ADR-0012、ADR-0011、ADR-0010。

## 1. 电网容量

- 每区域/文明可配置 `GridCapacity`；缺省无限。
- 溢出发电只存到容量上限，多余计入 `EnergyWastedTotal`；不逐 Tick 发浪费事件，避免日志刷屏。
- 本地→电网存能容量不足返回 `GridFull`，不半存。
- 电力到账容量不足时存入可用部分，其余浪费（`source=arrival`）。
- 自动潮流目标库存按容量截断；满区域不再收货。
- 实测：容量 2、溢出 4 → 入网 2、浪费 2；满网存能返回 `GridFull`。

## 2. 优先级取电

- `PowerModule.Priority` 控制取电优先级。
- `PowerPolicy`：`HighPriorityThreshold`、`PriorityReserveEnergy`。
- 高优先级可降到 `ReserveEnergy`；低优先级需保留 `ReserveEnergy + PriorityReserveEnergy`。
- `PowerShortfall.source` 新增 `priority`。
- 实测：高优先级设备在电网 13、储备 10+5 下成功取 3；低优先级被拒并产生 priority shortfall。

## 3. 损耗净额补偿

- 潮流先按路径反向 `ceil` 计算毛额，再进行正向损耗。
- 100% 损耗链路不参与自动潮流。
- 实测：三区域、每跳 10% 损耗，r1 净缺口 34 → 一次发 38 毛额、到账 34；总 Flow 57、Loss 10。
- 源区盈余/链路容量不足时仍会在后续 Tick 补。

## 4. 持久断路器

- `PowerGridState { BreakerOpen, ManualHold }` 进入存档/哈希。
- 电网 ≤0 且有 PowerModule 设备 → 跳闸 `PowerBlackout`。
- 断路器打开时自动/显式取电全部禁止，`PowerShortfall.source=blackout`；本地储能仍可用。
- 自动合闸：`AutoReconnect` 且未 ManualHold、电网 >0 且 ≥ `ReserveEnergy` → `PowerRestored`。
- 手动 `OpenBreakerCommand` 设置 `ManualHold`；`CloseBreakerCommand` 要求电网 >0 并清除保持。
- F4 面板新增 `OPEN BREAKER` / `CLOSE BREAKER`；`--power-breaker-smoke` 验证面板接线。
- 实测：跳闸 → 电网 5 仍不自动合闸 → 电网 15 自动合闸 → 手动分闸保持 → 手动合闸。

## 5. 存档

- 世界存档 schema v10 → v11：`GridCapacities`、`PowerModule.Priority`、`PowerPolicy` 新字段、`PowerGridStates`、`EnergyWastedTotal`。
- 旧 v10 存档默认容量无限、优先级 0、断路器关闭。

## 6. 验证

```powershell
dotnet build SeedField.slnx
dotnet build game/SeedField.Game/SeedField.Game.csproj
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --power-limits
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --power-breaker
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --power-recovery
godot --path game/SeedField.Game -- --power-breaker-smoke
godot --path game/SeedField.Game -- --capture-power
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Simulation.Tests: 177 passed
SeedField.Runtime.Js.Tests: 48 passed
```

```text
POWER_LIMITS capacityWaste=True priority=True netCompensation=True ok=True
POWER_BREAKER tripped=True drawBlocked=True heldBelowReserve=True autoClosed=True manualHeld=True manualClosed=True ok=True
POWER_RECOVERY overflow=True autoDraw=True reserveShed=True latency=True latencyArrived=True lossArrived=True ok=True
POWER_BREAKER_SMOKE opened=True held=True closed=True ok=True
```

```text
Run 1 final hash: 7a2114e30b9bdd38
Run 2 final hash: 7a2114e30b9bdd38
REPRODUCIBLE: identical state hash across both runs.
```

```text
SAVE_SMOKE ok=True before=634397de339b4ec9 mutated=f3ccfdfb23b0b3bc after=634397de339b4ec9 tick=39 A=v1 B=v1
```

截图：`m0-power.png` 显示 `G0/500`、`res 10+20`、`pri≥5`、断路器按钮与 `BLACKOUT`。

## 7. 未覆盖

- 断路器维护成本/故障概率/远程延迟。
- 抢占式意图重排；优先级通过取电下限实现。
- 多电压等级/潮流方程/无功。
- 大电网压力测试；容量/优先级/断路器已通过小规模场景验证。
