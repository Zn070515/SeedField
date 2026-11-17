# 证据：发电溢出、自动取电、潮流延迟/损耗与恢复储备（2026-11-17）

关联：ADR-0011、ADR-0010、E-01/E-02/E-03。

## 1. 发电溢出入网

- `GeneratePower` 先填满设备本地储能，多余发电进入本区域该文明的电网库存。
- `CatchUpPower` 在非活动区激活/保存前按同一规则补算。
- 新增 `EnergyOverflowedTotal`；`EnergyGeneratedTotal` 统计总发电。
- 实测：本地 9/10、发电 2 → 本地 10、电网 +1、总发电 2、溢出 1。

## 2. 自动取电与恢复储备

- 新增每文明 `PowerPolicy.ReserveEnergy`（场景配置，默认 0）。
- 移动/攻击/发信在本地不足时自动从本区域电网抽取差额；只能使用储备以上的部分。
- 显式 `DrawEnergyIntent` 同样受储备限制。
- 失败时 `PowerShortfall` 区分 `local` / `grid` / `reserve`。
- 新增 `EnergyAutoDrawn`、`EnergyAutoDrawnTotal`。
- 实测：电网 20、本地 0、移动耗 5 → 自动抽 5，电网剩 15；储备 10 时电网 12 无法支付 5。

## 3. 潮流延迟与损耗

- `RegionLink` 新增 `PowerLatencyTicks`（默认 0）与 `PowerLossPercent`（默认 0）。
- 延迟 > 0 时创建 `PowerShipment`：源区立即扣除，目标区 `ArriveTick` 到账；在途电力参与缺口估算。
- 损耗按路径逐跳整数向下取整；新增 `EnergyFlowLossTotal`，到账时结算。
- 实测：90E、延迟 2 Tick → 第一 Tick 源区 45、在途 45、目标 0；第二 Tick 目标 45。
- 实测：90E、10% 损耗 → 源区 45、目标 40、Flow 40、Loss 5。

## 4. 断供恢复策略

- 储备是取电下限：电网库存低于储备时停止设备取电，本地储能继续工作。
- 潮流/溢出让库存回到储备以上后自动恢复；不使用持久 blackout 锁存。
- 诊断新增储备、`IsLoadShed`、在途电力；F4 面板显示 `reserve` / `in` / `BLACKOUT` / `SHED`。
- 截图：`game/SeedField.Game/screenshots/m0-power.png`。

## 5. 存档

- 世界存档 schema v9 → v10：
  - 链路 `PowerLatencyTicks` / `PowerLossPercent`；
  - `PowerPolicies`、`PowerShipments`、`NextPowerShipmentId`；
  - 统计 `EnergyOverflowedTotal`、`EnergyFlowLossTotal`、`EnergyAutoDrawnTotal`。
- v9 旧存档默认延迟/损耗 0、储备 0、无在途电力。

## 6. 验证

```powershell
dotnet build SeedField.slnx
dotnet build game/SeedField.Game/SeedField.Game.csproj
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --power-recovery
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --power-grid
godot --path game/SeedField.Game -- --capture-power
godot --path game/SeedField.Game -- --save-smoke
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Simulation.Tests: 168 passed
SeedField.Runtime.Js.Tests: 48 passed
```

```text
POWER_RECOVERY overflow=True autoDraw=True reserveShed=True latency=True latencyArrived=True lossArrived=True ok=True
POWER_GRID balanced=True flowed=60 grid=90 regions=3 components=1 blackout=True shortfall=True ok=True
```

```text
Run 1 final hash: 0e53707713539498
Run 2 final hash: 0e53707713539498
REPRODUCIBLE: identical state hash across both runs.
```

```text
SAVE_SMOKE ok=True before=21720584640c65c9 mutated=dfae4ebd87dc3f82 after=21720584640c65c9 tick=39 A=v1 B=v1
```

## 7. 未覆盖

- 区域电网容量上限；溢出会持续累积。
- 按设备/蓝图优先级的负载切除。
- 持久 blackout 锁存/人工合闸。
- 潮流绕开高损耗链路、电压/频率/无功模型。
- 大电网性能基准；当前算法按分量 BFS + 路径移动。
- 损耗会导致后续补偿性发货，直到库存重新平衡；未做“一次发足净额”的逆损耗计算。
