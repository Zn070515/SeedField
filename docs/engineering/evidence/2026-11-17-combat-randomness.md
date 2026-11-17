# 证据：确定性战斗随机、伤害变体与掉落

- 日期：2026-11-17
- 决策：ADR-0029
- 命令：`dotnet run --project src/SeedField.Headless -c Debug -- --combat-rng`

## 配置与抽取顺序

```text
hitChancePercent = 60
damageVariancePercent = 40
lootChancePercent = 100
lootMaterial = 5
seed = 2026
```

每次攻击的抽取顺序固定为：**命中 → 伤害变体 → 掉落**。0% 和 100% 均不消耗
随机值；默认 `100/0/0/0` 保持旧战斗行为与随机流。

## 演示输出

```text
COMBAT_RNG t=0 hit target=2 damage=9 energy=1
COMBAT_RNG t=1 hit target=2 damage=5 energy=1
COMBAT_RNG t=2 hit target=2 damage=9 energy=1
COMBAT_RNG t=3 hit target=2 damage=6 energy=1
COMBAT_RNG t=4 hit target=2 damage=4 energy=1
COMBAT_RNG t=5 miss target=2 energy=1
COMBAT_RNG t=6 hit target=2 damage=8 energy=1
COMBAT_RNG t=7 hit target=2 damage=7 energy=1
COMBAT_RNG summary hits=7 misses=1 health=52 state=0x454021de755d4d25
COMBAT_LOOT material=5 stock=5 looted=5
COMBAT_RNG saveHash=0x0b56c8166e6ec0a7 loadHash=0x0b56c8166e6ec0a7
```

- 未命中仍消耗 1E、进入冷却，并产生 `ShotMissed`；
- 命中的伤害在 7 × (100 ± 40)% 内向下取整，最小 1；
- 击杀掉落 5M 进入攻击方区域库存，`MaterialLootedTotal=5`；
- 存档往返哈希一致。

## 规则摘要

- 命中率 0..100，默认 100；伤害变体 0..100，默认 0；
- 掉落概率 0..100、掉落材料 ≥0，默认均 0；
- 未命中：`ShotsFired += 1`、`ShotsMissed += 1`、无伤害事件；
- 掉落只对击杀生效，且要求 `CombatLootMaterial > 0`；
- 伤害/掉落均为整数结算，随机值来自 `WorldState.Random`，不依赖墙钟。

## 测试

```text
dotnet test tests/SeedField.Simulation.Tests/SeedField.Simulation.Tests.csproj
```

- `CombatRandomnessTests`（9 个）：
  - 0% 命中不消耗随机值；
  - 100% 命中不抽取；
  - 伤害变体抽取一次且匹配按种子计算的期望值；
  - 100%/0% 掉落不抽取，正确发放/不发放；
  - 同种子同配置的事件序列与哈希一致；
  - 规则存档往返、v17 迁移、非法范围拒绝。
- 黄金场景哈希：`0x77e84c31f71f63ba`（默认配置下行为不变，仅新增状态字段）。
- 存档 schema：**v18**；v17 缺省为 `100/0/0/0`、统计 0。

## 未验证与风险

- 掉落是场景材料源，不是正式经济/掉落表；正式版需要上限与来源设计。
- 未命中仍消耗能量/冷却，脚本策略需要自行处理 `ShotMissed` 或状态变化。
- 概率/变体数值均为演示配置，不代表平衡结论。
