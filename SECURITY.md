# Security Policy

## 支持范围

`main` 是唯一开发主线；发布版本的安全修复会先进入 `main`。

## 报告漏洞

请使用 GitHub 的 **Private vulnerability reporting / Security Advisories**：

<https://github.com/Zn070515/SeedField/security/advisories/new>

不要在公开 issue 中披露可利用细节、密钥或第三方包中的恶意代码。

## 重点范围

- 不可信 JavaScript：Jint 语句/内存/超时限制不是 OS 沙箱。
- 存档解析：路径穿越、超大输入、未知 schema、损坏消息。
- 第三方离线文明包：在完成进程隔离、IPC 校验与资源限额前，导入/执行保持关闭。
- 构建链：依赖投毒、CI 权限、发布产物完整性。

## 隔离门禁

- 威胁模型与目标隔离架构：
  [`docs/engineering/security/2026-11-17-offline-package-threat-model.md`](docs/engineering/security/2026-11-17-offline-package-threat-model.md)
- 决策：[ADR-0020](docs/engineering/adr/ADR-0020-offline-package-isolation-gate.md)
- 代码门禁：`OpponentPackage.ThirdPartyImportEnabled = false`；
  `tests/SeedField.Repository.Tests` 禁止对手包命名空间出现文件/网络/进程/程序集 API。
- Phase 1（ADR-0021）已实现：独立工作进程、长度前缀 IPC、Windows Job Object
  （内存/单进程/kill-on-close）、控制器工厂、Headless 双跑哈希验证。
- **仍未实现**：AppContainer/低完整性令牌、OS 网络与文件系统拒绝、磁盘配额、
  CPU Job 时间、完整 IPC fuzz、签名包、用户授权 UI。因此第三方可执行包导入继续关闭。
- 开放第三方可执行包前，必须逐项通过威胁模型第 5 节的 12 项硬门禁。

## 已知边界

- 当前没有发布二进制；Godot 导出模板尚未安装。
- 当前只有内置可信脚本参与运行；任意第三方代码导入未开放。
- 数据-only 废墟快照（ADR-0018）不是可执行包；它只能生成残骸，不执行代码。
