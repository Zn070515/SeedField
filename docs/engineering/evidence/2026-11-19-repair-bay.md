# 2026-11-19 Repair Bay 被动维修证据

- 分支：`art/workflow-v0`
- ADR：[ADR-0050](../adr/ADR-0050-repair-bay-passive-repair.md)
- 状态：Accepted；数值为场景配置，不是正式平衡。

## 1. 规则

```text
每 Tick：
  对每个有 RepairModule 且未处于 Critical 的实体：
    找同文明、同区域、Health < MaxHealth、范围内最近的友方实体（排除自身）
    等距按 EntityId 升序
    维修 min(HealthPerTick, MaxHealth - Health)
    扣本地 E + 区域 M；不足则整次跳过
    发出 EntityRepaired；累计 HealthRepairedTotal
```

Tick 顺序：`RegenerateShields` → `RepairEntities` → `ProduceMaterials` → intents。

## 2. 实测

```text
dotnet test tests/SeedField.Simulation.Tests \
  --filter "FullyQualifiedName~RepairModuleTests"
  → 14 passed / 0 failed

dotnet test SeedField.slnx
  → 514 passed / 0 failed
    仿真 360 · Runtime.Js 124 · 仓库门禁 22 · Catalog 8
```

覆盖：

- 最近目标、越界、满血、敌方单位；
- 区域材料消耗与材料不足；
- 本地能量不足；
- 维修者 Critical 停止；
- 等距按 EntityId 决胜；
- 过量治疗截断；
- Observation 暴露 `hasRepair` / `repairRangeMm` / `repairHealthPerTick`；
- 存档往返与哈希一致；
- `DeviceBlueprint` 创建 `RepairModule`。

## 3. Schema / 契约

- 世界存档 **v26**：实体、蓝图、统计新增 Repair 字段；v25 旧档缺字段按 `null`。
- 新事件 `EntityRepaired`；展示过滤、日志、Describe 均已接入。
- `contracts/seedfield-controller.d.ts` 新增三个观察字段。
- Golden scenario hash 更新为 v26 值。

## 4. 蓝图

`Scenarios.cs`：

| Blueprint | 内容 |
|---|---|
| `repair_bay` | Power 2/tick + Armor 2 + Repair(2500, 5, 2E, 1M)；80M / 4 Tick |
| `barricade` | 240 HP + Armor 6；25M / 1 Tick |
| `shield_generator` | Power 2/tick + Armor 2 + Shield(60,3,4,1E)；90M / 4 Tick |

## 5. 未覆盖

- 正式平衡数值与最低配置性能；
- 跨区域维修、维修残骸/核心、维修敌方；
- 护盾发生器的友军光环；
- 主客户端 topdown 视觉映射（当前仍由 `MindustryStyle` 样片展示）。
