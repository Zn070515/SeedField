# ADR-0033：离屏控制器抑制与区域规模边界

- 状态：**Accepted**
- 日期：2026-11-17
- 关联决策：ADR-0004（区域活动）、ADR-0010（电力潮流）、ADR-0028（并行控制器）
- 前置：区域 active/inactive gating、发电补算

## 背景

多区域世界里，非活动区已经拒绝 Intent、跳过着色器/区域表现，并在激活时补算
发电。但宿主仍会为这些区域的设备构建 Observation 并调用 `decide`——这些
Intent 最终会被 `RegionInactive` 拒绝，纯属浪费 Jint 时间。

同时，“分区 Tick”不能简单地按区域跳 Tick：电力潮流、断路器、消息/物流、
核心再生是跨区域或每 Tick 耦合的；跳过会改变状态、事件和哈希。

## 决定

### 1. 离屏控制器抑制

- 新增 `SimulationEngine.IsEntityActive(EntityId)`：实体存在且所在区域 active。
- Main 的 `StepOnce` 只对 active 实体的控制器构建 Observation / 调用 `decide`；
  非活动区控制器不运行。
- 语义：非活动区的 Intent 本来就会被拒绝，因此**权威世界状态不受影响**；
  脚本内部状态/计时器在离屏期间暂停，激活后继续。IsRegionActive 已经是
  会话状态（不进世界哈希），此决定不新增世界状态。
- 测试：激活/停用切换时 `IsEntityActive` 正确；区域激活测试继续覆盖 Intent 拒绝。

### 2. 区域规模基准

`--region-scale`：

- 8 个区域 × 50 台设备，链路电力容量；
- 只激活 1 个区域 vs 全部 8 个，各 20 Tick；
- 输出 active-only / all-active 时间与比例，作为离屏规模证据。

实测（开发机）：active-only 1.19 ms/tick，all-active 5.04 ms/tick（4.25×）。
不是 8×，因为 `Step` 的潮流/断路器/物流等仍全局执行。

### 3. 不做的分区 Tick

- 不按区域跳过 `FlowPower`/`UpdatePowerBreakers`/消息/物流/核心再生：
  这些系统有跨区域路径、共享链路容量或每 Tick 状态机，跳过会改变事件与哈希。
- 发电已按 active 区域跳过并在激活时补算（ADR-0004/0011），这是当前唯一
  安全的时间切片。
- 完整的“每区域 Tick 预算/增量加载”需要逐系统 catch-up 语义与 ADR，另行评估。

## 非目标

- 不做多线程权威仿真；`Step` 仍是单线程、稳定顺序。
- 不做离屏被动系统降频或时间轮。
- 不改变 Observation/Intent 契约；控制器只是不被调用。
- 不做 10k 设备的生产配置/内存上限。

## 后果

- 离屏区域节省 Jint/Observation 成本；脚本计时器在离屏期间暂停，文档化。
- 被动系统仍全局运行；大规模多区域仍需后续分区 ADR。
- `IsEntityActive` 是宿主/表现层 API，不进入存档/哈希。

## 验证

- `RegionActivationTests.InactiveRegionEntitiesAreNotActiveForControllers`。
- `--region-scale` 输出 active/all 对比。
- 全量测试、Godot smoke、CI 通过。

## 回滚

Main 恢复为对所有设备控制器调用 `Decide`；删除 `IsEntityActive` 与
`--region-scale`。不影响存档/仿真状态。
