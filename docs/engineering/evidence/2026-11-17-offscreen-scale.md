# 证据：离屏控制器抑制与区域规模

- 日期：2026-11-17
- 决策：ADR-0033
- 命令：

```powershell
dotnet run --project src/SeedField.Headless -- --region-scale
```

## 输出

```text
REGION_SCALE regions=8 devicesPerRegion=50 ticks=20 activeOnlyMs=1.19 allActiveMs=5.04 ratio=4.25
```

- 只激活 1 个区域：1.19 ms/tick；
- 8 个区域全激活：5.04 ms/tick；
- 比例 4.25×，说明离屏控制器/Observation 抑制有效；`Step` 的全局潮流/物流
  仍占一部分。

## 语义

- 非活动区的 Intent 本来就返回 `RegionInactive`；跳过 `decide` 不改变权威状态；
- 脚本内部计时器在离屏期间暂停，激活后继续（会话语义）；
- 发电已按区域 active/inactive 跳过并在激活时补算；其他被动系统仍全局运行。

## 测试

- `RegionActivationTests.InactiveRegionEntitiesAreNotActiveForControllers`：
  实体不存在/区域停用返回 false，激活后返回 true。
- 既有 `RegionActivationTests` 覆盖 Intent 拒绝与发电补算。

## 未做与风险

- 未做按区域跳过潮流/断路器/消息/物流/核心再生：跨区域耦合，跳过会改变状态与
  事件；需要逐系统 catch-up 的独立 ADR。
- 离屏控制器暂停意味着脚本内部状态不会按 Tick 推进；依赖真实时间/隐藏计数的
  脚本不应在非活动区运行。
