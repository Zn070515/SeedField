# ADR-0013：抢占式优先级取电与断路器维护锁定

- 状态：**Accepted（原型语义）**
- 日期：2026-11-17
- 关联决策：E-01、E-02、E-03、O-01
- 前置：ADR-0012

## 背景

ADR-0012 用取电下限（`PriorityReserveEnergy`）实现了“低优先级先被抑制”，但同一 Tick 内
意图仍按实体 ID 顺序处理：低优先级设备只要 ID 更小，就会先取走电网能量，高优先级设备
反而被饿死。阀门有下限，却没有真正的调度顺序。

断路器只有“开/关”，跳闸次数不保留。反复跳闸-合闸不会留下任何维护压力，也没有
“故障后必须检修”的入口。

## 决定

### 1. 抢占式优先级调度

- 每个 Tick 的意图按 `PowerModule.Priority` **降序**处理；同优先级按实体 ID、
  再按调用方输入顺序稳定排序。
- 没有 `PowerModule` 的实体优先级视为 0，因此默认配置下处理顺序与旧行为完全一致。
- 返回值 `IntentResults` 仍按调用方输入顺序排列；调度顺序变化不改变观察/意图契约。
- 效果：电网能量紧张时，高优先级设备先取电；低优先级设备看到的是已被高优先级
  消耗后的电网。取电下限（ADR-0012）继续生效，两者叠加。

### 2. 断路器跳闸计数与维护锁定

- `PowerGridState` 新增：
  - `TripCount`：自上次维护以来的故障跳闸次数；手动分闸不计数。
  - `MaintenanceRequired`：跳闸次数达到上限后置位。
- `PowerPolicy` 新增：
  - `BreakerTripLimit`（默认 0 = 不锁定）；
  - `BreakerMaintenanceMaterialCost`（默认 0）。
- 跳闸时 `TripCount++`；达到上限且尚未锁定时置 `MaintenanceRequired` 并发出
  `BreakerLockedOut`。
- 锁定期间：
  - 自动/显式取电继续禁止；
  - 自动合闸跳过（即使电网恢复到储备线以上）；
  - `CloseBreakerCommand` 返回 `BreakerMaintenanceRequired`。
- 新宿主命令 `MaintainBreakerCommand`：
  - 从区域库存扣除 `BreakerMaintenanceMaterialCost`；不足返回 `InsufficientMaterial`；
  - 清零 `TripCount`、清除 `MaintenanceRequired`；
  - **不合闸**：断路器仍保持打开，需显式合闸或等待自动合闸下一次条件满足；
  - 发出 `BreakerMaintained`，维护材料计入 `MaterialSpentTotal`。
- 维护不要求断路器处于打开状态；这是 `[PROPOSED]` 简化，正式版可再约束。

### 3. 诊断与表现

- `RegionPowerSnapshot` 增加 `BreakerTripCount`、`BreakerMaintenanceRequired`；
  `PowerGridDiagnostics` 增加 `BreakerTripLimit`、`BreakerMaintenanceMaterialCost`。
- F4 面板显示 `trips{n}` 与 `MAINT!`，并提供 `MAINTAIN` 按钮。
- Headless `--power-maintenance` 演示二次跳闸、锁定、维护与手动合闸。

## 存档

- schema v11 → v12：`PowerGridStateSaveData.TripCount` / `MaintenanceRequired`；
  `PowerPolicySaveData.BreakerTripLimit` / `BreakerMaintenanceMaterialCost`。
- 旧存档迁移：跳闸计数 0、未锁定、无跳闸上限、维护免费。
- 哈希纳入全部新字段；策略与断路器列表按稳定键排序后写入哈希。

## 非目标

- 随机故障概率；需要先有进入世界状态与哈希的 `DeterministicRandom`。
- 维护时长、远程维护、备件/工具链。
- 多电压等级、潮流方程、无功功率。
- 维护对电网拓扑的自动隔离/重构。

## 后果

- 优先级从“软下限”升级为“同 Tick 真实调度”，高优先级设备不会再被小 ID 抢占。
- 反复跳闸会累积维护压力；长期无人维护的电网最终需要宿主介入，而不是无限自动恢复。
- 默认 `BreakerTripLimit=0` 时行为与 ADR-0012 相同，现有场景无需改配置。
