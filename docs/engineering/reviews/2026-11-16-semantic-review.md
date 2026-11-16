# 语义审查：现有实现 vs V1.9 Rev2 设计基线

> 日期：2026-11-16  
> 范围：`src/SeedField.Simulation`、`src/SeedField.Runtime.Js`、`game/SeedField.Game`、全部测试  
> 方法：逐条对照 `docs/design-bible/v1.9-rev2/` 的 CONFIRMED/BASELINE 规则与架构边界；只审查语义，不审查风格。

## 结论摘要

系统骨架与文档方向一致：单一 C# 仿真源、固定 Tick、整数坐标、事件驱动 Godot、Jint JSON 边界、蓝图版本与核心区保底均已落地。  
本轮发现的问题已全部收口：R1/R2/R3/R5/R7/R8/R9 有代码与回归测试；R4 的部署/意图/存档入口已补保护；R6/R10 由 ADR-0003/0004 明确为原型语义或已知边界。剩余项只有核心区恢复命令统一、文明级 legacy 观察 API、离屏 Tick 预算。

## 已修复（本轮）

### R1 — Critical：意图没有发起文明校验

- 文档：`AGENTS.md` §3「统一动作验证」；`CORE_LOOP_AND_RULES.md` 不变量 1/4「行动经过仿真核验证」。
- 问题：`SimulationEngine.Step` 接收扁平 `Intent` 列表，只按实体 ID 查找；A 方 JS 可以提交 `entityId = B方实体` 的移动/攻击/运输意图，引擎照常执行。
- 修复：
  - `Intent.IssuerCivilizationId` 由 `SeedField.Runtime.Js` 在解析每条意图时写入。
  - `SimulationEngine.ProcessIntent` 校验发起文明与实体归属；不匹配时返回与“实体不存在”相同的 `UnknownEntity`，避免通过错误码探测敌方 ID。
  - 新增 `IntentOwnershipTests.ControllerCannotCommandAnotherCivilizationsEntity`。

### R2 — High：可攻击未被感知的目标

- 文档：`SIMULATION_TICK_AND_DETERMINISM.md`「Observation 只能包含合法传感器发现」；`SCENARIO_TEST_CASES.md` S3。
- 问题：`AttackIntent` 只校验同区域/射程/冷却；只要猜到目标 ID，就能攻击传感器之外的敌人，并用不同拒绝码探测其存在。
- 修复：
  - 新增 `VisibilityRules.IsVisibleTo`，与 `ObservationBuilder` 共用同一传感器规则。
  - 攻击前校验目标对发起文明可见；不可见统一返回 `TargetUnknown`。
  - 新增 `CombatTests.AttackAgainstAnUnsensedTargetIsRejected`；原“射程外”测试改为“可见但超射程”。

## 未修复（按严重度）

### R3 — High（运行时已修复）：传感器知识是文明级瞬时融合，绕过通信拓扑

> 已修复：`BuildForEntity` + `DeviceControllerSlot`；Godot 每个实体独立控制器，只读自己的设备观察。  
> 保留：文明级 `BuildObservation` 仅给工具/旧测试/命令级 AI，游戏运行路径不再使用。

- 文档：`CORE_LOOP_AND_RULES.md` 不变量 2「信息不能免费全图共享」；`SCENARIO_TEST_CASES.md` S3「断开中继后接收方不能继续获取新敌情」。
- 现状：`ObservationBuilder` 把所有己方实体、所有区域的传感器结果合并成一个文明级观察；一个侦察体的发现立刻被全文明控制器看到，不经过消息/中继/延迟。
- 影响：S3 的断联因果无法成立；单个控制器可跨区域全知。
- 方向：改为设备级 Observation/controller，或至少把敌方位置融合限制在通信可达的实体集合内；这属于架构调整，不应在现有文明级控制器上打补丁。

### R4 — High（本轮已部分修复）：核心区保护只存在于 API，不存在于世界规则

> 已修复：外文明部署进核心区、外文明实体在核心区行动、存档中的外文明核心实体均被拒绝。  
> 仍开放：恢复成本由调用方传入、恢复命令未统一、精确配额/围困破局。

- 文档：`CORE_ZONE_RECOVERY_AND_FAILURE.md`（CONFIRMED R-01/R-02）；`DECISION_REGISTER` R-01/R-02。
- 现状：`SeedCoreState` 只有储量与最新产品引用；核心区是普通 `RegionState`，没有“敌对意图不可进入/不可摧毁”的仿真规则；`TryBootstrapFromCore`/`TryRestoreLatestProduct` 是公开 C# API，成本由调用方随意传入。
- 影响：核心区不可毁的**产品承诺**尚不能由世界规则证明；只能证明“有一个 API 可以免费/低成本造单位”。
- 方向：核心区保护区/多出口规则；恢复走统一命令与配额；记录核心区保底支出与吞吐，防套利。

### R5 — Medium（已修复）：工坊部署/升级绕过 Intent 管线

> 已修复：`DeployCommand` / `UpgradeCommand` 经 `SimulationEngine.ExecuteDeploymentCommand`，产出 `EntityDeployed` / `EntityUpgraded`；工坊 UI 不再直接调用 `DeploymentService`。

