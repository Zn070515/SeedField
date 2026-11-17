# 证据：主动提议与多轮反提议（2026-11-17）

关联：ADR-0009、ADR-0008、ADR-0007。

## 1. 引擎

- `TradeOfferState` 新增 `NegotiationId`、`Round`；普通贸易单为 `Round = 1`、`NegotiationId = OfferId`。
- `CounterTradeCommand`：
  - 只有原单目标方能反提议；
  - 需要通信路径、生效 Trade 条约与足够给出物库存；
  - 退款并移除原单，创建同 `NegotiationId`、`Round + 1` 的新单；
  - `Round >= 3` 返回 `NegotiationExhausted`。
- 新事件 `TradeCountered`；观察/Jint 暴露 `negotiationId`、`round`。
- 世界存档 schema v7 → v8；v7 旧单迁移为 `NegotiationId = Id`、`Round = 1`。

## 2. B 主动提议与反提议

- 画像新增主动 NAP/Trade/下单、间隔、下单条款、让步步长。
- 纯函数按 `Tick % 240 == 0` 触发；已有响应命令时不再主动发起。
- Skirmisher：主动 Trade、主动下单 give M20 / want M30、每轮让步 10%。
- Turtle：主动 NAP/Trade、主动下单 give M20 / want M20、不让步。
- 反提议只处理单资源报价：B 给出原单 `WantX`，要求 `ceil(WantX × 有效阈值 / 100)` 的同种资源。
- 混合报价直接拒绝；库存不足或回合耗尽时拒绝。

## 3. 面板

- 贸易单显示 `R{round}/3`。
- 新增 `COUNTER SELECTED`，使用 Give/Want 数值框作为反提议条款。

## 4. 验证

```powershell
dotnet build SeedField.slnx
dotnet build game/SeedField.Game/SeedField.Game.csproj
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --diplomacy-ai
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --diplomacy-negotiation
godot --path game/SeedField.Game -- --diplomacy-counter-smoke
godot --path game/SeedField.Game -- --capture-diplomacy
godot --path game/SeedField.Game -- --capture-diplomacy-ai
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Simulation.Tests: 143 passed
SeedField.Runtime.Js.Tests: 48 passed
```

```text
DIPLOMACY_AI turtleNap=True aggressiveNapRejected=True fairAccepted=True counterRaised=True counterAccepted=True ok=True
DIPLOMACY_NEGOTIATION proactiveNap=True napActive=True proactiveTrade=True tradeActive=True proactiveOffer=True rounds=2 counterAccepted=True ok=True
```

```text
DIPLOMACY_COUNTER_SMOKE round=2 settledM=42 ok=True
```

截图：
- `game/SeedField.Game/screenshots/m0-diplomacy.png`：贸易单 `R1/3` 与 `COUNTER SELECTED`。
- `game/SeedField.Game/screenshots/m0-diplomacy-ai.png`：Skirmisher 反提议为 `give M30/E0 → want M36/E0 · R2/3`，面板状态显示“offer countered at R2”。

## 5. 未覆盖

- 混合资源反提议的 AI 条款生成。
- 完整议价记忆、信誉、威胁评估。
- 超过 3 轮。
- 第三方包自定义画像。
- 主动提议的拒绝记忆；A 拒绝后 B 可能在下一个节奏再次提议。
