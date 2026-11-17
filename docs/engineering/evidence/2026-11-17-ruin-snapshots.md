# 证据：离线文明废墟快照（2026-11-17）

关联：ADR-0018、ADR-0017。

## 1. 快照与校验

- 数据-only：`schemaVersion`、来源文明、尺寸、条目（蓝图 ID/版本/坐标/材料值）。
- 限额：JSON ≤ 1,000,000 字符、条目 ≤ 2,000、ID ≤ 64、材料 ≤ 1e9。
- 无控制器、模块、路径、网络或可执行内容。
- 实测：捕获→序列化→加载往返条目一致；格式错误、非法 schema、超大 JSON 被拒。

## 2. 导入管线

- 目标区域必须非核心区。
- 世界 RNG 选择切片中心 → 半径内条目 → 平移到目标中心 → 老化/掠夺 → 生成残骸。
- 残骸原主 = 快照来源文明；回收复用 ADR-0017（原主远程/他人在场）。
- 发 `RuinImported`；不创建可运行实体。
- 实测：

```text
RUINS captured=1 imported=1 material=40 owner=offline-A hashOk=True ok=True
```

- 材料 100、老化 20%、掠夺 50% → 残骸 40 M。
- 老化 90% + 掠夺 100% → 不生成残骸。
- 核心区导入被拒（`RuinImportRejected`）。
- 导入结果残骸随存档往返哈希一致。

## 3. 存档

- 废墟落为既有 `WreckState`，由 v16 存档覆盖；本功能不新增世界字段。
- 导入消耗的世界 RNG 状态进入存档/哈希，重放一致。

## 4. 验证命令与结果

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --ruins
```

```text
已成功生成。0 个警告，0 个错误
SeedField.Repository.Tests: 6 passed
SeedField.Simulation.Tests: 220 passed
SeedField.Runtime.Js.Tests: 48 passed
```

## 5. 未覆盖

- 在线快照服务器/自动下载/Workshop。
- 导入蓝图定义或可执行包（仍关闭）。
- 废墟实体、占领、清理、废墟 UI。
- 快照签名/来源认证。
