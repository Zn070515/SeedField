# 证据：Godot 外交面板（2026-11-17）

关联：ADR-0007；实现批次 `feat(sim): SeedField 条约与贸易协议`。

## 1. 面板

- `F3` 打开/关闭 `DiplomacyPanel`，与 F1 程序编辑器、F2 工坊互斥；打开时暂停。
- 面板只操作文明 A，按钮通过事件交给 `Main`，由 `ExecuteDiplomacyCommand` 执行。
- 区块：
  - 通信/库存状态行（A↔B 路径、frontier 的 A/B M/E）；
  - 条约操作：`PROPOSE NAP`、`PROPOSE TRADE`、`BREAK SELECTED`；
  - 生效条约、待处理提议（`IN`/`OUT`）、贸易单（`IN`/`OUT`）；
  - 接受/拒绝提议、接受/拒绝/取消贸易单；
  - 创建贸易单：Give M/E、Want M/E 四个数值框；
  - 状态/错误行与 `CLOSE F3`。
- A 发起的提议/贸易单等待 B 响应；B 的自动外交仍是后续工作。

## 2. 场景与截图

- `Scenarios.BuildWorld` 为 A/B 各增加 200 区域能源库存，便于演示 E 贸易。
- `--capture-diplomacy` 预置：
  - A-B 贸易协定生效；
  - B → A 的 NAP 提议；
  - B → A 的贸易单（give M40/E20，want M30/E10）；
  - A → B 的贸易单（give M25/E10，want M15/E5）。
- 截图：`game/SeedField.Game/screenshots/m0-diplomacy.png`。

## 3. 验证

```powershell
dotnet build SeedField.slnx
dotnet build game/SeedField.Game/SeedField.Game.csproj
dotnet test SeedField.slnx
godot --path game/SeedField.Game -- --capture-diplomacy
godot --path game/SeedField.Game -- --diplomacy-smoke
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Simulation.Tests: 136 passed
SeedField.Runtime.Js.Tests: 25 passed
```

```text
SEEDFIELD_READY args=--capture-diplomacy
SEEDFIELD_PRERUN tick=39 shots=3 entities=3
SCREENSHOT_SAVED .../screenshots/m0-diplomacy.png (Ok)
```

```text
DIPLOMACY_SMOKE treaties=2 offers=0 A=M380/E210 B=M410/E190 settled=M70/E30 ok=True
```

`--diplomacy-smoke` 通过面板事件处理函数完成：接受 B 的 NAP 提议、接受 B 的贸易单、取消 A 的贸易单；检查条约数、贸易单数、双方库存总量与成交统计。

## 4. 未覆盖

- B 侧自动外交/AI 谈判；A 发起的提议不会自动被 B 接受。
- B 侧调试按钮、国家选择器、条约续签。
- 面板的鼠标点击自动化测试；本批用截图与 smoke 路径验证。
