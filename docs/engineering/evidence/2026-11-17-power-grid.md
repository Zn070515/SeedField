# 证据：自动电力潮流与断供诊断（2026-11-17）

关联：ADR-0010、ADR-0004、E-01/E-02/E-03。

## 1. 电力链路与潮流

- `RegionLink` 新增 `PowerPerTick`（能量单位/Tick，默认 0）。
- 每个 Tick 在发电之后、意图之前运行自动潮流：
  - 按文明分别处理，连通分量由 `PowerPerTick > 0` 的链路构成；
  - 分量目标库存按 `total / count`，余数给库存最低的区域；
  - 盈余区到缺口区做确定性 BFS；路径上受共享链路剩余容量限制；
  - 多跳可一次传递；非活动区也参与被动潮流。
- 只移动区域电网库存，不改变设备本地储能；显式 `SendEnergyIntent` 语义不变。
- 新增统计 `EnergyFlowedTotal`；世界存档 schema v8 → v9。

## 2. 诊断与断供

- `PowerGridDiagnosticsBuilder.Build(world, civ)`：
  - 每区域电网库存、该文明发电潜力、本地能量/容量、连通区域、分量库存/区域数；
  - `IsBlackedOut = 分量库存 0 且分量内有该文明的 PowerModule 设备`。
- `PowerFlowed`：本 Tick 聚合的源区 → 缺口区流量。
- `PowerShortfall`：移动/攻击/发信/存能/取能因 `NoEnergy` 失败时产生，`source` 为 `local` 或 `grid`。
- 修复：诊断最初把同区域其他文明的发电/本地储能也算进 A 的电站；已按文明过滤并补回归测试。

## 3. 面板

- Godot `F4` 电网面板：汇总发电/电网/本地储能，区域列表、连通分量、停电标记、自动电力链路。
- `--capture-power` 截图；面板与 F1/F2/F3 互斥，打开时暂停。

## 4. 验证

```powershell
dotnet build SeedField.slnx
dotnet build game/SeedField.Game/SeedField.Game.csproj
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --power-grid
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj
godot --path game/SeedField.Game -- --capture-power
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Simulation.Tests: 156 passed
SeedField.Runtime.Js.Tests: 48 passed
```

```text
POWER_GRID balanced=True flowed=60 grid=90 regions=3 components=1 blackout=True shortfall=True ok=True
```

```text
Run 1 final hash: 84a14f0fc04a1201
Run 2 final hash: 84a14f0fc04a1201
REPRODUCIBLE: identical state hash across both runs.
```

截图：`game/SeedField.Game/screenshots/m0-power.png`。画面显示 `frontier · grid 0 · gen 4/t · local 9996/10000 · comp 1 · isolated · BLACKOUT`，说明电网无库存但本地缓冲仍可用。

## 5. 未覆盖

- 发电自动溢出到区域电网；设备自动从电网取电。
- 潮流延迟/损耗、链路移除命令、电池健康度。
- 观察/Jint 暴露完整电网状态；当前只给宿主诊断。
- 大电网性能基准；当前算法按分量 BFS，M0 规模未做压力测试。
