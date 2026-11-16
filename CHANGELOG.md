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
- 多区域显式链路、双向 BFS（扩展较小 frontier）、跨区消息延迟与链路带宽。
- 跨区材料物流：多跳路径、在途状态、路径总延迟、链路物资容量与本地库存扣减。
- 跨区能源：区域电网库存、实体存入/抽取、远端运输延迟与链路能源容量。
- 区域活动：非活动区意图 gating，被动系统继续运行。
- 设备级观察/控制器：每实体独立知识边界，Godot 切片改用设备控制器。
- 统一部署/升级命令：`DeployCommand` / `UpgradeCommand` 与领域事件。
- 存档 schema v2，支持 v1 迁移；摧毁实体库存损失进入统计。
- 多控制点 PvE 目标：独占驻留占领、争夺重置、占领数量阈值胜负、HUD 与存档 v3。
- 跨文明通信（目标区 Comm 端点）、内置受信任对手包、核心区恢复命令、建造队列。
- 建造取消/退款、Skirmisher/Turtle 对手选择、跨文明消息协议边界 ADR。
- 对手选择写入游戏存档；工坊建造队列 UI 与取消按钮。
- `.github` CI、依赖审查、Dependabot、Issue/PR 模板与提交规范。

### Changed

- 运行时目标统一为 .NET 10.0。
- Godot 探针与 net10.0 工程兼容性验证完成。

### Fixed

- 消息投递测试按逻辑 Tick 断言，避免把“同一 StepResult”误判为同 Tick 到达。
- 重建 Godot 引用的仿真程序集，避免继续运行旧 DLL。
- 校验意图发起文明，阻止控制器操控其他文明实体。
- 攻击必须对目标有传感器可见性，隐藏目标统一返回 `TargetUnknown`，避免 ID 探测。
- 增加语义审查记录：`docs/engineering/reviews/2026-11-16-semantic-review.md`。
- 核心区保护：拒绝外文明部署、核心区行动与含外文明核心实体的存档。

[Unreleased]: https://github.com/Zn070515/SeedField/commits/main
