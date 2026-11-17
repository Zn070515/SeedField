# 证据：条约与贸易协议（2026-11-17）

关联：ADR-0007、ADR-0006、O-01、OPEN-10。

## 1. 条约

- `TreatyKind.NonAggression`：生效期间跨文明攻击在可见性校验之后、冷却/能量之前被 `TreatyDenied` 拒绝。
- `TreatyKind.Trade`：创建/接受贸易单的前置条件。
- 提议/响应要求通信路径；撕毁是单边行为。
- 提议到期由 Tick 边界按 ID 稳定顺序清理；同一对文明同一种类最多一个生效条约/待处理提议。
- 所有签约/响应/撕毁走宿主 `ExecuteDiplomacyCommand`；脚本只能读观察。

## 2. 贸易

- 贸易单指定单个区域结算。
- 创建时托管提案方 `GiveMaterial`/`GiveEnergy`；不足则整单失败，不做半扣。
- 接受时先校验目标方 `Want*`，再一次性扣除与交换；不足不改变库存且贸易单保留。
- 拒绝/取消/到期全额退款；撕毁贸易协定会级联取消并退款。
- `TradeMaterialSettledTotal` / `TradeEnergySettledTotal` 记录成交的双向总量。

## 3. 存档与观察

- 世界存档 schema v6 → v7：条约、提议、贸易单、三个 ID 计数器与两项成交统计。
- v6 存档补空集合与零计数器。
- 观察新增 `treaties`、`treatyProposals`、`tradeOffers`；条约/提议按文明过滤，设备观察中的贸易单再按区域过滤。
- Jint JSON 只增字段；`obs.treaties[0].kind` 为字符串（如 `NonAggression`）。

## 4. 验证

```powershell
dotnet build SeedField.slnx
dotnet build game/SeedField.Game/SeedField.Game.csproj
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --diplomacy
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Simulation.Tests: 136 passed
SeedField.Runtime.Js.Tests: 25 passed
```

```text
DIPLOMACY napBlocked=True napBreakAttacked=True tradeSwapped=True refundOk=True hashOk=True ok=True
```

```text
Run 1 final hash: 6c8134c8f3981da1
Run 2 final hash: 6c8134c8f3981da1
REPRODUCIBLE: identical state hash across both runs.
```

## 5. 未覆盖

- Godot 外交面板；本批只做 Headless 与测试。
- 消息驱动谈判、延迟提议、AI 自动接受。
- 信誉、关税、制裁、情报共享、同盟、违约惩罚。
- 跨区域贸易物流；贸易只在指定区域结算。
- 消息签名/身份认证；仍按 ADR-0005/0006 的边界处理。
