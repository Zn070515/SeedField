# 证据：抢占式取电、断路器维护与仓库 Lint（2026-11-17）

关联：ADR-0013、ADR-0012、ADR-0011、ADR-0010。

## 1. 抢占式优先级

- 意图处理顺序：`PowerModule.Priority` 降序 → 实体 ID → 调用方输入顺序。
- 返回的 `IntentResults` 仍按输入顺序排列，观察/意图契约不变。
- 同优先级时与旧的实体 ID 顺序完全一致，默认场景行为不变。
- 实测（`PowerMaintenanceTests`）：实体 1 优先级 0、实体 2 优先级 10，电网仅 5E；
  实体 2 先取电并移动成功，实体 1 返回 `NoEnergy`；结果顺序仍是 `[1, 2]`。

## 2. 断路器维护锁定

- `PowerGridState`：`TripCount`、`MaintenanceRequired` 进入存档与哈希。
- `PowerPolicy`：`BreakerTripLimit`（0 = 不锁定）、`BreakerMaintenanceMaterialCost`。
- 故障跳闸累计；达到上限后锁定，自动合闸与 `CloseBreakerCommand` 都被阻止。
- `MaintainBreakerCommand` 扣区域材料、清零计数、解除锁定，但**不合闸**。
- 实测：trip limit 2 + 维护成本 7 →
  第一次跳闸计数 1；补充 1E 自动合闸；再次抽空后第二次跳闸锁定；
  `CloseBreakerCommand` 返回 `BreakerMaintenanceRequired`；
  维护后材料 20→13、`MaterialSpentTotal=7`、锁定清除、断路器仍打开；
  补 5E 后手动合闸成功。

## 3. 表现

- F4 面板：摘要增加 `trip≤∞/N`、`maint XM`；区域行增加 `trips{n}` 与 `MAINT!`；
  按钮行新增 `MAINTAIN`。
- 事件日志：`BreakerLockedOut`、`BreakerMaintained`。
- 实机截图 `game/SeedField.Game/screenshots/m0-power.png` 已重新生成并人工检查：
  `MAINTAIN` 按钮、`trip≤∞`、`maint 0M`、`trips0` 均完整显示，无裁切。
- `--power-breaker-smoke` 扩展为同时验证维护命令接线。

## 4. 存档

- 世界存档 schema v11 → v12：
  `PowerGridStateSaveData.TripCount/MaintenanceRequired`、
  `PowerPolicySaveData.BreakerTripLimit/BreakerMaintenanceMaterialCost`。
- 旧 v11 存档迁移：跳闸计数 0、未锁定、无上限、维护免费。
- 哈希新增全部字段；策略/断路器按稳定键排序后参与哈希，避免字典插入顺序影响。

## 5. 篇幅控制与仓库 Lint

- 拆分前最大文件：`SimulationEngine.cs` 3717 行、`Main.cs` 1855 行、
  `WorldSaveSerializer.cs` 1445 行、`Program.cs` 1415 行、`PowerGridTests.cs` 903 行。
- 拆分后最大 `.cs` 文件 559 行；全部 `.cs` / `.md` 均 ≤ 600 行。
- 新增 `tests/SeedField.Repository.Tests`，`dotnet test SeedField.slnx` 自动执行：
  - `.cs` / `.md` 600 行上限；
  - 所有项目 `net10.0`；
  - `SeedField.Simulation` 不引用 Godot；
  - ADR 文件名 `ADR-NNNN-kebab-case.md` 且编号唯一；
  - 设计基线 `MANIFEST.json` 的清单、size、SHA-256 与文件一致。
- 负向验证：临时创建 610 行 `_LintProbe.cs`，Lint 失败并输出
  `src/SeedField.Simulation/_LintProbe.cs: 610 lines`；随后删除探针。
- 新增 `CLAUDE.md`，并把篇幅规则写入 `ENGINEERING_STANDARDS.md`。

## 6. 验证命令与结果

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --power-maintenance
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --power-breaker
godot --headless --path game/SeedField.Game -- --power-breaker-smoke
godot --headless --path game/SeedField.Game -- --save-smoke
godot --path game/SeedField.Game -- --capture-power
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Repository.Tests: 6 passed
SeedField.Simulation.Tests: 182 passed
SeedField.Runtime.Js.Tests: 48 passed
```

```text
POWER_MAINTENANCE firstTrip=True reclosed=True lockedOut=True closeBlocked=True maintained=True closed=True hashOk=True ok=True
POWER_BREAKER tripped=True drawBlocked=True heldBelowReserve=True autoClosed=True manualHeld=True manualClosed=True ok=True
POWER_BREAKER_SMOKE opened=True held=True closed=True maintained=True ok=True
```

```text
SAVE_SMOKE ok=True before=ac044d198e99e3b1 mutated=92c4580ae1582b1e after=ac044d198e99e3b1 tick=39 A=v1 B=v1
```

```text
Run 1 final hash: 7a2114e30b9bdd38
Run 2 final hash: 7a2114e30b9bdd38
REPRODUCIBLE: identical state hash across both runs.
```

拆分前后 `--` 主切片哈希均为 `7a2114e30b9bdd38`，证明文件拆分是纯重构。

## 7. 未覆盖

- 随机断路器故障概率、维护时长/工具链；需要 `DeterministicRandom` 进入世界状态。
- 维护后的自动重构/拓扑隔离。
- 多电压等级、潮流方程、无功功率。
- 大电网压力测试；当前验证规模仍为小场景与单区域演示。
