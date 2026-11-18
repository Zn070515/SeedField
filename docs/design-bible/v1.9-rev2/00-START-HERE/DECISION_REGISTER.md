# DECISION REGISTER — V1.9 Rev2 统一决策登记表

本表是产品讨论的决策级摘要；详见各规范。**状态重要于文件书写口吻**。2026-11-16 之后的任何明确用户确认均须增量写入本表并修改相关规格。

| ID | 决策 | 状态 | 来源阶段 |
|---|---|---|---|
| P-01 | Steam 付费 Windows 优先单机、完整离线可玩 | CONFIRMED | V1.9 / Rev2 补充 |
| P-02 | 不运营开发者服务器/账号/持续后端 | CONFIRMED | Rev2 补充 |
| T-01 | Godot 4.7.2 .NET（.NET 10.0 构建目标）；C# 仿真 | CONFIRMED | V1.9 Rev1 / 工程补充 |
| T-02 | 游戏内 JavaScript，Jint 4.18.0；`.d.ts` 补全契约 | CONFIRMED | V1.9 Rev1 |
| W-01 | 主控崩溃已久、弱小玩家种子进入成熟数字文明世界 | CONFIRMED | 历史 + Rev2 Q6 B |
| W-02 | 多区域世界，每区真实二维空间、区域通过通道/网络连接 | CONFIRMED | Rev2 Q1 B |
| G-01 | 玩家实时任务指挥+暂停，设备由程序自主执行 | BASELINE | V1.5 |
| G-02 | 模板起步+深度自由编程，不退化成参数滑块 | BASELINE | V1.5 |
| G-03 | 精致机架与较自由模块装配 | BASELINE | V1.5 |
| G-04 | 首次正式攻防目标在 30–60 分钟 | BASELINE/VALIDATE | V1.5 |
| G-05 | 手工关键体验+半程序化开放区域 | BASELINE | V1.5 |
| G-06 | 游戏世界动态演化，对手有限资源/情报，自主竞争 | CONFIRMED | V1.9 |
| G-07 | 工坊和代码编辑默认暂停，可切换实时 | CONFIRMED | V1.9 |
| G-08 | 构造关卡与自由世界同存档/同元件库/同底层规则 | CONFIRMED | V1.9 |
| G-09 | 真实机会提示但不强迫目标 | CONFIRMED | V1.9 |
| G-10 | 新手五分支最低可用完成即可进入 PvE，质量低不阻塞 | CONFIRMED | V1.9 Rev1 |
| G-11 | AND/OR 前置只约束后续构造能力/挑战，不锁 PvE | CONFIRMED | V1.9 Rev1 |
| E-01 | E 能源 M 基质为库存；C 算力 S 存储 B 带宽为容量 | BASELINE | V1.5 |
| E-02 | E 网络供给、M 简化物流，可逐步增加高级物流 | CONFIRMED | Rev2 Q3 C |
| E-03 | 控制器基础预算+硬件升级+分布式协作 | CONFIRMED | Rev2 Q4 C |
| E-04 | 游戏算力与 Jint 宿主安全配额是两层 | BASELINE | Rev2 审查 |
| D-01 | 文明差异由协议/偏好/资源禀赋塑造 | CONFIRMED | Rev2 Q5 B |
| D-02 | PvE 对手来自其他玩家的离线版本，导入本地运行 | CONFIRMED | Rev2 补充 |
| D-03 | 充分的内置离线对手；文件分发，Workshop 可选 | BASELINE | Rev2 补充 |
| D-04 | 其他文明核心区是否也不可摧毁 | OPEN | Rev2 补充 |
| R-01 | 玩家核心区不可摧毁，保存种子+五分支最新已确认产品 | CONFIRMED | Rev2 补充 |
| R-02 | 核心区低速不可切断再生 E/M，外部全毁可复建 | CONFIRMED | Rev2 补充，方案 B |
| R-03 | 核心区外的领地/装备/资源/设施可损失 | CONFIRMED | Rev2 补充 |
| R-04 | 核心区精确费率、库存、突破封锁、防资源套利 | OPEN/VALIDATE | 后续平衡 |
| C-01 | 基础科技通过探索/研究；高级依构造验证+编程 | CONFIRMED | Rev2 Q8 C |
| C-02 | 多文明级终局，终局后继续沙盒 | CONFIRMED | Rev2 Q7 C |
| A-01 | 精致 2D + Blender 预渲染 + Shader，具体路线样片比较 | BASELINE/VALIDATE | V1.5 |
| A-02 | 世界地图采用 Mindustry 式正交俯视地块 + 末日废土美术：90° 俯视、网格地块、地表资源矿脉、块状锈蚀建筑 | CONFIRMED | 2026-11-18 用户明示 |
| N-01 | 极轻剧情、系统沙盒优先，不围绕生物/意识 | BASELINE | 历史讨论 |
| O-01 | 玩家脚本跨区消息延迟、权限、预算要真实受限 | BASELINE | V1.5 / M0 |

## 规则覆盖优先级

**新近用户明示决定 > 本表 CONFIRMED > 当前子系统文档中的 BASELINE > 历史原型文档 > Agent 的 PROPOSED > 任意默认值。**

## 被替代的旧结论

- V1.5 中 Godot Standard / Lua / Luau 的待定方案 → 已由 Godot .NET / JS + Jint 4.18.0 替代。
- “玩家所有恢复节点失效即可彻底死亡” → 已由不可摧毁种子核心区和保底 E/M 再生替代。
- “PvE 主要是内置 NPC 数值文明” → 以其他玩家离线设计的文明版本为体系核心；**内置对手仍必须存在**，可由开发者使用同一工具制作。
- “多区域间必须有具体传送成本”尚未被选择；只冻结多区域的连接结构，不虚构运输公式。

## 待决不阻挡原型

其他文明的永久核心/退场；离线包的完整战略自治程度；区域规模、跨区物流具体实现；保底经济的套利防护；算力可复现的计费；完整终局条件；核心区围困破局设计。详见 `08-APPENDIX/OPEN_QUESTIONS_AND_ADRS.md`。
