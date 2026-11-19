# ADR-0051：Shield Generator 友军护盾投射与世界存档 v27

- 状态：**Accepted（2026-11-19）**
- 日期：2026-11-19
- 关联：ADR-0045（护盾/装甲）、ADR-0050（Repair Bay 与支援建筑）
- 取代：ADR-0050 待确认项 2（“护盾发生器是否升级为友军光环”）
- 证据：`docs/engineering/evidence/2026-11-19-shield-projector.md`

## 背景

ADR-0050 已把 `shield_generator` 蓝图接进工坊，但它只有 `ShieldModule`，只能
保护自己；玩家缺少一个在攻防中真正有用的支援建筑。护盾投射器需要一条可测试、
确定性的光环规则，并且不能破坏现有 `ShieldEnergy`/Regen 语义。

## 决策

1. **模块**
   ```csharp
   ShieldProjectorModule(
       int RangeMm,
       long RechargePerTick,
       long EnergyCostPerTarget)
   ```
   数值是场景配置，不是正式平衡。
2. **目标**
   - 同文明、同区域、范围内的友方实体；
   - 目标必须已有 `ShieldModule`，且 `ShieldEnergy < Shield.Capacity`；
   - 排除投射器自身；
   - 按 `EntityId` 升序稳定处理；
   - **绕过**目标自己的 `ShieldRegenReadyTick`：投射器可以在目标刚受击后立即补盾。
3. **成本**
   - 每成功充能一个目标扣投射器本地能量 `EnergyCostPerTarget`；
   - 能量不足时停止处理后续目标，已处理的目标保留；
   - 没有目标时不扣能量。
4. **结果**
   - 新事件 `ShieldProjected(ProjectorId, TargetId, Amount, TargetShield, EnergySpent)`；
   - 新统计 `ShieldProjectedTotal`；
   - `ObservedEntity` 新增 `HasShieldProjector` / `ShieldProjectorRangeMm` /
     `ShieldProjectorRechargePerTick`；
   - TypeScript 控制器契约同步。
5. **存档**
   - `ShieldProjectorSaveData` 进入实体、蓝图与统计；
   - 世界存档 schema **v26 → v27**；旧档缺字段按 `null` 加载；
   - 哈希与 golden hash 同步更新。
6. **蓝图**
   - `shield_generator` 增加
     `ShieldProjector(Range 2500, Recharge 3, 1E/target)`；
   - 仍保留自护盾 `ShieldModule(60, 3, 4, 1E)`、Power 与 Armor。

## 非目标

- 不给没有 `ShieldModule` 的单位凭空加盾。
- 不做跨区域投射、伤害拦截/转移或护盾共享池。
- 不做护盾光环的视觉特效；当前只发事件与状态。
- 不改变 `RegenerateShields` 的自身再生语义。
- 不把演示数值写成正式平衡。

## 后果

- `shield_generator` 从“自保建筑”变为真正的支援建筑：能在攻防中给附近的
  `strike`/`beam` 等带盾单位连续补盾。
- 新增一个被动系统，Tick 顺序固定在 `RepairEntities` 之后、`ProduceMaterials`
  之前。
- 存档 v27、哈希、观察 JSON、TypeScript 契约、展示过滤/日志同步扩展。
- 处理顺序按 EntityId，双跑/回放哈希稳定。

## 验证

- `dotnet test tests/SeedField.Simulation.Tests --filter "FullyQualifiedName~ShieldProjectorTests"`
  → **12 passed**（友军/无盾/敌方/越界/满盾/多目标/能量耗尽/Critical/观察/存档/哈希/蓝图）。
- `dotnet test SeedField.slnx` → **526 passed / 0 failed**
  （仿真 372 · Runtime.Js 124 · 仓库门禁 22 · Catalog 8）。
- `PresentationCoverageTests` 要求 `ShieldProjected` 有 filter/日志/Describe 分支，已补齐。
- Golden scenario hash 更新为 v27 值。

## 回滚

- 删除 `ShieldProjectorModule`、`ShieldProjected`、相关统计/观察/契约/蓝图字段与
  `SimulationEngine.ShieldProjector.cs`，把 schema 退回 v26。
- v27 存档在旧代码中不可读；回滚前保留 v27 构建或导出。
- `shield_generator` 会退回 ADR-0050 的自护盾版本。

## 待确认 [OPEN]

1. 正式数值：范围、每目标充能、每目标能量成本、是否允许多个投射器叠加。
2. 是否允许投射器给自身补盾（当前排除，避免与自身 Regen 双算）。
3. 是否改为“按总预算分配”而不是“按 EntityId 逐个扣费”。
4. 护盾光环的视觉表现与主客户端 topdown 映射。
