# 证据：B 方自动外交响应（2026-11-17）

关联：ADR-0008、ADR-0007。

## 1. 策略

- `OpponentDiplomacyPolicy.Decide` 是纯函数：输入世界、双方文明 ID、通信路径与画像，输出宿主 `DiplomacyCommand`。
- 只处理 B 是 target、A 是 proposer 的待处理条约提议与贸易单，按 ID 稳定排序。
- 无通信路径时不响应；不读取隐藏信息；不修改世界。
- 条约：NAP/Trade 分别由画像开关决定接受/拒绝。
- 贸易单：要求生效 Trade 条约、B 有足够库存，并按
  `receivedValue * 100 >= paidValue * MinimumTradeValuePercent`
  判断；原型价值权重 M=1、E=1。
- 画像：
  - `builtin.skirmisher`（Aggressive）：拒绝 NAP，接受 Trade 条约，贸易需 ≥120% 收益。
  - `builtin.turtle`（Cautious）：接受 NAP/Trade，贸易 ≥100%。
  - 未知包默认 Cautious。

## 2. Godot 接入

- `Main.StepOnce` 在仿真 Step 后运行策略；返回的命令立即交给 `ExecuteDiplomacyCommand`。
- 产生的事件写入现有事件日志，新增条约/贸易事件的描述与颜色。
- 画像从对手包 ID 选择，读档后重建；不新增存档字段。
- 面板仍只操作 A；面板暂停期间 B 不行动，恢复运行后的下一个 Tick 响应。

## 3. 验证

```powershell
dotnet build SeedField.slnx
dotnet build game/SeedField.Game/SeedField.Game.csproj
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --diplomacy-ai
godot --path game/SeedField.Game -- --diplomacy-ai-smoke
godot --path game/SeedField.Game -- --capture-diplomacy-ai
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Simulation.Tests: 136 passed
SeedField.Runtime.Js.Tests: 37 passed
```

```text
DIPLOMACY_AI turtleNap=True aggressiveNapRejected=True fairAccepted=True unfairRejected=True ok=True
```

```text
DIPLOMACY_AI_SMOKE opponent=builtin.skirmisher tradeAccepted=True offerAccepted=True napAccepted=False expectNapAccepted=False napHandled=True settledM=50 ok=True
DIPLOMACY_AI_SMOKE opponent=builtin.turtle tradeAccepted=True offerAccepted=True napAccepted=True expectNapAccepted=True napHandled=True settledM=50 ok=True
```

截图：`game/SeedField.Game/screenshots/m0-diplomacy-ai.png`。画面显示 Skirmisher 接受 Trade 条约、接受 30M/20M 贸易单、拒绝 NAP；事件日志与面板状态行均可见。

## 4. 未覆盖

- B 主动提议、主动下单、多轮讨价还价。
- 信誉、记忆、威胁评估、人格对话、情报推理。
- 第三方包自定义外交画像。
- 复杂价值模型；当前 M/E 等权且仅按数量估值。
