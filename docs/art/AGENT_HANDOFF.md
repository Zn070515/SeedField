# 给 Coding Agent 的执行指令：SeedField 美术专项迭代

> 使用此任务时，Agent 必须真实操作用户本地的 SeedField 工程；不得把本文档当成已实施的代码或完成证据。

## 任务目标

**先把《种子域》的表现层做成可以评价的游戏画面，再继续大规模扩展玩法。**

根据已确认的 `main` 提交（审查时为 `1d5e580eaeecbf3a4df681a1a6249c934c6bd3d3`）检查现状；如 main 已更新，应先重新对照差异，不能覆盖后来的改动。

本轮只进行美术生产、视觉 UX 与表现层拆分。**不修改 V1.9 Rev2 的世界规则，不动存档 schema、SimulationCore、Jint 安全与离线 PvE 契约。**

## 开始前

1. 阅读仓库 `AGENTS.md`、`CLAUDE.md`、`docs/engineering/IMPLEMENTATION_BASELINE.md`、`docs/design-bible/v1.9-rev2/05-PRESENTATION/ART_DIRECTION_AND_VFX.md`。
2. 阅读本包 `docs/art/MAIN_AUDIT_2026-11-17.md`、`VISUAL_LANGUAGE_V0_1.md`、`PIPELINE_ASSET_CONTRACT.md` 与 `VERTICAL_SLICE_EXECUTION.md`。
3. 记录 main SHA、当前 CI 状态、实测 Godot 截图和可用 Blender 版本；建立独立 `art/` 分支，不直接改 main。

## 首个工作批次（必须先验收再扩张）

A. 把现有 `m0-combat`、`m0-recovery`、`m0-workshop` 存为 before 参考。

B. 完成两套同内容的视觉样片：**斜俯视正交** vs **严格俯视正交**；每组包含种子核心、一台模块工体、一个能源设施和对应地形。

C. 核心与设备应由可编辑 Blender 源资产制作，保留固定摄影机、材质、灯光、渲染导出配置；严禁仅用 prompt 生出无源文件的成品图替代。

D. 用真实 Godot 同场景比较，分别提交：默认、选择对象、战术覆盖/调试三个状态，以及远/中/近缩放截图。

E. 描述两方案的实际代价：单资产制作耗时、方向帧数、导出 PNG 总量、内存/帧时。成本未知则标注未知。

F. 只有用户批准视觉路线后，再渐进实现 `EntityVisual / WorldView / TacticalOverlay / CombatVfx`，保留 M0 原型画面作为诊断模式。

## 强制验收

- 在正常视距不看文字也能区分核心、能源设施和至少两个用途不同的单位。
- 设备模块变更能在实体外观体现，而不只是 UI 数值改变。
- 核心围困、断电、通信中断、开火/命中均能从真实 `WorldState`/`SimulationEvent` 解释；不得可视化隐藏情报。
- 打开/关闭调试层不影响 Headless hash 与 Tick 结果。
- 保留现有 F1/F2/F3/F4/F6 的工作路径；真实 Godot 截图而非编辑器截图/网页 Mockup。
- 不用 AI 动画素材或花哨特效掩盖缺乏功能造型的问题。
- 不允许无 Blender 源/未知授权美术进入正式 `assets/runtime`。

## 交付报告

```text
工作分支和基准 main SHA：
已确认设计 / 仍属提议：
源资产路径、导出脚本与调用命令：
真实 Godot 运行截图/录屏路径：
A/B 相机方案对比：
实际运行/性能/测试命令及结果：
是否变更 SimulationCore/存档/JS 契约（应为否）：
未通过的视觉问题：
下一步（保留/修改/回滚）：
```

## 开发纪律

- 不要出现“先随便做一大堆再统一美化”的路径。
- 不要一次性重做所有 UI；优先玩家世界、实体、工坊预览。
- 遇到选择风格的关键问题，输出两套真实样片交给用户，而不是自己把视觉方向冻结。
- 测试没有运行就是 `NOT RUN`，不能写 `PASS`。
