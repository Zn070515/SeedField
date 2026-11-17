# ADR-0014：确定性随机与断路器随机故障

- 状态：**Accepted（原型语义）**
- 日期：2026-11-17
- 关联决策：E-01、E-03、O-01
- 前置：ADR-0013

## 背景

ADR-0013 明确把“随机断路器故障”列为非目标，理由是缺少进入世界状态与哈希的
`DeterministicRandom`。固定 Tick 与可复现要求：任何随机结果必须由世界状态决定，
读档后继续同一条随机流，不能使用 `System.Random`、时间种子或线程本地状态。

## 决定

### 1. 世界内确定性随机流

- 新增 `DeterministicRandom`，算法为 SplitMix64：
  - `Seed` 记录场景种子；
  - `State` 是下一输出计数器，二者都是世界状态。
- `WorldState.Random` 唯一实例；`WorldBuilder.SetRandomSeed(ulong)` 设置种子。
- API：
  - `NextUInt64()`：返回 64 位输出并推进状态；
  - `NextInt(maxExclusive)`：`[0, maxExclusive)`；
  - `Percent(percent)`：`0..100` 整数概率；**0% 不消耗随机值**。
- 使用约束：
  - 调用顺序必须由确定性流程决定（排序后的实体/区域/事件）；
  - 不允许在 UI、渲染、Jint 控制器或诊断中推进世界随机流；
  - 新增消费者必须同时更新存档、哈希与回放测试。

### 2. 断路器随机故障

- `PowerPolicy.BreakerFaultChancePercent`（0..100，默认 0）。
- 每次故障跳闸（电网归零导致断路器打开）执行一次判定：
  - 概率为 0：不取随机数，行为与 ADR-0013 完全一致；
  - 概率 > 0：`Percent(chance)` 一次；命中则立即 `MaintenanceRequired=true`，
    不需要等到 `BreakerTripLimit`；
  - 未命中且达到 `BreakerTripLimit` 时仍按跳闸次数锁定。
- `BreakerLockedOut` 新增 `Reason`：
  - `"fault"`：随机故障；
  - `"trips"`：达到跳闸上限。
- 维护流程不变：`MaintainBreakerCommand` 清除锁定与计数，但不会自动合闸。

### 3. 存档与诊断

- schema v12 → v13：`RandomSeed`、`RandomState`、`BreakerFaultChancePercent`。
- 旧存档迁移：种子/状态 0（SplitMix64 从种子 0 开始）、故障概率 0。
- 哈希纳入种子、状态与故障概率；读档后继续同一随机流。
- F4 摘要显示 `fault X%`；`MAINT!` 与锁定原因进入事件日志。
- Headless `--power-fault` 演示 100% 故障锁定与存档往返。

## 非目标

- 随机战斗命中/伤害、掉落、AI 决策与地图生成。
- 加密安全随机或跨进程同步随机；本实现只服务于可复现模拟。
- 为随机流做版本化算法迁移；算法变更需新 ADR + 存档迁移。
- 维护时长、备件、远程维护。

## 后果

- 随机结果可被存档、哈希和回放完整复现。
- 默认种子 0 + 故障概率 0 时，现有场景不消耗随机值，行为与 v12 一致（哈希因新增状态字段而改变）。
- 新消费者若不按稳定顺序取数，或忘记同步存档/哈希，会造成读档分叉；需要专门的
  `DeterministicRandom` 回归测试。
