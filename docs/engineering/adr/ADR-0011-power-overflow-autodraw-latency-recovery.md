# ADR-0011：发电溢出、自动取电、潮流延迟/损耗与恢复储备

- 状态：**Accepted（原型语义）**
- 日期：2026-11-17
- 关联决策：E-01、E-02、E-03、O-01
- 前置：ADR-0010
- 后续：ADR-0012（容量、优先级、净额补偿与断路器）

## 背景

ADR-0010 已实现电力链路与按连通分量的自动潮流，但发电仍只进设备本地储能，设备仍必须显式 `DrawEnergyIntent`，潮流即时且无损耗，也没有断供后的明确恢复策略。本轮补齐这四项，使 E 网络更接近 M2 的可优化系统。

## 决定

### 1. 发电溢出入网

- `GeneratePower` 先填充设备本地储能；本地已满时，多余发电进入该文明在本区域的电网库存。
- `EnergyGeneratedTotal` 统计总发电；新增 `EnergyOverflowedTotal` 统计进入电网的溢出量。
- 非活动区仍跳过逐 Tick 发电；`CatchUpPower` 在激活/保存前按相同规则补算（本地优先，剩余入电网）。
- 不新增逐 Tick 溢出事件；面板/诊断显示累计值。

### 2. 自动取电与恢复储备

- 新增每文明 `PowerPolicyState.ReserveEnergy`（场景配置，默认 0）。
- 移动、攻击、发信在本地能量不足时，自动从本区域电网抽取差额：
  - 只能抽取 `max(0, GridEnergy - ReserveEnergy)`；
  - 若本地 + 可用电网仍不足，则不抽取并拒绝动作；
  - 成功时产生 `EnergyAutoDrawn`，`EnergyAutoDrawnTotal` 累加。
- 显式 `DrawEnergyIntent` 同样不能把电网抽到储备以下；失败时 `PowerShortfall` 的 `source` 为 `reserve` 或 `grid`。
- `SendEnergyIntent` 不自动取电，避免电网→本地→电网空转。
- 贸易托管能源仍由宿主命令直接结算，不受自动取电影响。

### 3. 潮流延迟与损耗

- `RegionLink` 新增：
  - `PowerLatencyTicks = 0`：自动潮流路径延迟；0 表示当 Tick 到账，保持既有测试语义。
  - `PowerLossPercent = 0`：每跳损耗百分比（0..100）。
- 路径延迟 = 各链路 `PowerLatencyTicks` 之和。
  - 延迟为 0：保持即时移动。
  - 延迟 > 0：源区立即扣除，创建 `PowerShipment`，目标区在 `ArriveTick` 到账；在途能量不可用于消费，但参与缺口估算，避免重复发货。
- 损耗按路径逐跳整数向下取整：`delivered = delivered * (100 - loss) / 100`。
- 新增 `EnergyFlowLossTotal`；源区扣除全额，目标区收到净额。
- 事件：`PowerShipped`（发货）、`PowerArrived`（到账）、`PowerFlowed`（即时流量）。
- 在途电力进入存档、哈希与诊断。

### 4. 断供恢复策略

- 采用**电网储备 + 无状态负载抑制**：
  - `GridEnergy < ReserveEnergy` 时停止设备取电（本地储能仍可用）；
  - 潮流/溢出把库存推回储备以上后自动恢复；
  - 不引入持久 blackout 锁存，避免读档/激活分叉。
- 诊断新增 `ReserveEnergy`、`IsLoadShed`、在途电力。
- `PowerShortfall.source` 为 `local` / `grid` / `reserve`。

## 存档

- 世界存档 schema v9 → v10：
  - `PowerPolicies`（每文明储备）；
  - `InTransitPower` 与 `NextPowerShipmentId`；
  - 链路 `PowerLatencyTicks`、`PowerLossPercent`；
  - 统计 `EnergyOverflowedTotal`、`EnergyFlowLossTotal`、`EnergyAutoDrawnTotal`。
- 旧存档缺失字段默认：延迟/损耗 0、储备 0、无在途电力。

## 非目标

- 区域电网容量上限。
- 按设备/蓝图优先级的负载切除。
- 持久 blackout 锁存/人工合闸。
- 潮流绕开高损耗链路、电压/频率模型、无功功率。

## 后果

- 本地满电的发电机会自动维护区域电网，设备缺电时自动取电；脚本仍可使用显式 `DrawEnergyIntent` 做精细控制。
- 储备策略可以防止设备把区域电网榨干，给断供恢复留出缓冲；储备 0 时行为最接近旧版本。
- 延迟/损耗默认 0，旧场景不改变；启用后电网更像真实网络，在途电力需要诊断与存档。
- 区域电网库存仍无上限，溢出可能持续累积；这是后续容量模型的开放问题。
