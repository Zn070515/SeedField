# 证据：签名包、显式同意与 Phase 2a 限额（2026-11-17）

关联：ADR-0022、ADR-0021、ADR-0020。

## 1. 包验证

- `SeedField.PackageHost`：目录加载、manifest 64 KiB、程序 256 KiB/文件、
  最多 32 文件、总 1 MiB。
- 拒绝：路径穿越、额外文件、重解析点、非法 UTF-8、缺失入口、超大文件。
- 内容哈希：`key\0source\0` 排序 SHA-256；篡改程序 → `CONTENT_HASH_MISMATCH`。
- 签名：ECDSA P-256/SHA-256；篡改签名 → `INVALID_SIGNATURE`；未知 key →
  `UNTRUSTED_SIGNER`。
- 同意：未确认 → `CONSENT_REQUIRED`；未签名且不允许 → `UNSIGNED_PACKAGE`；
  允许后返回 `TrustLevel.Unsigned`。
- 实测 11 项 PackageHost 测试全绿。

## 2. 授权 CLI

```text
PACKAGE_CHECK id=test.unsigned version=1.0.0 trust=Unsigned signer= hash=4a16d37a...
EXECUTION_DISABLED thirdPartyImport=False
```

未加 `--confirm`：

```text
PACKAGE_CHECK failed [CONSENT_REQUIRED] Package import requires explicit user confirmation.
```

CLI 不启动 worker、不执行脚本。

## 3. 资源与解析

- Job Object 进程 CPU 时间上限默认 30s（相对）。
- 磁盘看门狗默认 8 MiB，每 250ms 检查 worker 临时目录；超限杀进程。
- 目录大小守卫测试：嵌套 100+200 字节、缺失目录、stopAt 提前停止。
- IPC fuzz：300 个随机帧 + 300 个随机 envelope；只出现协议错误或成功。
- `WorkerControllerOptions` 默认/非法 CPU/磁盘值测试。

## 4. 测试与构建

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless -- --worker-opponent
dotnet run --project src/SeedField.Headless -- --package-check logs/test-package --allow-unsigned --confirm
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Repository.Tests: 9 passed
SeedField.Simulation.Tests: 220 passed
SeedField.Runtime.Js.Tests: 85 passed
WORKER_OPPONENT inProcess=34886c1bb93164a1 worker1=34886c1bb93164a1 worker2=34886c1bb93164a1 match=True
PACKAGE_CHECK ... EXECUTION_DISABLED thirdPartyImport=False
```

## 5. 仍关闭

- AppContainer/低完整性令牌、OS 网络/文件拒绝、只读挂载、OS 磁盘配额。
- 完整 IPC fuzz/红队、发布签名密钥、Workshop、独立安全审查。
- `ThirdPartyImportEnabled=false`；没有游戏路径执行加载的包。
