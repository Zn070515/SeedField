# ADR-0029：确定性战斗随机、伤害变体与掉落

- 状态：**Accepted**
- 日期：2026-11-17
- 关联决策：ADR-0014（确定性随机）、ADR-0017（残骸）、ADR-0023（黄金场景）
- 前置：SplitMix64 随机流、固定 Tick、整数结算

## 背景

战斗此前是固定命中、固定伤害（`WeaponModule.Damage`），没有随机命中、伤害
浮动或击杀掉落。ADR-0014 已经提供 `WorldState.Random`（SplitMix64），但只用于
断路器故障。需要一个不依赖墙钟、可回放、可由场景配置关闭的战斗随机层。

## 决定

### 1. 场景配置（不是正式平衡）

`WorldState` 新增四个场景规则字段，全部可关闭：

| 字段 | 范围 | 默认 | 含义 |
|---|---|---|---|
| `CombatHitChancePercent` | 0..100 | 100 | 命中概率 |
| `CombatDamageVariancePercent` | 0..100 | 0 | 伤害在 ±variance% 内浮动 |
| `CombatLootChancePercent` | 0..100 | 0 | 击杀后掉落概率 |
| `CombatLootMaterial` | ≥0 | 0 | 成功掉落时给攻击方文明的 M |

`WorldBuilder.SetCombatRandomness(...)` 校验范围。默认值
`100/0/0/0` 保持旧战斗行为不变；数值仍然是场景配置，不是平衡结论。

### 2. 随机抽取顺序

每次攻击按固定顺序抽取：

1. **命中**：`RollChance(hitChance)`；
2. **伤害变体**：只在命中且 `variance > 0` 时 `NextInt(2*variance+1)`；
3. **掉落**：只在目标被摧毁且 `lootMaterial > 0` 时 `RollChance(lootChance)`。

`RollChance` 规则：

- `percent <= 0`：返回 false，**不消耗**随机值；
- `percent >= 100`：返回 true，**不消耗**随机值；
- 其余：`World.Random.Percent(percent)`，消耗一次。

因此默认 `100/0/0/0` 场景的随机流与旧版完全一致；`Percent(0)` 的既有约定
不变（未修改 `DeterministicRandom.Percent` 本身）。

### 3. 命中/未命中语义

- 未命中仍然消耗武器能量、设置冷却、`ShotsFired += 1`；
- 未命中产生 `ShotMissed` 事件，`ShotsMissed += 1`，不产生 `ShotFired`/
  `EntityDamaged`；
- 命中才结算伤害；`ShotFired.Damage` 是实际应用伤害（不超过剩余生命）。

### 4. 伤害变体

```text
offset = NextInt(2*variance + 1) - variance
damage = max(1, floor(baseDamage * (100 + offset) / 100))
```

使用 `Int128` 中间值、向下取整；基础伤害为 0 时不浮动。伤害仍受目标剩余
生命截断，保持原有 `DamageDealt` 统计口径。

### 5. 击杀掉落

目标被摧毁后，若 `CombatLootMaterial > 0` 且 `RollChance(lootChance)` 成功：

- 把 `CombatLootMaterial` 加到**攻击方文明在目标区域的库存**（攻击与目标
  同区域，这是现有攻击规则）；
- 产生 `CombatLootGranted` 事件；
- `MaterialLootedTotal += amount`。

掉落与残骸相互独立：残骸仍按蓝图成本比例生成（ADR-0017）。默认
`CombatLootMaterial=0` 时不抽取、不发放。

### 6. 观察/表现

- `ShotMissed` 与 `ShotFired` 一样受 `PlayerEventFilter` 的攻击者+目标可见性
  约束；`CombatLootGranted` 在玩家文明或目标可见时显示；
- `Main` 的事件日志、事件颜色、`DrawTracers`（未命中画淡色弹道、不画命中环）
  已覆盖新事件；
- 未命中不触发命中闪红；掉落只在玩家 A 的击杀上追加 `+M` 特效。

### 7. 存档与哈希

- 世界存档 schema **v17 → v18**：新增四个规则字段和
  `ShotsMissed`/`MaterialLootedTotal` 统计；
- 旧 v17 存档缺省为 `100/0/0/0`、统计 0；
- 读档校验百分比 0..100、掉落材料非负；
- 新字段与统计进入 `WorldStateHasher`，黄金场景哈希更新为
  `0x77e84c31f71f63ba`（默认配置下行为不变，仅状态字段增加）。

## 非目标

- 不做暴击/穿甲/多段伤害/武器精度表/物品掉落表；这些需要独立设计。
- 不改变默认战斗数值，不把演示概率当正式平衡。
- 不引入真实墙钟、浮点结算或 `System.Random`。
- 不改 worker/IPC 或脚本观察契约。

## 后果

- 默认场景的随机流不变；显式开启随机后才消耗 `World.Random`。
- 未命中仍花费能量/冷却，脚本需要根据 `ShotMissed`/事件或自身状态调整策略。
- 掉落是场景配置的材料源；正式版需要经济/掉落表与上限设计。
- 黄金哈希与存档版本变化；旧存档可迁移，但历史证据中的旧哈希保持原值。

## 验证

- `tests/SeedField.Simulation.Tests/CombatRandomnessTests.cs`：
  - 0% 命中不消耗随机值、100% 命中不抽取；
  - 伤害变体抽取一次且等于按种子计算的期望值；
  - 100%/0% 掉落不抽取、正确发放/不发放；
  - 同种子同配置事件序列与哈希一致；
  - 存档往返、v17 迁移、非法范围拒绝。
- `--combat-rng` headless 演示：固定种子输出命中/未命中/伤害/掉落与
  存档往返哈希。
- 黄金场景、fuzz 与全量测试通过。

## 回滚

移除四个规则字段、`ShotMissed`/`CombatLootGranted` 事件与统计，恢复
`ProcessAttack` 的固定命中/固定伤害；存档 schema 回 v17，旧 v18 存档需要
丢弃新增字段。默认路径没有行为变化，回滚不影响既有存档的玩法。
