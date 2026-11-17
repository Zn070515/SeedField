# ADR-0010：自动电力潮流与断供诊断

- 状态：**Accepted（原型语义）**
- 日期：2026-11-17
- 关联决策：E-01、E-02、E-03、O-01
- 前置：ADR-0004（区域活动）

## 背景

现有能源是“显式配送”：设备本地发电/储能，`SendEnergyIntent` 手动存入区域电网，`DrawEnergyIntent` 手动取回；链路 `EnergyPerTick` 只限制显式能源运输的**批次数**。没有自动电网潮流，因此断供不会沿拓扑传播，也没有可解释的电网诊断。

路线图 M2 要求供电/储能/负载与断供成为真实限制。本 ADR 实现第一层：**电力链路 + 按连通分量自动均衡电网库存 + 诊断**。

## 决定

### 1. 电力链路

- `RegionLink` 新增 `PowerPerTick`（能量单位/Tick，默认 0）。
- `PowerPerTick = 0` 表示该链路不参与自动潮流；显式 `SendEnergyIntent` 仍使用 `EnergyPerTick` 批次数容量。
- 自动潮流与显式运输使用独立容量，互不扣减。
- 世界存档 schema v8 → v9；旧链路迁移默认 0。

### 2. 潮流规则

- 每个 Tick 在 `GeneratePower` 之后、处理意图之前运行；非活动区也参与（被动系统）。
- 按文明分别处理；连通分量由 `PowerPerTick > 0` 的链路构成，孤立区域是单点分量。
- 分量目标库存：
  - `target = total / count`，`remainder = total % count`；
  - 按当前库存升序（同库存按区域 ID）把 `target + 1` 分给前 `remainder` 个区域，其余为 `target`。
- 盈余区向缺口区做确定性 BFS 寻路；只经过本 Tick 仍有剩余容量的电力链路。
- 一次移动量 = `min(盈余, 缺口, 路径上最小剩余容量)`；沿路径同时扣减各链路容量并移动电网库存。
- 多个缺口按到盈余区的 BFS 距离升序处理（同距离按区域 ID），保证多跳时先填近端缺口。
- 链路容量在所有文明间共享；文明按 ID 升序处理，保证确定性。
- 只移动区域电网库存，不改变设备本地储能；不自动发电入网，也不自动向设备取电。
- 能量守恒：移动只做 `TryConsumeEnergy` + `AddEnergy`，不产生/销毁能量；`EnergyFlowedTotal` 记录移动量。

### 3. 诊断

- 新查询 `PowerGridDiagnostics.Build(world, civilizationId)`：
  - 每区域：电网库存、发电潜力（PowerModule.EnergyPerTick 之和）、本地能量/容量、连通区域、分量库存与区域数；
  - `IsBlackedOut = 分量库存 == 0 && 分量内存在 PowerModule 设备`。
- 停电只表示电网无库存；设备仍可依赖本地储能运行。
- 新事件：
  - `PowerFlowed(tick, civ, from, to, amount)`：按本 Tick 聚合；
  - `PowerShortfall(tick, civ, region, entity, requested, available, source)`：`source` 为 `local` 或 `grid`，在相关动作因 `NoEnergy` 失败时产生。
- 断供事件不持久化；诊断按当前世界状态派生。

### 4. 面板

- Godot `F4` 电网面板：区域列表、发电潜力、电网库存、本地储能、连通分量、停电标记与最近断供。
- 面板只读 `PowerGridDiagnostics`，不修改世界。
- `--capture-power` 截图；`--power-grid` Headless 演示多跳均衡与停电/断供。

## 非目标

- 发电自动溢出到区域电网。
- 设备自动从电网取电。
- 潮流延迟、损耗、交流潮流/电压、链路移除命令。
- 电池健康/老化。
- 观察/Jint 暴露完整电网状态（本批只给宿主诊断）。

## 后果

- 电网库存会沿电力链路自动均衡，断供会随拓扑/容量传播。
- 显式 `SendEnergyIntent` 仍是把本地发电送入电网的主要方式；M2 后续可评估自动溢出。
- 设备本地储能仍是最后一道缓冲；电网停电不等于设备立即停机。
- 共享链路容量是原型规则；如果未来改为按文明独立容量，需要新 ADR 与迁移。
