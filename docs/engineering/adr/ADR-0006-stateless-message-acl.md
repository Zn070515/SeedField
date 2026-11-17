# ADR-0006：无状态消息 ACL 与发送方身份边界

- 状态：**Accepted（原型语义）**
- 日期：2026-11-17
- 关联决策：D-01、O-01、OPEN-10
- 补充：ADR-0005
- 后续：ADR-0007（条约与贸易协议）

## 背景

跨文明消息已具备目标区域 Comm 端点检查与最小拒绝策略（发送方黑名单、话题黑名单、关闭入站）。仍缺少：

- 发送方白名单与话题白名单；
- 对“脚本伪造发送者身份”的可验证边界；
- 重复消息 ID / 重放的结构性校验；
- 断联与在途消息语义的回归测试。

外交、条约与贸易范围仍是 OPEN-10，本 ADR 不引入关系评分或条约状态，只收紧消息权限与身份边界。

## 决定

### 1. 策略模型（无状态，仅入站）

`MessagePolicyState` 增加：

| 字段 | 默认 | 语义 |
|---|---|---|
| `AllowAllInbound` | 由场景配置 | 发送方总开关 |
| `AllowAllTopics` | `true` | 话题总开关 |
| `BlockedCivilizations` | 空 | 发送方黑名单 |
| `AllowedCivilizations` | 空 | 发送方白名单 |
| `BlockedTopics` | 空 | 话题黑名单 |
| `AllowedTopics` | 空 | 话题白名单 |

判定顺序固定，**拒绝优先**：

1. 同文明消息直接允许（内部通信不受目标文明入站策略影响）。
2. 发送者在 `BlockedCivilizations` → 拒绝。
3. `AllowAllInbound=false` 且发送者不在 `AllowedCivilizations` → 拒绝。
4. 话题在 `BlockedTopics` → 拒绝。
5. `AllowAllTopics=false` 且话题不在 `AllowedTopics` → 拒绝。
6. 其余允许。

匹配使用 ordinal 精确匹配，不做前缀通配。拒绝统一返回 `MessageDenied`，detail 保持通用措辞，避免发送方枚举目标策略。

### 2. 发送方身份

- `MessageEnvelope.FromCivilizationId` 只能由引擎从 `EntityState.CivilizationId` 派生。
- JS 意图 schema 不接受发送者字段；脚本附带 `fromCivilizationId` 不会进入意图。
- 意图的 `IssuerCivilizationId` 与实体文明不匹配时，沿用 `UnknownEntity` 拒绝，避免 ID 探测。

### 3. 重放与重复

- 消息 ID 在“在途消息 + 所有收件箱”范围内必须唯一；读档发现重复返回 `SaveFormatException("DUPLICATE_MESSAGE_ID")`。
- `NextMessageId` 必须大于所有已存消息 ID。
- 同一 payload 重发是**新消息**，不做内容去重；去重会吞掉合法通信，且需要额外配额规则，超出本 ADR。

### 4. 断联与在途消息

- 发送时仍要求目标文明在目标区域有 Comm 端点、存在链路路径；失败返回 `NoRoute`，且不扣能量。
- 消息在发送 Tick 即提交；之后链路或端点被删除，仍在 `DeliverTick` 投递。这是原型语义，用测试固定，避免“半路消失”与存档分叉。

### 5. 存档与可复现

- 世界存档 schema v5 → v6：策略记录新增 `AllowAllTopics`、`AllowedCivilizations`、`AllowedTopics`。
- 旧存档缺失字段按 `true` / 空集合迁移。
- 新字段进入 `WorldStateHasher`。

## 非目标

- 条约、关系评分、外交提议/接受、贸易结算（OPEN-10）。
- 消息签名、密钥、重放窗口或序号认证。
- 第三方可执行包权限；仍受 ADR-0005 与进程隔离约束。
- `Observation` 暴露目标文明的 ACL；设备只看到自己的收件箱。

## 后果

- 场景可以用白名单收紧入站通信，黑名单始终可覆盖白名单。
- 身份伪造在意图层与信封层都有结构性阻断，并有不泄漏隐藏 ID 的回归测试。
- 读档不会因重复消息 ID 静默产生双份投递。
- “发送即提交”的断联语义是原型决定；若未来改为链路失效丢弃，必须新增 ADR 并迁移在途消息。
