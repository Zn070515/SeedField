# ADR-0040：用仿真事实验证五分支教程准入

- 状态：**Accepted**
- 日期：2026-11-18
- 关联决策：M1（五分支教程/构造挑战）、ADR-0002（M0 简化假设）、ADR-0003（原型经济语义）
- 前置：`WorkshopService.IsMinimumUsable` 只按蓝图覆盖判定

## 背景

M1 要求玩家完成五类**最低可用**结构后立即开放 PvE，且低效率/低精度的合法构造
也必须通过。现有 `IsMinimumUsable` 只看“每个分支至少有一份已发布蓝图”，这证明
玩家会填写模块字段，不证明能源真的发电、传感器真的看到目标、通信真的送达。

同时，教程进度不应改变权威世界规则：它不是 Tick 结算的一部分，也不应该因为
新增进度字段而改写所有世界哈希、回放与旧存档。

## 决定

### 1. 五步事实判定

新增 `SeedField.Simulation.TutorialProgress`，对指定文明逐 Tick 观察
`WorldState` 与 `TickResult.Events`，只在事实成立时记为完成：

| 步骤 | 最低功能事实 |
|---|---|
| Energy | 己方存活设备的 `PowerModule.EnergyPerTick > 0`，且本地 `Energy > 0` |
| Sensing | 己方存活传感器通过 `VisibilityRules.IsSensedBy` 看到外国实体 |
| Communication | 收到 `MessageDelivered(ToCivilizationId = 文明)` |
| State | 拥有 `StateModule` 的己方设备产生了移动/攻击/发信事件 |
| Execution | 己方存活设备产生 `EntityMoved` / `ShotFired` / `ShotMissed` |

- 效率、精度、构造复杂度都不参与判定。
- 事件必须能映射回本文明存活设备；已毁设备不补记。
- 首次完成 Tick 固定，重复观察不会改写。
- 五步全部完成即 `PveUnlocked = true`。

### 2. 进度不是世界状态

- `TutorialProgress` 只读世界与事件，不持有可变世界引用，不进入
  `WorldState`、世界哈希、回放或存档 schema。
- 完成 Tick 由调用方（Godot/Host）持有；当前客户端在重开/读档时重建进度。
- 这样避免为了一个入口引导功能升级世界存档 schema、漂移黄金哈希或破坏旧回放。

### 3. 表现层只读

- Godot 在 `StepOnce` 后调用 `Observe`，HUD 只渲染 `TutorialSteps.All` 与
  完成状态；面板文案、勾选与 `PVE READY` 不自行判定完成。
- 蓝图覆盖仍用于工坊展示，但不再等同于 PvE 准入。

## 非目标

- 不实现科技树、AND/OR 能力解锁或高阶构造关卡。
- 不实现非程序员默认控制器模板；执行/状态步骤仍需要设备真正行动。
- 不把教程进度写入世界存档；跨版本持久化另见“开放问题”。
- 不改变 `IsMinimumUsable` 的工坊语义。

## 后果

- 教程完成需要真实仿真事实，杜绝“只填蓝图”通过。
- 无世界 schema/哈希变更；新增测试可使用现成世界与事件。
- 传感器判定在完成前为 O(设备 × 设备) 的简单扫描；教程世界规模很小，完成即停止。
- 进度已持久化到 GameSave v4（完成 Tick 排序、读档校验不超过世界 Tick）；
  旧 v1–v3 游戏存档没有 tutorials 字段时按空进度的新教程处理。

## 开放问题

- 第一组构造挑战是否扩成带目标/评分/AND-OR 前置的关卡目录。
- 非程序员默认策略（scout/sentry/relay 模板）如何在无脚本时自动绑定。

## 验证

- `TutorialProgressTests`：能量必须真的充电、传感器必须看到外国目标、通信必须
  送达、State 必须由带 State 模块的设备行动、执行必须有移动/射击；其他文明事件
  不计入；重复观察幂等；五步完成解锁 PvE；`Restore` 保留完成 Tick 并拒绝负 Tick。
- `GameSaveTests`：GameSave v4 往返保留教程进度；v3 旧档无 tutorials 时为空；
  拒绝未来 Tick、重复文明、未知步骤。
- `--tutorial`：真实引擎两步内完成 5/5 并输出 `pveReady=True`。
- Godot `--save-smoke`：`SAVE_SMOKE ok=True ... tutorial=5/5`。
- Godot `--capture`：HUD 显示五步与 `PVE READY`（截图 `m0-combat.png`）。

## 回滚

删除 `TutorialProgress` / `TutorialSteps`、HUD 面板与 `--tutorial` 演示；恢复
`IsMinimumUsable` 作为准入展示。不影响世界状态、存档或哈希。
