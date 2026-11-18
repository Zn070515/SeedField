# ADR-0045：M4 战斗深度 v1——装甲、护盾、武器种类与模块损伤

- 状态：**Accepted**
- 日期：2026-11-18
- 关联决策：M4（战构体与攻防）、E-01（能量结算）
- 前置：确定性战斗随机（ADR-0029）、直接火力模型、世界存档 v20

## 背景

战斗此前只有“命中概率 → 伤害变体 → 掉落”，所有武器同质，耐久只有一条血条：
装甲、护盾、模块失效都没有玩法意义，装备差异只体现在数值上。M4 要求
“代码/设备设计决定胜负”，至少要让防御模块与武器类型改变结算方式。

## 决定

### 1. 装甲与武器种类

- 新增 `ArmorModule(FlatReduction)`：每次命中先按固定值减伤，但至少保留 1 点伤害。
- `WeaponModule` 新增 `WeaponKind`：`Direct`（默认，受装甲影响）/ `Beam`
  （完全忽略装甲）。
- 伤害顺序固定：武器类型 → 装甲 → 护盾 → 生命；未命中仍耗能、进冷却。
- 命中事件 `EntityDamaged` 增加 `ShieldAbsorbed`/`ArmorAbsorbed`；统计新增
  `ShieldAbsorbedTotal`/`ArmorAbsorbedTotal`。

### 2. 护盾

- 新增 `ShieldModule(Capacity, RegenPerTick, RegenDelayTicks, EnergyCostPerRegen)`。
- 部署/升级时护盾从满容量开始；升级保留当前值并夹到新容量。
- 命中先扣护盾；造成伤害后 `ShieldRegenReadyTick = tick + RegenDelayTicks`。
- 被动再生在每 Tick 电力结算后执行：只花**本地**能量，不自动从电网取电；
  容量满、未到延迟、或设备处于 `Damaged`（≤50% 生命）时停止。
- 统计 `ShieldRegeneratedTotal`；HUD 在生命条上方绘制青色护盾条。

### 3. 模块损伤

- `IsDamaged`：生命 ≤ 50%；护盾停止再生。
- `IsCritical`：生命 ≤ 25%；武器/传感器/驱动全部失效：
  - 移动/攻击返回 `ModuleDisabled`；
  - `VisibilityRules` 不再感知目标；
  - 设备观察中的 `hasWeapon/hasSensor/hasDrive` 变为 false。
- 只做“临界失效”与“护盾停再生”，不做逐模块命中位置/独立耐久。

### 4. 持久化与场景

- 世界存档 v21：实体/蓝图新增 `armor`、`shield`；实体新增 `shieldEnergy`、
  `shieldRegenReadyTick`；武器存档新增 `kind`。v20 及更早迁移为无装甲/护盾、
  Direct 武器。
- 哈希加入上述字段与三项统计。
- 黄金场景：A 方给装甲 3 + 护盾 25/2/4/1，B 方改用 Beam Frame；
  因此伤害结算行为变化，黄金哈希按新规则更新。
- Godot 日志：受伤事件显示 `(shield -N)` / `(armor -N)`。

## 非目标

- 不做护盾朝向/分区、模块独立耐久、命中部位、维修、过载、穿甲弹种。
- 不做爆炸/范围伤害、护盾恢复延迟以外的状态机。
- 不把装甲/护盾暴露给 Observation 脚本（脚本目前不能读取护盾值）；
  需要时另立契约变更。
- 不冻结数值：3/25/2/4/1 是场景演示，不是正式平衡。

## 后果

- 武器类型与防御模块产生真实差异：Beam 对高装甲目标更强，护盾吃爆发后需等待再生。
- 设备在低血量下会失去行动/感知能力，战斗有“打残”阶段而不只是击杀。
- 存档/哈希升级到 v21，旧档迁移为无防御；黄金哈希已更新。
- 新增字段增加观察/存档体积，但当前规模可接受。

## 验证

- `CombatModulesTests`：护盾先吸收、装甲减伤、Beam 忽略装甲、护盾延迟再生并耗能、
  ≤50%/≤25% 阈值、临界禁用武器/驱动/传感器、存档往返哈希。
- 全量测试与仓库门禁通过。
- Godot `--capture` 显示护盾条；`--save-smoke` 哈希一致。

## 回滚

删除装甲/护盾模块与相关存档字段、恢复旧伤害结算；v21 存档需要迁移回 v20
或保留字段但忽略。黄金哈希随行为变化回退。
