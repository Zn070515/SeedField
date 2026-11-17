# 证据：无状态消息 ACL、身份边界与断联回归（2026-11-17）

关联：ADR-0006、ADR-0005、O-01、OPEN-10。

## 1. 入站策略

- `MessagePolicyState` 新增 `AllowAllTopics`、`AllowedCivilizations`、`AllowedTopics`。
- 判定顺序固定为：黑名单 > 白名单 > 默认开关；ordinal 精确匹配。
- 同文明消息不经过目标文明入站策略。
- 拒绝仍返回 `MessageDenied`，detail 不区分“被拉黑”与“不在白名单”。

## 2. 发送方身份

- `MessageEnvelope.FromCivilizationId` 由引擎从实体派生；`SendMessageIntent` 没有发送者字段。
- 脚本附带 `fromCivilizationId` 不进入意图；伪造 `IssuerCivilizationId` 仍按 `UnknownEntity` 拒绝，避免 ID 探测。

## 3. 重复消息与断联

- 读档时消息 ID 必须在“在途 + 所有收件箱”中唯一，否则 `DUPLICATE_MESSAGE_ID`。
- 无端点/无链路发送返回 `NoRoute`，且在扣能量之前拒绝。
- 消息在发送 Tick 提交；链路随后删除仍在 `DeliverTick` 投递（原型语义，已由测试固定）。

## 4. 存档

- 世界存档 schema v5 → v6；旧策略缺失字段按 `AllowAllTopics=true`、空允许列表迁移。
- 新字段进入 `WorldStateHasher`；`AllowListsSurviveSaveLoad` 验证往返哈希一致。

## 5. 验证

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --message-acl
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Simulation.Tests: 113 passed
SeedField.Runtime.Js.Tests: 24 passed
```

```text
MESSAGE_ACL senderAllowed=True senderDenied=True topicAllowed=True topicDenied=True noRoute=True energy=99 inboxB=1 inboxC=0 hashOk=True ok=True
```

```text
ticks: 600
final: tick=600 entities=1 energySpent=423 shots=52 damage=414 destroyed=2 messagesDelivered=39
Run 1 final hash: 4ca65e0d83768a38
Run 2 final hash: 4ca65e0d83768a38
REPRODUCIBLE: identical state hash across both runs.
```

## 6. 未覆盖

- 条约、关系评分、贸易结算（OPEN-10）。
- 消息签名、密钥、序号认证与重放窗口。
- 第三方可执行包权限；仍需 OS 级进程隔离与 IPC 校验。
- 目标文明 ACL 不进入 `Observation`；设备只能从拒绝结果感知策略，不能读取策略详情。
