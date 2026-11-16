# 证据：核心区保护进入世界规则（2026-11-16）

## 实现

- `CoreProtection.IsProtectedFrom(world, regionId, civilizationId)`：核心区只允许在该区登记核心的文明。
- `DeploymentService.Deploy`：外文明部署进核心区抛出 `CORE_PROTECTED`。
- `SimulationEngine.ProcessIntent`：外文明实体在核心区内的任何意图返回 `CoreProtected`。
- `WorldSaveSerializer.FromData`：核心区内出现外文明实体时抛 `FOREIGN_ENTITY_IN_CORE`。
- 核心区保底再生、最新五分支产品、`TryBootstrapFromCore`/`TryRestoreLatestProduct` 保持原语义。

## 构建与测试

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 71 passed
SeedField.Runtime.Js.Tests: 14 passed
```

新增覆盖：

| 用例 | 断言 |
|---|---|
| 外文明部署 | `CORE_PROTECTED`，核心区无新实体 |
| 外文明行动 | `CoreProtected`，位置不变 |
| 己方行动 | 核心区内的己方实体可移动 |
| 己方部署 | 可部署进自己的核心区 |
| 存档 | 含外文明核心实体的存档加载失败 |

## 仍开放

- 恢复成本由调用方传入，尚未走统一恢复命令/配额。
- 核心区出口围困、多出口/缓冲区与防套利规则。
- 敌对实体进入核心区的路径目前只靠部署/加载拦截；未来若加入跨区移动，必须在移动验证中复用 `CoreProtection`。
