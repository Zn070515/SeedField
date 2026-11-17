# ADR-0022：签名包、显式同意与 Phase 2 门禁状态

- 状态：**Accepted（Phase 2a 已实现；第三方执行仍关闭）**
- 日期：2026-11-17
- 关联决策：D-02、D-03、P-02、O-01
- 前置：ADR-0020、ADR-0021

## 背景

ADR-0021 已实现工作进程、IPC 与 Job Object。Phase 2 还要求签名包、用户授权与
更硬的资源/解析边界。本 ADR 落地**可验证的 Phase 2a**，并明确哪些门禁无法在当前
环境诚实完成。

## 决定

### 1. 本地签名包

- 新项目 `SeedField.PackageHost`（host-only，允许文件 API，禁止网络/进程 API）。
- 包 = 目录：
  - `manifest.json`（≤64 KiB）；
  - 入口 `.js` 文件（路径安全、≤256 KiB/文件、≤32 文件、总计≤1 MiB）。
- 清单新增 `SignerKeyId` / `Signature`；`ContentHash` 仍按
  `key\0source\0` 排序 SHA-256。
- 签名：ECDSA P-256 + SHA-256，覆盖规范化清单（排序入口/能力，包含内容哈希）。
- 信任：`PackageTrustStore`（key id → 公钥）；空信任库不信任任何签名包。
- 加载前校验：路径/重解析点、额外文件、UTF-8、大小、内容哈希、签名、能力元数据。
- 加载器**只返回已验证的 `OpponentPackage`，不执行**。

### 2. 显式同意

- `PackageImportConsent(UserConfirmed, AllowUnsigned)`。
- 未确认 → `CONSENT_REQUIRED`。
- 无签名且未允许 → `UNSIGNED_PACKAGE`。
- 无签名但允许 → 返回 `TrustLevel.Unsigned`（本地开发/白名单场景）。
- 仅作为验证/授权输入；`ThirdPartyImportEnabled` 仍为 `false`。

### 3. 授权 CLI

- Headless `--package-check <dir> [--allow-unsigned] [--confirm]`：
  - 加载并打印包 ID/版本/信任级别/签名者/哈希；
  - 始终打印 `EXECUTION_DISABLED thirdPartyImport=False`；
  - 不启动工作进程、不执行脚本。

### 4. 资源与解析边界

- Job Object 新增进程 CPU 时间上限（默认 30s，相对时间）；
- `WorkerControllerOptions.DiskLimitBytes`（默认 8 MiB）+ 250ms 目录大小看门狗；
  超限杀 worker；
- IPC fuzz：300 个随机帧 + 300 个随机 envelope，只允许协议错误或成功；
- 目录大小守卫单元测试；CPU/磁盘 OS 行为作为证据级，不做易抖动的计时测试。

## Phase 2 门禁状态

| 门禁 | 状态 |
|---|---|
| 工作进程 + Job Object 内存/CPU/进程数 | ✅ Phase 1 + 2a |
| 长度前缀 IPC + 超时 | ✅ |
| IPC fuzz（基础） | ✅ 600 例；完整 fuzz/红队仍缺 |
| 签名/哈希/信任库 | ✅ |
| 显式同意 | ✅ CLI 级 |
| 磁盘看门狗 | ✅ 应用级（非 OS 配额） |
| AppContainer/低完整性令牌 | ❌ 未实现 |
| OS 网络/文件系统拒绝、只读挂载 | ❌ 未实现 |
| OS 磁盘配额 | ❌ 未实现 |
| 独立安全审查 | ❌ 未完成 |
| 发布签名密钥/Workshop | ❌ 未完成 |

因此 `ThirdPartyImportEnabled` 保持 `false`；没有游戏路径执行加载的包。

## 非目标

- 打开第三方执行导入。
- 实现 AppContainer/OS 网络文件拒绝（需要专门 Windows 安全 spike 与 ACL/自包含发布）。
- Steam Workshop/自动更新。
- 完整模糊测试平台/红队。
- 正式发布签名密钥管理。

## 后果

- 可以在不执行的前提下验证/审查本地包：路径、大小、哈希、签名、信任与同意。
- 第三方执行仍被全局门禁挡住；Phase 2a 是必要非充分条件。
- CPU/磁盘限额是应用/Job 级防护，不等于 OS 级文件/网络隔离。
