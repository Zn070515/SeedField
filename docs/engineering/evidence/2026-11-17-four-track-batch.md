# 证据：四条并行任务线（2026-11-17）

## 1. 跨文明通信

- `SendMessageIntent` 允许目标文明不同于发送者。
- 约束：目标文明必须在目标区域拥有至少一个 `Comm` 端点；否则 `NoRoute`。
- 消息进入目标文明收件箱，不进入发送者收件箱；目标区域仍作为观察过滤维度。
- 覆盖：本地跨文明、跨区跨文明、缺少目标端点、设备观察隔离。

## 2. 内置离线对手包

- `OpponentPackage`：版本化 manifest、入口清单、内容哈希、程序源。
- 校验：schema、kind、engine API、入口数、路径穿越（`..`/绝对路径/反斜杠/盘符）、重复入口、缺失入口、单文件/总大小上限。
- 内置受信任包：`builtin.skirmisher`、`builtin.turtle`；使用与玩家相同的 Observation/Intent 契约。
- Godot B 方使用 `builtin.skirmisher`；HUD 显示包 ID/版本。
- 第三方文件导入仍关闭；没有新增任意代码执行入口。

## 3. 核心区恢复命令

- `BootstrapCoreCommand` / `RestoreCoreProductCommand` 经 `SimulationEngine.ExecuteRecoveryCommand`。
- 返回统一 `RecoveryCommandResult` 与 `EntityBootstrapped` 事件。
- 成本仍由场景传入；配额/围困规则未冻结。

## 4. 建造队列

- `DeviceBlueprint.BuildTicks`；`DeployCommand` 先校验、扣 M，再生成 `ConstructionOrder`。
- 到期由引擎 `DeliverDueConstructions` 生成实体并产生 `EntityDeployed`。
- 材料只扣一次；订单进入存档（schema v4）与哈希。
- 失败/退款规则未实现。

## 构建与测试

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 93 passed
SeedField.Runtime.Js.Tests: 22 passed
```

## 运行证据

```text
CONSTRUCTION queued=True entity= orders=1 material=80
CONSTRUCTION completed tick=3 entity=1 material=80 ok=True

SAVE_SMOKE ok=True before=5283ddd6603e7506 mutated=3f6a8740f47effa6 after=5283ddd6603e7506 tick=39 A=v1 B=v1
```

Godot：`--capture-victory` 仍以 A 占两点击败；HUD 底部显示 `OPPONENT builtin.skirmisher v0.1.0`。

## 未覆盖

- 跨文明协议/条约/身份与情报权限。
- 第三方对手包导入与 OS 级隔离。
- 建造取消/退款/残骸与生产队列 UI。
