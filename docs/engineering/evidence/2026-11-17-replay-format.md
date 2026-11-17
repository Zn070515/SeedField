# 证据：确定性回放格式与逐 Tick 校验

- 日期：2026-11-17
- 决策：ADR-0030
- 命令：

```powershell
dotnet run --project src/SeedField.Headless -- --replay-record logs/replay-demo.json
dotnet run --project src/SeedField.Headless -- --replay-play logs/replay-demo.json
```

## 演示输出

```text
REPLAY_RECORDED path=logs/replay-demo.json ticks=20 bytes=10191 hash=0x2bc626aab35b6fdf
REPLAY_OK ticks=20 events=39 hash=0x2bc626aab35b6fdf
```

- 场景：固定种子 2026，命中 60%、伤害 ±40%、掉落 100%/5M；
- 录制 20 Tick 攻击意图，重放后最终哈希与事件数一致；
- 回放文件 10,191 bytes。

## 格式

- `schemaVersion = 1`；
- `initialWorld`：世界存档 v18 JSON（字符串）；
- `ticks[]`：严格递增的 Tick 与 Intent DTO；
- `tickHashes[]`：每个 Tick 执行后的 64 位世界哈希；
- 上限：16 MiB / 100,000 Tick / 每 Tick 10,000 Intent。

## 校验行为

| 情况 | 结果 |
|---|---|
| 正常回放 | `Success=true`，返回最终哈希/Tick 数/事件数 |
| 篡改某 Tick 哈希 | `Success=false`，`DivergedTick=该 Tick` |
| Tick 与初始世界不对齐 | `ReplayFormatException`（`REPLAY_TICK_MISMATCH`） |
| 不支持 schema / 非法 JSON / 超限 | `ReplayFormatException` |
| Tick 与哈希数量不一致 | `ReplayFormatException` |
| 空回放 | 返回初始世界哈希，成功 |

## 测试

`tests/SeedField.Simulation.Tests/ReplayTests.cs`（7 个）：

- 录制/保存/加载/重放战斗随机场景；
- 篡改哈希定位分叉 Tick；
- 拒绝不支持 schema；
- 拒绝 Tick/哈希数量不一致；
- 空回放；
- 全部 IntentKind 编解码往返。

## 未验证与风险

- 回放包含完整初始世界，大世界体积会增长；压缩/分片未做。
- 哈希不是密码学签名；来源认证需要独立 ADR。
- 回放只验证仿真状态，不验证脚本内部行为；脚本确定性需要额外的
  Observation/Intent 哈希（ADR-0020 目标）。
- 回放格式与世界存档 schema 耦合，世界存档升级需要回放回归。
