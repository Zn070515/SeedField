# ADR-0018：离线文明废墟快照

- 状态：**Accepted（原型语义；导入参数为场景/宿主配置）**
- 日期：2026-11-17
- 关联决策：D-02、D-03、P-02、O-01
- 前置：ADR-0017

## 背景

D-02 要求 PvE 对手来自其他玩家的离线文明版本，P-02 禁止自建游戏服务器。调研
（`03-Resources/游戏设计_核心区恢复围困与离线对手调研.md`）中最接近的做法是
RimWorld **Real Ruins**：匿名基地快照 → 随机圆形切片 → 老化/掠夺 → 生成遗迹。

本 ADR 落地**最小离线废墟管线**：本地 JSON 快照，数据-only，不引入可执行内容。

## 决定

### 1. 快照是数据，不是包

`RuinSnapshotData` / `RuinSnapshotEntry` 只包含：

- `schemaVersion`（当前 1）；
- `sourceCivilizationId`（≤64 字符，可为匿名令牌）；
- `widthMm` / `heightMm`；
- `entries[]`：`blueprintId`、`blueprintVersion`、`x`、`y`、`materialValue`。

不包含：控制器代码、模块定义、脚本、文件路径、任意 CLR/网络访问。导入方不会执行
快照中的任何内容；未知蓝图只会变成普通残骸。

### 2. 校验与限额

- JSON 长度 ≤ 1,000,000 字符；
- 条目 ≤ 2,000；
- 标识符 ≤ 64 字符；
- 坐标必须在快照尺寸内；
- `materialValue` 0..1,000,000,000；
- schema 必须精确匹配当前版本；
- 失败抛 `RuinSnapshotException`，宿主命令返回 `RuinImportRejected`。

第三方可执行包导入仍然关闭；本功能只接受数据文件。

### 3. 导出

`RuinSnapshotSerializer.Capture(world, civilizationId, regionId)`：

- 仅收集该区域、该文明的、有已发布蓝图版本的实体；
- `materialValue` 取蓝图的 `MaterialCost`（0 成本实体跳过）；
- 快照尺寸取区域尺寸；
- 输出不包含玩家账号/路径/时间戳。

### 4. 导入管线

`ImportRuinCommand(CivilizationId, RegionId, SnapshotJson, CenterX, CenterY,
RadiusMm, AgingPercent, LootPercent)`：

1. 目标区域必须存在且**不是核心区**（保护永久种子，R-01）。
2. 校验快照。
3. 用世界 RNG 在快照内选一个随机切片中心（确定性、进哈希）。
4. 保留距切片中心 ≤ `RadiusMm` 的条目。
5. 平移到目标中心；落在目标区域外的条目丢弃。
6. 老化：`material = material × (100 - AgingPercent) / 100`（0..90）。
7. 掠夺：`material = material × (100 - LootPercent) / 100`（0..100）。
8. 结果 ≤0 的条目丢弃；其余生成 `WreckState`：
   - 原主 = 快照 `sourceCivilizationId`（通常不是本局玩家，因此只能被在场文明搜刮）；
   - 蓝图引用保留，便于诊断/UI；
   - 可回收 M 使用老化/掠夺后的值。
9. 发一个 `RuinImported` 事件（切片条目数、生成残骸数、总材料）。

- 导入只创建残骸，不创建可运行实体/控制器。
- 残骸的回收复用 ADR-0017：原主远程、其他文明需在场。
- 导入不会修改核心区、永久种子或保底再生。

## 存档与哈希

- 废墟本身落为既有 `WreckState`，已由 ADR-0017 的 v16 存档覆盖。
- 快照文件不进入世界存档；导入结果（残骸、`NextWreckId`）进入存档/哈希。
- 导入消耗的世界 RNG 状态进入存档/哈希，重放一致。
- ADR-0019 的维护成本使当前 schema 为 v17；本 ADR 不新增世界字段。

## 非目标

- 在线快照服务器、账号、自动下载或 Steam Workshop 流程。
- 导入蓝图定义、控制器、模块或可执行包。
- 占地区域、废墟实体（非残骸）、废墟清理/占领。
- 快照加密、签名、来源认证；当前只做 schema/大小/数值校验。
- 废墟 UI 浏览器；导入由宿主/工具调用。

## 后果

- 玩家可以把本地导出的快照文件分享给他人，导入后只得到老化/掠夺过的残骸。
- 恶意 JSON 不能执行代码；最坏结果是大量残骸/材料，受条目数与上限约束。
- 导入是确定性命令：同世界、同 RNG 状态、同快照 → 同残骸。
- 默认没有自动导入入口；需要宿主显式提供快照文件与导入参数。
