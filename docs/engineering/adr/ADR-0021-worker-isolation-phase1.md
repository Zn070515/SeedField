# ADR-0021：离线包隔离 Phase 1（工作进程、IPC、Job Object）

- 状态：**Accepted（Phase 1 已实现；第三方导入仍关闭）**
- 日期：2026-11-17
- 关联决策：D-02、D-03、P-02、O-01
- 前置：ADR-0020

## 背景

ADR-0020 定义了父进程权威 + 不可信工作进程的目标架构，并保持第三方可执行包关闭。
本 ADR 落地 Phase 1 的最小纵切片：**一个控制器一个工作进程、长度前缀 IPC、
Windows Job Object 限额、控制器工厂、Headless 双跑验证**。

## 决定

### 1. 工作进程

- 新增 `src/SeedField.Worker`（net10.0 console），只引用 `SeedField.Runtime.Js`。
- 进程只通过 stdin/stdout 交换帧；stdout 不写人类日志，诊断写 stderr。
- 工作进程只持有一个 Jint `JsController`，不接触 `WorldState`、文件路径或宿主 CLR 对象。
- 脚本通过 `hello` 下发（≤256 KiB），不落盘。

### 2. IPC

- 4 字节小端长度前缀 + UTF-8 帧；单帧上限 4 MiB。
- 消息：`hello` / `ready` / `observe` / `intents` / `fault` / `shutdown`。
- 空帧、超长、截断、非法 UTF-8、未知 kind、缺 kind 均拒绝。
- 父进程按请求 ID 关联响应；启动超时默认 5s，单次决策超时默认 500ms。
- 工作进程崩溃/EOF/超时：父进程杀掉并重启一次；再次失败返回 `ControllerFault`。

### 3. 资源与隔离

- Windows Job Object：
  - 进程内存 / Job 内存上限（默认 128 MiB）；
  - 活动进程数 1；
  - `KILL_ON_JOB_CLOSE`。
- Jint 仍保留语句/内存/单次超时预算。
- 工作目录为每次启动唯一临时目录；启动前移除 `DOTNET_STARTUP_HOOKS`、
  `CORECLR_*`、`COMPlus_*` 环境变量；设置 `DOTNET_EnableDiagnostics=0`。
- 非 Windows / Job 分配失败：`IsolationDegraded=true`；`RequireJobObject=true` 时启动失败。
- **不申报**完成 AppContainer、OS 网络拒绝、只读包挂载、磁盘配额；这些仍是开放门禁。

### 4. 控制器工厂

- 新增 `IController` 与 `ControllerFactory`。
- `JsController`、`WorkerController` 都实现 `IController`。
- `ControllerSlot` / `DeviceControllerSlot` 接受工厂；默认仍是进程内 Jint。
- Headless `--worker-opponent` 让 B 方走 `WorkerController`，A 方保持进程内。

### 5. 验证

- 进程内 B 与工作进程 B 的 600 Tick 最终哈希一致：
  `34886c1bb93164a1`，工作进程双跑也一致。
- 工作进程测试覆盖：正常意图、确定性、无限循环限额故障、非法脚本启动失败、
  Dispose 杀进程、Windows Job Object 已分配。
- 第三方导入开关 `ThirdPartyImportEnabled=false` 保持不变；仓库 Lint 继续守门。

## 非目标

- 打开第三方可执行包导入。
- AppContainer/低完整性令牌、网络/文件系统 OS 级拒绝、磁盘配额、CPU Job 时间。
- Godot 设备级工作进程（当前只接 `ControllerSlot` 路径）。
- 多包并发/每文明/每设备的工作进程粒度优化。
- 跨平台（Linux/macOS）OS 限额。
- 完整 IPC fuzz/红队与签名包。

## 后果

- 控制器可以在独立进程运行，父进程仍验证全部 Intent；进程内路径保持默认。
- 工作进程启动成本按控制器计；600 Tick 双跑验证未暴露问题，但大量设备级 worker
  需要后续池化/粒度调整。
- 第三方包仍不能导入：Phase 1 只是必要非充分条件。

## 回滚

- Headless 加 `--worker-opponent` 才走 worker；移除该 flag 即回到进程内。
- `ControllerSlot` 默认工厂仍是 `JsController`，Godot/现有测试不受影响。
