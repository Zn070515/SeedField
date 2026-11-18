# ADR-0042：M1 完成——内置策略模板、构造挑战目录与首启教程

- 状态：**Accepted**
- 日期：2026-11-18
- 关联决策：G-02、G-07、G-08、G-10、G-11；ADR-0040（教程准入）
- 前置：五分支蓝图、工坊、TutorialProgress、GameSave v4

## 背景

M1 的退出条件要求：

- 非程序员也能用最低可用结构完成入门，不能强制写 JavaScript；
- 五分支最低可用完成即可进入 PvE，低效率合法构造不被拒；
- 第一组构造挑战存在，AND/OR 只约束后续能力，不锁 PvE；
- 教程要有可执行的首次体验，而不是一张说明面板。

截至 ADR-0040，教程事实与准入已完成，但仍缺：默认策略模板、构造挑战目录、
以及“正常启动时教程真的从头开始”的流程（此前启动会预跑到战斗，教程直接 5/5）。

## 决定

### 1. 内置策略模板（非程序员路径）

新增 `SeedField.Runtime.Js.DevicePrograms`：

| id | 用途 | 关键行为 |
|---|---|---|
| `builtin.idle.v1` | 供能/传感/状态等静态设备 | 不产生 Intent |
| `builtin.scout.v1` | 侦察 | 报告接触、向目标/控制点移动、射程内攻击 |
| `builtin.sentry.v1` | 火力 | 射程内攻击；有驱动时接近；否则周期性报告 |
| `builtin.relay.v1` | 中继 | 每 60 Tick 发送一次 `status` 心跳 |

- 模板是普通 Jint 脚本，遵守同一 Observation/Intent 契约，不访问宿主。
- 只有蓝图显式设置 `ControllerProgramId = builtin.*` 才启用；未知/缺失 id 回退到
  文明程序，保持 F1 编辑器的既有语义。
- 初始工坊蓝图：power/sensing/state 用 idle，comm 用 relay；strike/scout 继续使用
  文明程序（编辑器演示与既有行为不变）。
- `SuggestId` 按模块推荐模板（武器+传感器→sentry，传感器+驱动→scout，仅通信→relay，
  否则 idle），供后续工坊作者 UI 使用；当前不强制改写蓝图。

### 2. 构造挑战目录

新增 `ConstructionChallenges`：

- 五个 branch 挑战直接映射教程步骤；
- `network.minimum` = Energy **AND** Communication **AND** Execution；
- `field.minimum` = Sensing **OR** Communication；
- 评估只读 `TutorialProgress`，不写世界、不进哈希/存档；
- 挑战不参与 `PveUnlocked`：PvE 仍只由五分支事实决定，AND/OR 只描述后续能力。

### 3. 首启教程流程

- 正常启动（无 capture/smoke/benchmark 参数）从 Tick 0 开始，教程 0/5、
  PvE 锁定；`RequiresPreRun` 只为截图、冒烟与基准保留预跑。
- HUD 面板按规范顺序列出五步、两个 AND/OR 挑战、当前 `NEXT` 提示与
  `PVE READY` 状态；进度随 GameSave v4 持久化。
- 新增 `--tutorial-smoke` 验证首启状态与退出码。

## 非目标

- 不做完整科技树、战役关卡或挑战编辑 UI；挑战目录只是第一组可执行定义。
- 不把模板做成“最优解”；低效率/不同装配仍有空间。
- 不改工坊发布/升级语义，不改 Simulation 世界状态、哈希或 Tick。
- 不把模板用于不可信第三方代码；模板是内置只读内容。

## 后果

- 非程序员可部署 relay/power/sensor 等设备完成通信/能源等分支，不需要编辑 JS。
- 构造挑战可见、可评估，但不会阻塞自由 PvE；后续科技树可复用该目录结构。
- 正常启动不再“跳过教程”；截图/基准仍保留预跑状态。
- `builtin.*` id 进入世界存档/哈希（已有 `ControllerProgramId` 字段），
  改名需要迁移；本轮保持 id 稳定。
- 模板行为由单元测试与真实引擎集成测试锁定；修改模板需同步测试与证据。

## 验证

- `DeviceProgramsTests`：id 解析、回退、角色推荐、sentry/scout/relay/idle 行为、
  relay 真实发送并完成 Communication 分支。
- `ConstructionChallengeTests`：AND 需全部、OR 任一即可、五分支全完成、PvE 不受挑战影响。
- `TutorialProgressTests.NextStepFollowsCanonicalOrder`。
- `--tutorial` / `--challenges` Headless 输出 5/5 与 7/7。
- Godot `--tutorial-smoke`：`TUTORIAL_SMOKE ok=True tick=0 tutorial=0/5 pve=False`。
- 全量测试与仓库门禁通过。

## 回滚

删除 `DevicePrograms`、挑战目录、`--challenges`/`--tutorial-smoke` 与面板扩展；
恢复初始蓝图 `ControllerProgramId` 与启动预跑。不影响世界存档 schema（除非要移除
已写入的 `builtin.*` 值，需单独迁移）。
