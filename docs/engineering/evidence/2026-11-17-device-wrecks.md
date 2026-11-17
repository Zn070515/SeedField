# 证据：设备残骸与回收（2026-11-17）

关联：ADR-0017、ADR-0016、ADR-0015。

## 1. 残骸生成

- 世界规则 `WreckRecoveryPercent`（0..100，默认 0）。
- 战斗摧毁 `BlueprintId` 实例时，按 `floor(MaterialCost × percent / 100)` 生成残骸。
- 残骸保存区域/位置/原主/蓝图版本/可回收 M/创建 Tick；`NextWreckId` 自增。
- 无蓝图、0 材料或比例 0 时不生成。
- 实测：蓝图成本 100、比例 50 → 摧毁后残骸 50 M，事件 `WreckCreated`。

## 2. 回收

- `RecoverWreckCommand` + `ExecuteWreckCommand`。
- 原主可远程回收；其他文明必须在残骸区域有存活实体。
- 成功后材料进入区域库存，`MaterialSalvagedTotal` 累加，残骸移除，发 `WreckSalvaged`。
- 实测：
  - 原主回收：库存 0→50，残骸 0，统计 50。
  - C 方无实体：`WreckUnavailable`。
  - B 方（攻击者，仍在区域）：回收成功，库存 +50。
  - 未知残骸：`WreckNotFound`，无副作用。
  - 比例 0：不产生残骸。

## 3. 存档与哈希

- schema v15 → v16：`Wrecks`、`NextWreckId`、`WreckRecoveryPercent`、
  `StatisticsSaveData.MaterialSalvagedTotal`。
- 旧 v15 存档默认无残骸、比例 0、`NextWreckId=1`。
- 加载校验：ID 唯一/正数、区域与位置、材料非负、创建 Tick 不晚于存档 Tick、
  `NextWreckId` 大于所有 ID、比例 0..100。
- 实测：残骸存档往返哈希一致，`NextWreckId` 与比例保留。
- 主切片哈希因新增世界字段变为 `34886c1bb93164a1`（双跑一致）。

## 4. 事件与表现

- `WreckCreated` / `WreckSalvaged` 已加入 Godot 日志白名单、颜色与文案。
- 无独立回收面板；本 ADR 只做引擎规则与日志。

## 5. 验证命令与结果

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --wrecks
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj
godot --headless --path game/SeedField.Game -- --save-smoke
godot --headless --path game/SeedField.Game -- --power-breaker-smoke
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Repository.Tests: 6 passed
SeedField.Simulation.Tests: 209 passed
SeedField.Runtime.Js.Tests: 48 passed
```

```text
WRECKS created=True material=50 salvaged=True stock=50 salvagedTotal=50 remaining=0 hashOk=True ok=True
```

```text
Run 1 final hash: 34886c1bb93164a1
Run 2 final hash: 34886c1bb93164a1
REPRODUCIBLE: identical state hash across both runs.
```

```text
SAVE_SMOKE ok=True before=2cbc8b394b1e39d2 mutated=4a7fcd99784a81bb after=2cbc8b394b1e39d2 tick=39 A=v1 B=v1
POWER_BREAKER_SMOKE opened=True held=True closed=True maintained=True ok=True
```

## 6. 未覆盖

- 残骸老化/消失、占用区域、掠夺概率、自动回收。
- 离线文明废墟（快照→切片→老化→掠夺）。
- 恢复 UI 面板（配额/债务/锚点缺口/围困/MAINTAIN/RECOVER）。
- 正式回收比例仍 OPEN；当前为场景配置。
