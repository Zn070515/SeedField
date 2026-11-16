# Contributing

## 开工前

1. 阅读 `AGENTS.md` 与 `docs/engineering/ENGINEERING_STANDARDS.md`。
2. 阅读相关设计条目与决策登记；`[CONFIRMED]` 不可静默改写。
3. 检查实际代码、测试与运行状态，不凭文件名假定完成。

## 本地设置

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
pwsh tools/setup-git-hooks.ps1
```

Godot 客户端：

```powershell
dotnet build game/SeedField.Game/SeedField.Game.csproj
godot --path game/SeedField.Game
```

> **改了 `SeedField.Simulation` 后必须重建 Godot 工程**。Godot 从 `game/SeedField.Game/bin/` 加载 `SeedField.Simulation.dll` 的副本；只跑测试不会更新这份副本。

## 分支

- `main`：唯一开发主线，始终可构建、可测试。
- 短期分支：`feat/*`、`fix/*`、`docs/*`、`test/*`。
- 不重写或强推共享历史；发布 tag 只从 `main` 打。

## 提交信息

格式：

```text
<type>(<scope>): <摘要>
```

- 允许类型：`feat`、`fix`、`docs`、`test`、`refactor`、`perf`、`build`、`ci`、`chore`、`revert`。
- `scope` 可选，小写：`sim`、`js`、`godot`、`save`、`workshop`、`economy`、`core`、`docs`、`ci`。
- 首行不超过 72 字符，不加句号；摘要可用中文。
- 一个提交只做一件逻辑上的事。
- 破坏性变更在 footer 写 `BREAKING CHANGE: ...`。

示例：

```text
feat(economy): 部署从区域库存扣除材料
fix(godot): 重建 Godot 引用的仿真程序集
docs(adr): 记录 .NET 10 运行时目标
```

本地 `commit-msg` hook 与 CI 都会校验首行；合并/回滚提交豁免。

## 验证门禁

提交前至少：

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

涉及以下内容时额外要求：

| 改动 | 额外证据 |
|---|---|
| 仿真规则 | 固定 Tick 哈希可复现；拒绝码稳定 |
| Godot/UI | 真实运行截图，不接受概念图 |
| 存档/蓝图 schema | 往返哈希一致 + 兼容/迁移测试 |
| 脚本运行时 | 死循环/越权/坏脚本失败测试 |
| 第三方包执行 | 必须已有 OS 级隔离与 IPC 校验 |

## 代码约束

- 只有 `SeedField.Simulation` 修改 `WorldState`；Godot、UI、Shader、脚本只读观察、提交意图。
- 不新增第二套战斗/资源/Tick 逻辑。
- 位置与结算保持整数/确定性；随机数必须可哈希、可复现。
- 不要把演示数值写成已确认平衡。
- 不提交密钥、`.env`、日志、`bin/`、`obj/`、本地存档或导出产物。
- 不开放任意第三方 JS 执行，直到安全隔离完成。

## PR

- 使用仓库 PR 模板；填写决策 ID、验证命令与真实输出、截图/哈希、风险。
- CI 必须绿；绿不代表可玩，仍需附运行证据。
- 涉及未决设计时先写 ADR，不要用代码替用户做不可逆决定。
