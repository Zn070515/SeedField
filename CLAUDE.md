# CLAUDE.md — SeedField 操作约束

本文件是 Claude Code 在本仓库的工作规则。产品与工程规则的权威入口是
`AGENTS.md`、`docs/design-bible/v1.9-rev2/` 和
`docs/engineering/IMPLEMENTATION_BASELINE.md`；本文件只补充日常执行方式。

## 开工前

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

- 先读 `AGENTS.md`、`docs/engineering/IMPLEMENTATION_BASELINE.md`、
  `docs/design-bible/v1.9-rev2/00-START-HERE/DECISION_REGISTER.md`。
- 以实际代码、测试和运行结果为准，不以文件名或旧文档宣称完成。
- 未冻结内容必须标注 `[PROPOSED]` / `[VALIDATE]` / `[OPEN]`。

## 开工后

- 先写失败测试（或给出可复现日志），再改实现；新增规则必须带测试。
- 只让 `SeedField.Simulation` 修改世界状态；脚本、UI、着色器只读
  `Observation` 并提交 `Intent`。
- 保持固定 Tick、整数坐标、稳定 ID 与规范顺序；不引入浮点结算或真实时钟。
- 数值是场景配置，不得把演示参数写成正式平衡。
- 每次 schema 变更同步 `ToData` / `FromData` / 哈希 / 迁移测试。

## 文件篇幅与拆分（强制）

- 单个 `.cs` 文件 ≤ **600 行**；单个 `.md` 文件 ≤ **600 行**。
- 超限按职责拆分为 `partial class` 文件或独立类型，不按行数硬切。
- 规则由 `tests/SeedField.Repository.Tests/RepositoryConventionTests.cs` 自动校验；
  超限时 `dotnet test` 会直接失败并列出文件与行数。
- 同一测试还校验：所有项目 `net10.0`、仿真核心不引用 Godot、ADR 命名、
  设计基线 `MANIFEST.json` 的 size/SHA-256 与文件一致。
- 设计基线文档改动后运行 `python tools/refresh_design_manifest.py`。

## 验证与证据

最低门禁：

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

长任务把输出重定向到 `logs/*.log`，再用文件读取工具查看；不要给训练/长命令挂
`| tail` / `| head` 管道。

交付时按 `AGENTS.md` 的格式报告：变更目的、关联决策 ID、受影响 Schema、修改文件、
构建命令、实际测试与结果、性能/内存、未验证风险、下一步/回滚方式。未运行的测试
不得写成通过。

## 提交

- 提交信息：`<type>(<scope>): SeedField <变更目的>`，首行 ≤72 字符。
- 一个提交只包含一个逻辑变更；日志、存档、生成物不入库。
- 提交前运行 `dotnet test SeedField.slnx`；只提交已验证的结果。
- 推送前确认 `git status` 干净且远端为 `origin/main`。

## Windows 注意事项

- 调用 Python 子进程时设置 `PYTHONIOENCODING=utf-8`，避免 GBK 乱码。
- 优先使用 `Glob` / `Grep` / `Read`，不要用 `find` / `grep` / `cat` 做仓库检索。
- Godot 项目引用的是 Simulation DLL 副本；改仿真后重建
  `game/SeedField.Game/SeedField.Game.csproj` 再运行截图/冒烟。
