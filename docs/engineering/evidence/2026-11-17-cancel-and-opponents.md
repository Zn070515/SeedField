# 证据：建造取消/退款与对手选择（2026-11-17）

## 建造取消/退款

- `CancelConstructionCommand(civ, orderId)` 走 `ExecuteDeploymentCommand`。
- 订单保存 `MaterialPaid`；取消时移除订单并把材料退回区域库存。
- 统计 `MaterialRefundedTotal` 进入存档/哈希；产生 `ConstructionCancelled`。
- 非本人订单拒绝；到期后不能取消（订单已不在队列）。
- 未实现：部分退款、取消手续费、施工残骸、UI 队列。

## 对手选择

- `BuiltInOpponents.Skirmisher` / `Turtle` 均为受信任包。
- Godot 启动参数 `--opponent-turtle` 选择 Turtle；默认 Skirmisher。
- HUD 显示包 ID/版本；B 方设备控制器使用所选包程序。
- 未实现：包选择写入存档、对手难度/协议配置。

## 协议边界

- `ADR-0005` 明确跨文明消息当前只有“目标区 Comm 端点”约束，没有身份认证、条约、话题 ACL 或第三方权限。

## 验证

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 100 passed
SeedField.Runtime.Js.Tests: 23 passed
```

```text
CONSTRUCTION_CANCEL ok=True orders=0 material=100 refunded=20

Skirmisher SAVE_SMOKE ok=True before=fc5ef2e3ccce9446 ... after=fc5ef2e3ccce9446 tick=39
Turtle      SAVE_SMOKE ok=True before=e522098b49638d0a ... after=e522098b49638d0a tick=51
```

## 未覆盖

- 建造取消 UI/队列列表。
- 对手包选择持久化到存档。
- 跨文明协议/情报权限（ADR-0005）。
