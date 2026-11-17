# ADR-0012：电网容量、优先级取电、损耗净额补偿与断路器

- 状态：**Accepted（原型语义）**
- 日期：2026-11-17
- 关联决策：E-01、E-02、E-03、O-01
- 前置：ADR-0010、ADR-0011

## 背景

ADR-0011 已实现溢出入网、自动取电/储备、延迟/损耗与无状态负载抑制。仍缺：

- 区域电网库存没有容量上限，溢出会无限累积；
- 取电没有设备优先级；
- 损耗会导致到账不足后的补偿性小批次；
- 断供没有持久状态和“合闸”入口。

## 决定

### 1. 区域电网容量

- 每区域/每文明可配置 `GridCapacity`；缺省无限。
- 溢出发电：只加到容量上限，多余计入 `EnergyWastedTotal`；不逐 Tick 发浪费事件，避免日志刷屏。
- `SendEnergyIntent` 本地→电网：容量不足时整单拒绝 `GridFull`，不半存。
- 显式/自动电力运输到账：容量不足时存入可用部分，其余浪费（`source=arrival`）。
- 自动潮流：目标库存按容量截断；满区域不能再收货，但仍可作为盈余源。
- 诊断显示容量/空闲/满；存档 schema v11。

### 2. 优先级取电

- `PowerModule` 新增 `Priority`（默认 0）。
- `PowerPolicy` 新增：
  - `HighPriorityThreshold`（默认 0，所有设备高优先级）；
  - `PriorityReserveEnergy`（默认 0，低优先级设备额外保留）。
- 取电下限：
  - 高优先级可降到 `ReserveEnergy`；
  - 低优先级需保留 `ReserveEnergy + PriorityReserveEnergy`。
- 自动取电与显式 `DrawEnergyIntent` 都遵守；`PowerShortfall.source` 新增 `priority`。
- 默认值下行为不变。

### 3. 损耗净额补偿

- 潮流填缺口先算毛额：从净额反向逐跳 `gross = ceil(gross * 100 / (100-loss))`。
- 毛额受源区盈余与链路容量限制；正向损耗后到账为净额。
- 100% 损耗链路不参与自动潮流。
- 减少“到账不足 → 下 Tick 补偿”的抖动；容量/盈余不足时仍会后续补。
- `EnergyFlowedTotal` 计净到账，`EnergyFlowLossTotal` 计损耗。

### 4. 持久断路器

- `PowerGridState { BreakerOpen, ManualHold }` 按区域/文明进入存档与哈希。
- 跳闸：断路器闭合、区域电网 ≤0 且有本文明 PowerModule 设备 → 打开，`PowerBlackout`。
- 打开期间：自动/显式取电全部禁止；本地储能仍可用；潮流/溢出/到账仍可充能。
- 合闸：
  - 自动：`AutoReconnect`（默认 true）、未 `ManualHold`、电网 >0 且 ≥ `ReserveEnergy` → 合闸，`PowerRestored`；
  - 手动：宿主 `CloseBreakerCommand`（电网 >0）/ `OpenBreakerCommand`（设置 `ManualHold`）。
- F4 面板提供断路器开关与状态；Headless `--power-breaker` 演示。

## 存档

- schema v10 → v11：`GridCapacities`、`PowerModule.Priority`、`PowerPolicy` 新字段、`PowerGridStates`、`EnergyWastedTotal`。
- 旧存档迁移：容量无限、优先级 0、策略默认、断路器关闭。

## 非目标

- 断路器维护成本/故障概率/远程延迟。
- 抢占式意图重排；优先级通过取电下限实现。
- 多电压等级/潮流方程/无功。
- 自动扩容或蓝图化储能模块。

## 后果

- 电网库存有明确上限，溢出/到账浪费可解释。
- 低优先级设备在紧张时先被抑制，高优先级设备可继续使用备用电网库存。
- 损耗补偿减少抖动，但高损耗链路仍会降低总可用能量。
- 断路器使断供恢复成为持久状态：不会因为一次到账就立刻恢复；可以自动或手动合闸。
