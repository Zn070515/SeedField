# ADR-0009：主动提议与多轮反提议谈判

- 状态：**Accepted（原型语义）**
- 日期：2026-11-17
- 关联决策：D-01、D-02、O-01、OPEN-10
- 前置：ADR-0007、ADR-0008

## 背景

ADR-0008 只让 B 被动响应；A 的报价不可接受时只能拒绝，B 也不会主动提出条约或贸易。本轮由用户要求实现**B 主动提议**与**多轮谈判**。

## 决定

### 1. 引擎级反提议

- `TradeOfferState` 新增：
  - `NegotiationId`：同一轮谈判的稳定 ID。
  - `Round`：从 1 开始，每次反提议 +1。
- 新命令：
  ```text
  CounterTradeCommand(civ, offerId, giveM, giveE, wantM, wantE, expiresInTicks)
  ```
- 规则：
  1. 只有原单目标方能反提议；否则 `TradeNotFound`。
  2. 需要通信路径与生效 Trade 条约。
  3. 需要反提议方在结算区域有足够 `Give*` 库存。
  4. `Round >= 3` 时拒绝，返回 `NegotiationExhausted`。
  5. 应用：退款并移除原单 → 托管反提议方的给出物 → 创建同 `NegotiationId`、`Round + 1` 的新单，提案方与目标方互换。
  6. 新单区域沿用原单区域。
- 普通 `ProposeTradeCommand` 创建的贸易单：`NegotiationId = OfferId`、`Round = 1`。
- 新事件 `TradeCountered`；观察与 Jint 暴露 `negotiationId`、`round`。
- 世界存档 schema v7 → v8；v7 旧单迁移为 `NegotiationId = Id`、`Round = 1`。

### 2. B 主动提议

- 画像扩展：是否主动 NAP/Trade、是否主动下单、提议间隔、下单条款、让步步长。
- 策略仍是纯函数；按 `World.Tick % interval == 0` 触发，间隔 240 Tick。
- 同一 Tick 若已产生响应命令，不再主动发起；避免同一 Tick 既反提议又下单。
- 主动提议优先缺失的 NAP（画像启用时），否则缺失的 Trade；没有生效 Trade 时不主动下单。
- 主动下单查找第一个 B 有足够给出物库存的区域；已有涉及 B 的贸易单时跳过。

| 画像 | 主动 NAP | 主动 Trade | 主动下单 | 反提议让步 |
|---|---|---|---|---|
| Skirmisher | 否 | 是 | give M20 / want M30 | 每轮 -10% |
| Turtle | 是 | 是 | give M20 / want M20 | 无 |
| 默认 | 是 | 是 | 公平单 | 无 |

### 3. B 反提议

- 可接受 → 接受；不可接受且 `Round < 3` → 反提议；否则拒绝。
- 本批只对**单资源**报价（纯 M 或纯 E）生成反提议；混合报价直接拒绝。
- 有效阈值：
  `max(100, MinimumTradeValuePercent - (Round - 1) * CounterConcessionPercent)`
- 反提议条款：B 给出原单 `WantX`，要求 `ceil(WantX * 有效阈值 / 100)` 的同种资源。
- B 在结算区域库存不足或没有可生成条款 → 拒绝。

### 4. 面板

- 贸易单列表显示 `R{round}/3`。
- 新增 `COUNTER SELECTED`：用现有 Give/Want 数值框作为反提议条款。
- 反提议失败时显示引擎错误码与消息。

## 非目标

- 混合资源反提议的 AI 条款生成。
- 谈判记忆、信誉、威胁评估、超过 3 轮。
- 第三方包自定义画像。
- 消息驱动谈判或延迟提议。

## 后果

- 谈判可以多轮收敛：Skirmisher 从 120% 逐轮让步到 110%、100%；Turtle 始终公平。
- 反提议会退款原单并重新托管，资源不会凭空产生或消失。
- 单资源限制是原型边界；混合报价需要后续的价值分配规则。
- 主动提议是 Tick 节奏的纯函数，不引入隐藏状态；读档后行为可复现。
