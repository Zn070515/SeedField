# 证据：Phase 2b AppContainer 工作进程沙箱

- 日期：2026-11-17
- 决策：ADR-0034
- 环境：Windows 11 Pro for Workstations，普通用户（无管理员）
- 命令：

```powershell
dotnet run --project src/SeedField.Headless -- --sandbox-check
dotnet run --project src/SeedField.Headless -- --worker-opponent-appcontainer
```

## 沙箱检查

```text
SANDBOX_LAUNCHER C:\...\SeedField.SandboxLauncher.exe
SANDBOX_EXEC exit=0 ok=True
SANDBOX_FILE exit=1 denied=True
SANDBOX_NETWORK exit=6 denied=True
```

- `SANDBOX_EXEC`：`cmd /c echo` 在 AppContainer 内运行；
- `SANDBOX_FILE`：读取 `%TEMP%` 外部文件被拒绝（Access is denied）；
- `SANDBOX_NETWORK`：curl 无法联网（DNS/连接失败，exit 6）；
- `AppContainerCannotWriteToWorkerDirectory` 测试确认 worker 目录不可写。

## Worker 集成

```text
WORKER_OPPONENT inProcess=df627fa8515572c5 worker1=df627fa8515572c5 worker2=df627fa8515572c5 match=True
```

- `--worker-opponent-appcontainer` 下 B 方 worker 在 AppContainer 中运行，
  IPC 决策与进程内 A 的最终哈希一致；
- 启动器创建 Job Object（128 MiB、30s CPU、单进程、kill-on-close），
  worker 以 suspended 状态创建后加入 job 再恢复。

## 测试

`AppContainerTests`（4 个）：

1. `WorkerRunsUnderAppContainer`：WorkerController 在 AppContainer 内决策；
2. `AppContainerDeniesFileOutsideSandbox`：外部文件读取拒绝；
3. `AppContainerDeniesNetwork`：网络拒绝；
4. `AppContainerCannotWriteToWorkerDirectory`：worker 目录写入拒绝。

Windows/无 AppContainer 环境会跳过这些断言（launcher 预检失败即返回）。

## 剩余门禁

- OS 磁盘配额：当前 worker/脚本无文件 API，AppContainer 无写权限；
  应用级磁盘看门狗仍保留。未来开放文件 API 需 VHD/文件系统配额。
- 完整 fuzz/红队、发布签名密钥、独立安全审查、Workshop 仍未完成。
- `ThirdPartyImportEnabled=false` 不变；第三方可执行包仍不可导入。
- 非 Windows 不支持 AppContainer；产品 Windows 优先。