- 文档：`ARCHITECTURE_AND_COMPONENTS.md` 关键边界 3「所有意图经过统一验证」；`AGENTS.md` 单一真实状态源。
- 现状：`WorkshopPanel` 直接调用 `DeploymentService.Deploy/Upgrade`；服务在 Simulation 内并有成本校验，但没有部署命令/事件/权限的统一入口。
- 影响：本地玩家 UI 与 JS 控制器不是同一条受验证路径；后续权限、回放、审计会分叉。
- 方向：引入 `DeploymentCommand`/Facade，产出稳定事件与审计；UI 只提交命令。

### R6 — Medium（ADR-0003 关闭为原型语义）：E/C/S/B 仍是原型近似

- 文档：`ECONOMY_ENERGY_MATERIALS_COMPUTE.md`；`DECISION_REGISTER` E-01/E-02/E-03。
- 现状：
  - C = 每条非 Wait 意图扣 1，占 `StateModule.ComputePerTick`；不是 JS 执行预算。
  - S = 己方 `StateModule.StorageCapacity` 之和，限制收件箱；不是完整历史/程序存储。
  - E = 实体本地电池 + 显式区域电网转运；没有自动电网潮流。
  - B = 每实体发信上限 + 每链路消息/物资/能源批次；没有拥塞模型。
- 影响：这些数值与语义不能当作冻结平衡；文档已标 `[PROPOSED]/[VALIDATE]`，实现与 README 需要继续明确“原型近似”。
- 方向：写 ADR 固化原型语义；在 UI/调试面板显示实际消耗来源。

### R7 — Medium（已修复）：存档 schema 长期停留在 v1

- 文档：`SCHEMAS_SAVES_AND_OPPONENT_FILES.md`「引擎/脚本/蓝图/存档分别版本化」「必须有迁移测试」。
- 现状：`WorldSaveData`/`GameSaveData` 多次加字段，`CurrentSchemaVersion` 仍为 1；无 v1→v2 迁移测试；活动区域是会话状态，不写入存档。
- 影响：旧存档靠 JSON 默认值“碰巧”可读；字段语义变化时无法判断兼容性。
- 方向：下一次形状变更提升 schema；加入迁移测试；在文档中明确 active set 属于会话。

### R8 — Medium（已修复）：被摧毁实体的库存离开守恒统计

- 文档：`ECONOMY_ENERGY_MATERIALS_COMPUTE.md`「资源守恒必须可测试」。
- 现状：`ProcessAttack` 直接移除实体；其剩余 E/M 不进入任何损失/回收事件，能量守恒测试只能避开摧毁场景。
- 影响：长期经济与战斗结算无法审计资源去向。
- 方向：摧毁时产出库存损失/残骸事件，或定义战利品/回收规则。

### R9 — Medium（运行时已修复）：消息/物流知识仍是文明级全知

> 已修复：设备观察只包含目标区域为该设备所在区域的消息/物资/能源；Godot 切片使用设备观察。  
> 保留：文明级 `BuildObservation` 仍合并全量消息，仅供工具/旧测试。

- 文档：`SIMULATION_TICK_AND_DETERMINISM.md`；`SCENARIO_TEST_CASES.md` S3/S9；O-01。
- 现状：跨区发送要求目标区有己方 Comm 端点，但消息到达后进入文明级收件箱，所有控制器都能看到；没有“只有目标区中继知道”的边界。
- 方向：设备/区域级收件箱，或至少按目标区域过滤 Observation。

### R10 — Low（ADR-0004 接受为边界）：区域活动只是意图 gating

- 文档：`FULL_GAME_ROADMAP.md` M3「离屏区模拟/加载」；本仓库 `IMPLEMENTATION_BASELINE.md` 已标注。
- 现状：非活动区仍跑被动发电/投递/核心再生，没有 Tick 预算或增量加载收益；活动集不持久化。
- 影响：目前是语义正确性切片，不是性能优化。

## 合规项（简要）

- 单一真实状态源：Godot 只读 `WorldState`/`SimulationEvent`，不改字段；JS 只返回 JSON 意图。
- 固定 Tick、整数坐标、稳定 ID 排序；消息/物资/能源的 `DeliverTick/ArriveTick` 均严格大于发送 Tick。
- 观察过滤、蓝图不可变版本、显式升级、核心区保底再生、Jint 限制与禁用 `eval`/CLR、第三方包导入关闭。
- 存档包含在途消息/物资/能源、控制器版本、蓝图注册表、核心区产品；拒绝未知 schema。
- CI 构建/测试/提交规范通过。

## 下一步建议

1. 先补 R3（设备级知识）或至少 S3 的可验证切片；这是“程序化自主”与 PvE 公平性的前提。
2. 把 R4 剩余项与 R5 合并成“统一部署/恢复命令”一次架构收口。
3. 再做 R7/R8 的存档迁移与资源损失审计。
4. 新功能（PvE/电网断供）在 R3/R4 之前不应扩大数量级。
