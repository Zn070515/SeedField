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

## 已知边界

- 当前没有发布二进制；Godot 导出模板尚未安装。
- 当前只有内置可信脚本参与运行；任意第三方代码导入未开放。
