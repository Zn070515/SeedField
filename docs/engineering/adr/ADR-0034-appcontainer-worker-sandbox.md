# ADR-0034：Phase 2b AppContainer 工作进程沙箱

- 状态：**Accepted（Phase 2b 核心已实现；第三方执行仍关闭）**
- 日期：2026-11-17
- 关联决策：ADR-0020、ADR-0021、ADR-0022
- 前置：Worker/IPC、Job Object、签名包与显式同意

## 背景

ADR-0022 的门禁表仍缺 AppContainer/低完整性、OS 网络与文件系统拒绝。Worker
目前只有 Jint 预算、Job Object 和目录看门狗；工作进程仍以普通用户令牌运行，
可访问网络和用户文件。Phase 2b 需要 OS 级隔离，而不是应用级检查。

## 决定

### 1. 独立沙箱启动器

新增 `SeedField.SandboxLauncher`（Windows，独立进程）：

- 通过 `CreateAppContainerProfile` / `DeriveAppContainerSidFromAppContainerName`
  创建或复用 AppContainer profile；profile 名默认 `SeedField.Worker`；
- 仅给 AppContainer SID **读/执行**权限：`icacls <worker dir> /grant *SID:(OI)(CI)(RX)`；
- 用 `CreateProcess` + `STARTUPINFOEX` +
  `PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES` 启动 worker；
  **不授予任何 capability**（无 internetClient/privateNetwork）；
- 继承父进程 stdio 句柄并转发给 worker，宿主仍使用普通 `Process` 重定向；
- 创建 Job Object（内存、CPU 时间、单进程、kill-on-close），通过
  `CREATE_SUSPENDED` + `AssignProcessToJobObject` + `ResumeThread` 把 worker
  原子地放入 job；
- worker 当前目录设为 worker 目录（读/执行），不设置可写目录；
- profile 默认保留复用（`--keep-profile`），避免每个 worker 创建新 profile。

### 2. 宿主集成

- `WorkerControllerOptions.UseAppContainer`（默认 false）：
  - true 时通过 `WorkerLocator.FindLauncher()` 启动启动器；
  - 启动器不存在/启动失败 → 硬失败，**不静默回退到无沙箱 worker**；
  - 宿主此时不再把启动器放进自己的 Job（host Job 的
    `ActiveProcessLimit=1` 会阻止启动器创建 worker），资源边界由启动器的 Job
    负责；`ThirdPartyImportEnabled` 仍为 false。
- `--worker-opponent-appcontainer`：Headless 用 AppContainer 运行 B 方 worker，
  与进程内 A 比较最终哈希。
- `--sandbox-check`：验证容器内执行、外部文件读取拒绝、网络拒绝。

### 3. 验证结果

- AppContainer 内 `cmd /c echo` 正常；
- 读取 `%TEMP%` 外部文件：`Access is denied`（exit 1）；
- `curl http://example.com`：exit 6（无法联网）；
- 写入 worker 目录：文件不存在（无写权限）；
- `SeedField.Worker.exe` 在 AppContainer 内正常启动，IPC 决策可用；
- `--worker-opponent-appcontainer` 与进程内哈希一致。

### 4. 剩余门禁

- **OS 磁盘配额**：当前 worker/脚本没有文件 API，AppContainer 只授读/执行，
  没有可写文件面；仍保留应用级目录看门狗。未来若开放文件 API，必须加
  VHD/文件系统配额或受限可写目录。
- 完整 fuzz/红队、发布签名密钥、独立安全审查、Workshop 流程仍未完成。
- 部署必须同时携带启动器与 worker，并在安装时建立 AppContainer profile/ACL；
  当前仓库/开发路径由启动器现场授权。
- 非 Windows：`UseAppContainer=true` 不支持；产品定位 Windows 优先。

## 非目标

- 不打开 `ThirdPartyImportEnabled`；独立审查/发布密钥完成前保持关闭。
- 不做 Linux/macOS 沙箱。
- 不做系统级防火墙规则（AppContainer capability 模型已阻断网络）。
- 不做 OS 磁盘配额（当前无可写文件面）。

## 后果

- worker 获得 OS 级网络/文件隔离；宿主仍通过 IPC 与 Job 限额交互。
- 启动器是一个额外进程；worker 的实际资源边界在启动器 Job 中。
- AppContainer profile/ACL 是安装/部署关注点；开发树由启动器自动授权。
- 测试在非 Windows/无 AppContainer 环境会跳过，Windows CI/开发机实际执行。

## 验证

- `AppContainerTests`（4 个）：worker 决策、外部文件拒绝、网络拒绝、
  worker 目录写入拒绝。
- `--sandbox-check` 输出。
- `--worker-opponent-appcontainer` 哈希一致。
- SECURITY/ADR-0022 门禁表更新；第三方导入仍关闭。

## 回滚

移除 `SeedField.SandboxLauncher`、`UseAppContainer` 选项与 CLI，恢复普通
`ProcessStartInfo` worker；`ThirdPartyImportEnabled` 不变。AppContainer profile
可由管理员删除。
