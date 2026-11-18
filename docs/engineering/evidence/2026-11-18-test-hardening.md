# 测试体系加强证据（2026-11-18）

## 变更

- 经济 golden fixture：`tests/fixtures/economy/smelting-recipes.v1.json` +
  `EconomyGoldenFixtureTests`（规则与一 Tick 产出）。
- 经济不变量 fuzz：`EconomyInvariantFuzzTests`（种子 1..20，采矿/冶炼/生产开关）。
- 损坏存档矩阵：`SaveValidationMatrixTests`（负矿库存、重复/未知发现、非法模块参数）。
- 展示覆盖门禁：`PresentationCoverageTests`（事件 filter/日志/Describe、白名单只减不增）。
- 意图契约门禁：`IntentContractTests`（IntentKind ↔ Jint ↔ TypeScript）。
- ADR 结构门禁：ADR-0040+ 必须有 `## 验证` 与 `## 回滚`。
- 加载器新增实体 Smelter/Fabricator/Armor/Shield 参数校验。

## 全量测试

```powershell
dotnet test SeedField.slnx
```

结果：**500 通过 / 0 失败**

- 仿真 346
- Runtime.Js 124
- 仓库门禁 22
- Catalog 8

## 负向探针（证明门禁会失败）

1. 把 `SmeltingRecipes` 铜配方改为 `2 M / 1 Ore`：
   `EconomyGoldenFixtureTests` 失败（Expected 4 / Actual 8），随后恢复。
2. 删除 `PlayerEventFilter` 的 `OreMined` 分支：
   `PresentationCoverageTests` 报 `OreMined: missing PlayerEventFilter decision`，随后恢复。

两次探针均按预期失败，恢复后全量测试重新通过。

## ArtFlow 对照

- 采用其 **结构门禁 + golden fixture + 属性/不变量 + 逐字段破坏** 分层。
- 采用“规则变更显式改 fixture、实现回归不得迁就 fixture”的纪律。
- 采用“白名单只减不增”的缺口棘轮思路（事件展示豁免）。
- 未引入其 Kubernetes/HTTP/PostgreSQL 部署门禁与浏览器 E2E；SeedField 以
  Godot 冒烟/截图与 Headless 哈希替代。

## 边界

- 未引入属性测试库；fuzz 仍是固定种子手写场景。
- 事件/意图门禁是源码结构检查，不替代领域行为测试。
- 未做变异测试或覆盖率阈值上调。
