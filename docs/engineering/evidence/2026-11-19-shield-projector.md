# 2026-11-19 Shield Generator 友军护盾投射证据

- 分支：`feat/repair-bay`
- ADR：[ADR-0051](../adr/ADR-0051-shield-generator-friendly-projector.md)
- 状态：Accepted；数值为场景配置，不是正式平衡。

## 1. 规则

```text
每 Tick：
  对每个有 ShieldProjector、未 Critical 的实体：
    遍历同文明、同区域、范围内、已有 ShieldModule 且未满盾的友军（排除自身）
    按 EntityId 升序：
      若能量不足 EnergyCostPerTarget → 停止
      充能 min(RechargePerTick, 缺口)
      扣 E；发 ShieldProjected；累计 ShieldProjectedTotal
```

Tick 顺序：`RegenerateShields` → `RepairEntities` → `ProjectShields` → `ProduceMaterials`。

## 2. 实测

```text
dotnet test tests/SeedField.Simulation.Tests \
  --filter "FullyQualifiedName~ShieldProjectorTests"
  → 12 passed / 0 failed

dotnet test SeedField.slnx
  → 526 passed / 0 failed
    仿真 372 · Runtime.Js 124 · 仓库门禁 22 · Catalog 8
```

覆盖：

- 友军带盾目标充能；
- 无盾友军、敌方、越界不处理；
- 满盾截断；
- 多目标按 EntityId 顺序、每目标扣费；
- 能量耗尽后停止；
- 投射器 Critical 停止；
- Observation 三字段；
- 实体/蓝图存档往返与哈希一致；
- 蓝图创建 `ShieldProjectorModule`。

## 3. Schema / 契约

- 世界存档 **v27**：实体、蓝图、统计新增 ShieldProjector 字段；v26 旧档按 `null`。
- 新事件 `ShieldProjected`；filter/日志/Describe 已接入。
- `contracts/seedfield-controller.d.ts` 新增三个观察字段。
- Golden scenario hash 更新为 v27 值。

## 4. 蓝图

`shield_generator`：

```text
Power 2/tick · Armor 2 · Shield(60,3,4,1E)
ShieldProjector(2500, 3, 1E/target)
Material 90 · Build 4 Tick
```

## 5. 未覆盖

- 正式平衡数值与多投射器叠加规则；
- 跨区域、伤害拦截/转移、无盾单位；
- 护盾光环视觉特效与主客户端 topdown 映射；
- 1000 实体下的性能基准（当前 O(投射器×实体)）。
