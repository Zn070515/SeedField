# Changelog

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)；版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Added

- C# 固定 Tick 仿真核心：观察过滤、意图验证、事件、状态哈希。
- Jint 4.18.0 控制器运行时与原子热部署（失败保留旧版本）。
- Godot 4.7.2 Mono 可视切片、程序编辑器、工坊面板与 F5/F9 存档。
- 蓝图不可变版本、实例部署/显式升级、五分支最低可用判定。
- 核心区保底再生、最新五分支产品登记与恢复。
- 本地 M 库存扣费、State 算力预算、Comm 带宽上限、收件箱 S 容量。
- 版本化世界存档与游戏存档（含控制器脚本/版本）。
- `.github` CI、依赖审查、Dependabot、Issue/PR 模板与提交规范。

### Changed

- 运行时目标统一为 .NET 10.0。
- Godot 探针与 net10.0 工程兼容性验证完成。

### Fixed

- 消息投递测试按逻辑 Tick 断言，避免把“同一 StepResult”误判为同 Tick 到达。
- 重建 Godot 引用的仿真程序集，避免继续运行旧 DLL。

[Unreleased]: https://github.com/Zn070515/SeedField/commits/main
