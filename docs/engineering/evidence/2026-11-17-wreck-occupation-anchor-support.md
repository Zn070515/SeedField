# 证据：残骸占领与锚点支持

- 日期：2026-11-17
- 决策：ADR-0032
- 测试：

```powershell
dotnet test tests/SeedField.Simulation.Tests/SeedField.Simulation.Tests.csproj --filter "WreckRecoveryTests|AnchorSupportTests"
```

## 残骸占领

- 无在场实体：`OccupyWreckCommand("A", id)` → `WreckUnavailable`；
- 在场文明 B 占领：`WreckOccupied`，`WreckState.OccupiedByCivilizationId="B"`；
- 原主 A 远程回收被拒绝（被 B 占领）；
- B 回收成功，获得 50M，残骸移除；
- 存档往返后占领状态与哈希一致。

## 锚点支持

配置：

```text
EnergyPerTick=1, MaterialPerTick=1
AnchorEnergyPerTick=2, AnchorMaterialPerTick=3, AnchorStorageBonus=50
```

| 场景 | 结果 |
|---|---|
| 1 个存活锚点，1 Tick | E=3、M=4 |
| 上限 100 + bonus 50，3 Tick | E/M 封顶 150 |
| 无锚点 | E=1、M=1（旧行为） |
| 存档往返 | 三个配置字段保留 |
| v18 存档 | 三个字段默认 0 |

## 存档 / 哈希

- 世界存档 v18 → **v19**；
- `WreckSaveData` 增加占领者/占领 Tick；
- `CoreSaveData` 增加三个锚点配置；
- 占领状态与锚点配置进入 `WorldStateHasher`；
- 黄金场景无核心/残骸，哈希不变（`0x77e84c31f71f63ba`）。

## 风险

- 锚点产量/仓储是场景配置，正式版需要经济上限与平衡。
- 占领 UI 未接入；当前为宿主命令 + 事件。
- 残骸仍复用 `WreckState`，没有独立废墟美术/碰撞。
