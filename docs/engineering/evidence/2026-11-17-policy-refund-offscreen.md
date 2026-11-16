# 证据：消息权限、失败退款、离屏发电预算（2026-11-17）

## 1. 跨文明消息权限

- `MessagePolicyState`：每文明可拒绝发送者、拒绝话题或关闭全部入站。
- 跨文明发送在目标 Comm 端点之后、扣能量之前检查策略；拒绝返回 `MessageDenied`，不产生消息。
- 策略进入存档（schema v5）与哈希。
- 仍没有身份认证、条约、消息签名或第三方包权限（ADR-0005）。

## 2. 建造失败退款

- `DeliverDueConstructions` 在蓝图版本缺失/校验失败时退还 `MaterialPaid`。
- `ConstructionFailed` 事件携带 `MaterialRefunded`；退款计入 `MaterialRefundedTotal`。
- 若目标区域已不存在则无法退款（返回 0），属于保守行为。

## 3. 离屏区域发电预算

- 非活动区跳过逐 Tick 发电；`ActivateRegion` 按 `World.Tick - 停用Tick` 补算。
- 消息/物资/能源投递、核心再生仍全局运行。
- `CatchUpInactiveRegions()` 在保存前补齐；Godot SaveGame 已调用。
- 活动/非活动两局在停用期间状态不同，激活补算后哈希一致。
- 补算上限 1,000,000 Tick（原型安全值）。

## 验证

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 100 passed
SeedField.Runtime.Js.Tests: 23 passed
```

```text
REGION_LOAD inactive=RegionInactive activeAccepted=True position=(500,500) ok=True
Skirmisher SAVE_SMOKE before=0c2b298af8180d74 ... after=0c2b298af8180d74 tick=39
Turtle      SAVE_SMOKE before=1964d51d9dcf7938 ... after=1964d51d9dcf7938 tick=51
```

## 未覆盖

- 完整分区 Tick 预算/增量加载（当前只延迟发电）。
- 消息身份/条约/话题白名单；第三方包权限。
- 建造失败残骸/部分退款。
