# ADR-0030：确定性回放格式与逐 Tick 校验

- 状态：**Accepted**
- 日期：2026-11-17
- 关联决策：ADR-0014（确定性随机）、ADR-0023（黄金场景）、ADR-0029（战斗随机）
- 前置：世界存档 v18、固定 Tick、世界状态哈希

## 背景

设计基线要求存档/回放/调试能追溯程序与结构版本；确定性随机与战斗随机加入后，
需要一个不依赖墙钟、可定位分叉 Tick 的回放格式。现有黄金场景只做进程内双跑，
没有可持久化的输入记录。

## 决定

### 1. 回放以 Intent 为边界

回放文件只记录：

- 初始世界存档（`WorldSaveSerializer.Save` 的 v18 JSON）；
- 每个 Tick 提交的 Intent 列表；
- 每个 Tick 执行后的 `WorldStateHasher` 哈希。

不记录控制器代码、Observation、事件或表现层状态。重放时用相同 Intent 序列驱动
`SimulationEngine.Step`，世界状态与随机流由初始存档恢复。这样回放不执行任何
不可信代码，也不依赖脚本 runtime。

### 2. 格式

```json
{
  "schemaVersion": 1,
  "initialWorld": "<WorldSaveData JSON>",
  "ticks": [
    { "tick": 0, "intents": [ { "kind": "attack", "entityId": 1, "targetId": 2 } ] }
  ],
  "tickHashes": [ "0x..." ]
}
```

- Intent DTO 覆盖当前全部 `IntentKind`：move/attack/send/wait/transfer/
  sendEnergy/drawEnergy；未知类型拒绝；
- 数字/字符串字段显式；issuer 可选（宿主/测试可为 null）；
- 上限：16 MiB、100,000 Tick、每 Tick 10,000 Intent；
- 存档 JSON 内嵌为字符串，保持数据-only；不压缩、不签名。

### 3. 录制与重放规则

- `ReplayRecorder` 在 `Step` **之后**记录当前世界哈希；
- Tick 必须严格递增；重放时每个记录 Tick 必须等于世界当前 Tick，否则拒绝；
- `ReplayPlayer` 逐 Tick 重放并比较哈希；第一个不一致的 Tick 作为
  `DivergedTick` 返回，后续停止；
- 成功时返回最终哈希、Tick 数、事件总数；
- 空回放合法，返回初始世界哈希。

### 4. 命令

- `--replay-record <path>`：固定战斗随机场景运行 20 Tick，写回放文件；
- `--replay-play <path>`：加载并重放；成功退出 0，分叉退出 1，文件/格式错误
  抛出 `ReplayFormatException` 或退出 2。

### 5. 安全与确定性

- 回放文件是数据，不包含可执行内容；解析只走显式 DTO；
- 哈希用于一致性/篡改检测，不是密码学签名；需要来源认证时另立快照签名 ADR；
- 回放依赖世界存档 schema；世界存档升级后，旧回放按存档迁移规则加载，
  但回放格式自身有独立 `schemaVersion`。

## 非目标

- 不重放控制器内部状态或重新执行脚本；脚本差异应通过 Intent/事件比对。
- 不记录/回放表现层特效、音频、相机或 UI。
- 不做压缩、分片、网络 lockstep、延迟补偿或作弊证明。
- 不做签名/来源认证（另行 ADR）。

## 后果

- 回放文件包含完整初始世界，大世界会较大；后续需要压缩/分片。
- 回放只验证仿真状态，不验证脚本行为；脚本回放需要额外的 Observation/Intent
  哈希（ADR-0020 的目标）。
- 逐 Tick 哈希会暴露第一个分叉点，便于调试但与回放格式版本耦合。

## 验证

- `ReplayTests`（7 个）：
  - 战斗随机场景录制/保存/加载/重放，最终哈希与事件数一致；
  - 篡改某个 Tick 哈希 → 第一个分叉 Tick 被报告；
  - 不支持的回放 schema、Tick/哈希数量不一致被拒绝；
  - 空回放返回初始哈希；
  - 全部 IntentKind 编解码往返。
- `--replay-record` / `--replay-play` 演示：20 Tick、10,191 bytes、哈希一致。

## 回滚

移除 ReplayData/Recorder/Player 与两个 CLI 命令即可；世界存档、仿真与脚本契约
不变，回放文件是独立数据，不需要迁移。
