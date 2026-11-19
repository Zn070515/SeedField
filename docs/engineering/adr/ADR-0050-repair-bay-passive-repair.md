# ADR-0050：Repair Bay 被动维修模块与世界存档 v26

- 状态：**Accepted（2026-11-19）**
- 日期：2026-11-19
- 关联：ADR-0045（装甲/护盾）、ADR-0046（生产链）、ADR-0049（测试门禁）
- 证据：`docs/engineering/evidence/2026-11-19-repair-bay.md`

## 背景

新资产包已经有 `waste_repair_bay`，但仿真里没有任何建筑能维修其他单位；现有
`ShieldModule`、`SmelterModule`、`FabricatorModule` 都是“设备自用”或“区域生产”，
缺少一个支援型建筑。`repair_bay` 需要一条真实、可测试的仿真规则，而不是只做
一张可部署的外观。

## 决策

1. **模块**
   ```csharp
   RepairModule(
       int RangeMm,
       int HealthPerTick,
       long EnergyCostPerTick,
       long MaterialPerTick = 0)
   ```
   数值是场景配置，不是正式平衡。
2. **目标选择**
   - 只维修**同文明、同区域**的友方实体；
   - 排除维修者自身；
   - 只选择 `Health < MaxHealth` 的目标；
   - 选择范围内**最近**的目标；等距时按 `EntityId` 升序稳定决胜；
   - 维修者 `IsCritical`（≤25% 生命）时停止工作。
3. **成本与结算**
   - 能量从维修者本地库存扣；材料从区域库存扣；
   - 只有实际发生维修时才扣费；
   - 维修量 `min(HealthPerTick, MaxHealth - Health)`；
   - 材料不足或能量不足时整次跳过，不产生部分效果。
4. **可观察结果**
   - 新事件 `EntityRepaired(RepairerId, TargetId, Amount, TargetHealth, EnergySpent, MaterialSpent)`；
   - 新统计 `HealthRepairedTotal`；
   - `ObservedEntity` 新增 `HasRepair` / `RepairRangeMm` / `RepairHealthPerTick`；
   - TypeScript 控制器契约同步。
5. **存档**
   - `RepairSaveData` 进入实体、蓝图与统计；
   - 世界存档 schema 从 **v25 → v26**；旧档缺少字段时按 `null` 加载；
   - 状态哈希与 golden hash 同步更新。
6. **蓝图**
   - 新增 `repair_bay`：Power + Armor + Repair，演示值为
     `Range 2500 / 5 HP / 2E / 1M`，成本 80M、4 Tick；
   - 同时登记 `barricade`（高血 + 装甲）与 `shield_generator`（自护盾）蓝图；
   - 三者都仍是 `style-test-not-final` 场景配置。

## 非目标

- 不做跨区域维修、维修残骸/核心或维修敌方单位。
- 不做“护盾投射器”光环；`shield_generator` 目前只保护自身。
- 不把演示数值写成正式平衡。
- 不改变 Tick 频率、Intent 语义或第三方包安全边界。
- 不要求主客户端立即切到 topdown 美术；视觉样片仍由 `MindustryStyle` 承担。

## 后果

- 支援型建筑成为真实玩法：维修坞会持续消耗 E/M，优先修最近的受损友军。
- 新增一个被动系统，Tick 顺序固定在 `RegenerateShields` 之后、`ProduceMaterials` 之前。
- 存档 v26、哈希、观察 JSON、TypeScript 契约、展示过滤/日志同步扩展。
- 维修目标选择是确定性的，回放/双跑哈希不受字典顺序影响。
- `repair_bay` 蓝图进入工坊；主客户端视觉映射到 topdown 资产仍是后续工作。

## 验证

- `dotnet test tests/SeedField.Simulation.Tests --filter "FullyQualifiedName~RepairModuleTests"`
  → **14 passed**（最近目标、越界、满血、敌方、材料、能量、临界、等距决胜、
  过量治疗、观察、存档、哈希、蓝图）。
- `dotnet test SeedField.slnx` → **514 passed / 0 failed**
  （仿真 360 · Runtime.Js 124 · 仓库门禁 22 · Catalog 8）。
- Golden scenario hash 更新为 v26 值；`PresentationCoverageTests` 要求
  `EntityRepaired` 有 filter/日志/Describe 分支，已补齐。
- `ArtAssetTests` 仍通过；四件新资产在 manifest 中。

## 回滚

- 删除 `RepairModule`、`EntityRepaired`、相关统计/观察/契约/蓝图字段与
  `SimulationEngine.Repair.cs`，把 schema 退回 v25。
- 已有 v26 存档在旧代码中不可读；回滚前先导出或保留 v26 构建。
- 新资产 PNG/manifest 可保留；它们不依赖维修逻辑。

## 待确认 [OPEN]

1. 维修坞的正式数值（范围/速率/材料成本）与是否允许自修。
2. 护盾发生器是否升级为“向友军投射护盾”的光环模块。
3. 主客户端是否切换到 topdown 资产，以及 `repair_bay` 的视觉映射。
4. 维修是否应受 `ProductionEnabled` 或新的 `RepairEnabled` 开关控制。
