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
- 跨文明消息策略、建造失败退款、非活动区发电延迟与补算。
- `.github` CI、依赖审查、Dependabot、Issue/PR 模板与提交规范。
- M1 教程准入：五分支最低可用由仿真事实验证，Godot HUD 教程面板与 `--tutorial` 演示（ADR-0040）。
- 美术方向：世界层改为 Mindustry 式正交俯视废土地块地图，新增 `MindustryStyle` 样片（ADR-0041）。
- 废土资产包 v1：9 建筑 + 3 道具 + 4 矿脉 + 4 无缝地表；Blender 程序化材质与暖光；Godot 样片改用真实纹理（ADR-0043）。
- M2 采集闭环：区域有限矿脉、钻机模块、`MineIntent`、`builtin.harvester.v1`；世界存档 v20（ADR-0044）。
- 矿脉观察与 UI：`ObservedDeposit`/`hasDrill`/`drillRangeMm`、harvester 移动开采、HUD 余量/耗尽标签。
- M4 战斗深度 v1：装甲、护盾、Beam 武器、低血量模块失效；世界存档 v21（ADR-0045）。
- M2 生产链：钻机产 Ore、冶炼厂转 M、构件厂产 Components；构件成本/退款；世界存档 v22（ADR-0046）。
- 矿脉侦察发现、装甲/护盾/生产模块观察字段、`SetProductionIntent` 生产开关；世界存档 v24（ADR-0047）。
- 多矿种配方：铜/铅/钛/煤分库存，铅副产构件、煤副产能源；观察分矿种库存；世界存档 v25（ADR-0048）。
- 测试体系加强：经济 golden fixture、经济不变量 fuzz、损坏存档矩阵、事件展示/意图契约/ADR 验证段门禁（ADR-0049）。
- 教程进度写入游戏存档 v4（完成步骤 + Tick），旧 v1–v3 存档按空进度读取。
- M1 完成：内置 idle/scout/sentry/relay 策略模板、AND/OR 构造挑战目录、正常启动从 Tick 0 开始（ADR-0042）。

### Changed

- 归档已完成的 superpowers spec/plan 与 2026-11-17 斜俯视美术指导，新增归档索引并更新链接。


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
