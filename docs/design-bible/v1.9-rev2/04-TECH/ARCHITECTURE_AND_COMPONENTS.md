# 正式游戏技术架构总览

## 已确定技术组合 [CONFIRMED]

| 层 | 技术 | 目的 |
|---|---|---|
| 游戏客户端与场景/UI | Godot **4.7.2 .NET**（.NET 10.0 构建目标） | Windows 首发、2D 画面、输入、音效、资源与交互 |
| 世界仿真 | **C#** | 固定 Tick、实体状态、规则、经济、战斗、跨区、事件 |
| 玩家/对手控制脚本 | **JavaScript** | 真正可编程的行为逻辑、状态机和协作 |
| JS 嵌入执行 | **Jint 4.18.0** | 受限运行、错误诊断、宿主集成 |
| 编辑辅助 | TypeScript **`.d.ts`** | 类型声明、补全、文档；不要求 TS 作为正式运行语言 |
| 美术 | 2D + Blender 预渲染 + Shader | 统一高完成度机体、网络、战斗表现 |

## 逻辑架构图

```text
       Game/Editor/World UI (Godot)
                  │ input / design / commands
                  ▼
      Controller & Deployment Facade
                  │
     ┌────────────┼──────────────┐
     │            │              │
 Workshop/Blueprints│     Save/Replay/Debug
     │            │              │
     ▼            ▼              │
          SimulationCore (C#)
     World / Region / Civ / Entity / Network
      Energy / Materials / Combat / Events
           ▲                  │
           │ validated Intent │ Observation/Events
           │                  ▼
       Control Runtime    Observation Filter
        Jint/JS             ▲        │
           ▲                └────────┘
           │
     Local controller / Offline opponent version
```

所有动作、库存、伤害、通信和跨区事件以 `SimulationCore` 为权威；UI、Shader、视效不得成为真状态来源。

## 推荐逻辑子域 [PROPOSED]

- `Simulation.World`：世界 Tick、区域索引、文明与持久事件。
- `Simulation.Region`：二维位置、实体、设施、通道、空间查询。
- `Simulation.Resources`：E/M 储量、供电、简化物流与可回收事件。
- `Simulation.Information`：传感器、可见性、消息、带宽 B、存储 S。
- `Simulation.Computing`：抽象算力 C 的预算、设备能力和事件反馈。
- `Simulation.Combat`：武器、护盾、模块、伤害与状态变化。
- `Design.Workshop`：元件、模块、机架、蓝图、验证与版本。
- `Design.Deployment`：物化实例、升级、回滚、维护。
- `Runtime.JS`：Jint 适配、序列化、安全上下文、故障中止。
- `Civilization.Offline`：内置/导入文明包、策略状态、实例化。
- `Meta.SeedCore`：永久核心区、保底 E/M、五分支最新产品注册与最低恢复。
- `Persistence`：存档、迁移、事件日志、复现和离线包缓存。
- `Presentation`：Godot 场景、UI、特效与音频。

这是职责域，不要求一上来按微服务、程序集或复杂的 ECS 框架过度分拆。先稳定公共 schema 和可运行测试。

## 关键边界

1. `WorldState` 不向 JS 或 UI 暴露可变引用。
2. 观察过滤由 C# 完成；受限实体 API 不等同于世界状态全量序列化。
3. 所有意图经过统一验证，拒绝非法动作并记录原因。
4. 资源/消息/碰撞的竞争处理必须有确定顺序或明确决策规则。
5. 设计 `Blueprint` 与已部署 `Instance` 的生命周期分离。
6. 离线文明包是外部**数据和代码版本**，不是自动享有宿主进程的高权限插件。
7. 存档含世界实例状态、设计版本引用、Jint API 版本和随机性状态。
8. 任何网络功能只作为可选内容分发增强；离线断网时完整功能正常。

## 首发架构不做的事情

微服务集群、账号/联机服务器、游戏开发者托管的 Python/Node 执行环境、远端 AI 模型代理、对玩家仓库执行任意 shell 的扩展系统、持续同步的全球文明数据库。

## 技术决策替换原则

若 Jint/接口无法在最低配置、安全或规模上达标，先留可复现 benchmark 和安全复现，再写 ADR 评估解决方案。不得只因“想用 C++/Rust/QuickJS”就重构全部规则系统。[BASELINE]
