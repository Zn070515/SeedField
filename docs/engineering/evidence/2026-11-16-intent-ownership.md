# 证据：意图归属与目标可见性加固（2026-11-16）

## 问题

语义审查发现：`SimulationEngine.Step` 接收扁平意图，只按实体 ID 查找；不可信 JS 可以控制其他文明实体，并可攻击传感器之外的隐藏目标，还可能用错误码探测隐藏 ID。

## 修复

- `Intent.IssuerCivilizationId`：Jint 控制器解析每条意图时写入自己的文明。
- `SimulationEngine.ProcessIntent`：
  - 发起文明与实体归属不一致 → `UnknownEntity`（与“不存在”同码，避免 ID 探测）。
  - 实体所在区域非活动 → `RegionInactive`。
- `VisibilityRules`：与 Observation 共用传感器规则；攻击未感知目标 → `TargetUnknown`。
- 新增测试：
  - `IntentOwnershipTests.ControllerCannotCommandAnotherCivilizationsEntity`
  - `CombatTests.AttackAgainstAnUnsensedTargetIsRejected`

## 验证

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 67 passed
SeedField.Runtime.Js.Tests: 14 passed
```

## 未覆盖

- 文明级传感器融合仍绕过通信拓扑（审查 R3）。
- 核心区保护、部署命令统一入口（R4/R5）。
- 存档 schema 迁移（R7）。
