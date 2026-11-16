# 证据：对手选择持久化与建造队列 UI（2026-11-17）

## 对手选择持久化

- `GameSaveData` 增加 `OpponentPackageId` / `OpponentPackageVersion`；游戏存档 schema 升 v3。
- `SaveGame` 写入当前内置对手包；`LoadGame` 按包 ID 找回 `BuiltInOpponents` 并更新 HUD。
- 旧存档缺少元数据仍可加载；B 方脚本以存档控制器为准。
- 测试：`GameSavePersistsOpponentPackageMetadata`。

## 建造队列 UI

- 工坊面板新增 `A-SIDE CONSTRUCTION QUEUE` 列表与 `CANCEL SELECTED ORDER (FULL REFUND)`。
- 队列显示 order id / blueprint / 版本 / 完成 Tick / 已付 M。
- 取消走 `CancelConstructionCommand`，状态栏显示退款结果。
- 队列中部署在 `StepOnce` 完成时自动注册设备控制器。
- `power v1` 设为 `BuildTicks=2`，`--capture-workshop` 显示 queued 订单。

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
Skirmisher SAVE_SMOKE before=0c2b298af8180d74 ... after=0c2b298af8180d74 tick=39
Turtle      SAVE_SMOKE before=1964d51d9dcf7938 ... after=1964d51d9dcf7938 tick=51
```

截图：`game/SeedField.Game/screenshots/m0-workshop.png`（队列 `power v1 · ready t41 · M20`）。

## 未覆盖

- 跨文明协议/情报权限（ADR-0005 仍是边界说明）。
- 建造失败退款/残骸、队列多选/批量取消。
- 对手选择在“新开一局”时是否继承当前包的产品规则。
