# 证据：语义审查剩余项收口（2026-11-16）

## 收口项

| 项 | 结果 |
|---|---|
| R3 设备级知识 | 新增 `ObservationBuilder.BuildForEntity` + `DeviceControllerSlot`；Godot 切片改为每设备控制器与设备观察 |
| R5 统一部署 | 新增 `DeployCommand` / `UpgradeCommand` / `ExecuteDeploymentCommand`；工坊 UI 不再直接调用 `DeploymentService` |
| R6 E/C/S/B 近似 | ADR-0003 固化原型语义，明确非冻结 |
| R7 存档迁移 | schema 升 v2；接受 v1 并迁移；世界与游戏存档均有 v1→v2 测试 |
| R8 摧毁资源 | `EntityInventoryLost` + `EnergyLostTotal` / `MaterialLostTotal`，进入存档与哈希 |
| R9 消息知识 | 设备观察按目标区域过滤消息/物资/能源；切片控制器不再读取文明级全量消息 |
| R10 离屏性能 | ADR-0004 明确 active 仅语义 gating，不宣称离屏优化 |

## 构建与测试

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 84 passed
SeedField.Runtime.Js.Tests: 17 passed
```

新增覆盖：

- 设备观察：侦察体可见敌人、无传感器友军不可见；消息按设备区域过滤。
- 设备控制器：跨设备意图被丢弃（`DEVICE_SCOPE`）；错误 viewer 的观察被拒绝。
- 部署命令：成功事件、核心区拒绝、升级版本事件、跨文明升级拒绝。
- 存档 v1→v2：世界与游戏存档均哈希一致；未知版本仍拒绝。
- 摧毁库存：损失事件、统计与能量守恒等式。

## Godot 切片

```powershell
godot --path game/SeedField.Game -- --save-smoke
godot --path game/SeedField.Game -- --capture
godot --path game/SeedField.Game -- --capture-editor
```

```text
SEEDFIELD_PRERUN tick=39 shots=3 entities=3
SAVE_SMOKE ok=True before=8f50ddb2d2b54659 mutated=d2aad66c312d9faf after=8f50ddb2d2b54659 tick=39 A=v1 B=v1
```

- 每个实体独立 Jint 控制器，只接受自己的设备观察。
- 通用脚本只命令 `obs.viewerEntityId`。
- 截图：`screenshots/m0-combat.png`、`screenshots/m0-program-editor.png`。

## 剩余边界

- `ObservationBuilder.Build`（文明级）仍保留给工具/旧测试/命令级 AI；游戏运行路径已使用设备观察。后续应标记为 legacy 或移除。
- 核心区恢复命令与配额仍未统一。
- 离屏 Tick 预算/增量加载仍未实现（ADR-0004）。
