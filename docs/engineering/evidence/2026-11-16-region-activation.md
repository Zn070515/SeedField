# 证据：区域活动与加载语义（2026-11-16）

## 实现

- `SimulationEngine` 增加会话级活动区域集合。
- API：`DeactivateRegion`、`ActivateRegion`、`SetActiveRegions`、`IsRegionActive`。
- 非活动区：
  - 控制器意图被拒绝，错误码 `RegionInactive`；
  - 发电、消息/物资/能源投递、核心再生等被动系统继续运行。
- 活动集不属于世界状态：不写入存档、不进入哈希、重建引擎后默认全部活动。

## 构建与测试

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 66 passed
SeedField.Runtime.Js.Tests: 13 passed
```

新增覆盖：

| 用例 | 断言 |
|---|---|
| 非活动区意图 | `RegionInactive`，位置不变 |
| 重新激活 | 同一移动意图成功到 `(500,500)` |
| 被动系统连续 | 活动/非活动两局各跑 60 Tick，被动发电上限与状态哈希一致 |
| 未知区域 | `DeactivateRegion` 抛 `ArgumentException` |

## Headless 演示

```powershell
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --region-load
```

```text
REGION_LOAD inactive=RegionInactive activeAccepted=True position=(500,500) ok=True
```

## 未覆盖

- 真正的离屏 Tick 预算/增量加载与性能收益。
- 区域卸载后跨区消息/物流在加载时的复杂因果。
- 活动集与 UI 区域切换的联动。
