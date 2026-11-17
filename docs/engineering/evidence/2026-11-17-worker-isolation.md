# 证据：离线包隔离 Phase 1（2026-11-17）

关联：ADR-0021、ADR-0020。

## 1. 工作进程与 IPC

- `src/SeedField.Worker`：net10.0 console，只引用 Runtime.Js。
- stdin/stdout 长度前缀帧（4 MiB 上限）；`hello/ready/observe/intents/fault/shutdown`。
- 空帧、超长、截断、非法 UTF-8、缺 kind 都有测试。
- 工作进程绝不写 stdout 文本；诊断走 stderr，父进程保留最后 4 KiB。

## 2. 父进程控制器

- `WorkerController` 启动/重启/超时/崩溃处理；`Dispose` 杀进程并删除临时目录。
- 默认启动超时 5s、决策超时 500ms；Jint 语句/内存/超时预算通过 hello 下发。
- 工作进程崩溃/EOF/超时：父进程重启一次，仍失败返回 `ControllerFault`。

## 3. Windows Job Object

- 内存/Job 内存 128 MiB、活动进程数 1、`KILL_ON_JOB_CLOSE`。
- 测试（Windows）：`JobObjectAssigned=True`，`RequireJobObject=true` 时分配失败会拒绝启动。
- 非 Windows/分配失败：`IsolationDegraded=true`（Phase 1 仍是白名单，不运行第三方）。

## 4. 控制器工厂

- `IController` / `ControllerFactory`；`ControllerSlot`、`DeviceControllerSlot` 可注入工厂。
- 默认工厂仍是 `JsController`；Headless `--worker-opponent` 只让 B 方走 worker。

## 5. 双跑验证

```text
WORKER_OPPONENT inProcess=34886c1bb93164a1 worker1=34886c1bb93164a1 worker2=34886c1bb93164a1 match=True
```

- 进程内 B 与工作进程 B 的 600 Tick 最终哈希一致；worker 双跑一致。
- 工作进程测试：
  - 正常返回 `WaitIntent`；
  - 同一 Observation 两次决策一致；
  - 无限循环返回 `SCRIPT_STATEMENT_LIMIT`/`SCRIPT_TIMEOUT`，父进程不挂起；
  - 非法脚本启动失败关闭（`WORKER_LOAD_FAILED`）；
  - Dispose 后进程已退出；
  - Windows Job Object 已分配。

## 6. 测试与构建

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless -- --worker-opponent
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Repository.Tests: 8 passed
SeedField.Simulation.Tests: 220 passed
SeedField.Runtime.Js.Tests: 67 passed
WORKER_OPPONENT ... match=True
```

## 7. 仍未完成（第三方导入继续关闭）

- AppContainer/低完整性令牌；OS 网络/文件系统拒绝；磁盘配额；CPU Job 时间。
- 工作进程粒度与池化；Godot 设备级 worker。
- IPC 全量 fuzz、红队、签名包、用户授权 UI。
- `ThirdPartyImportEnabled=false` 与仓库 Lint 继续守门。
