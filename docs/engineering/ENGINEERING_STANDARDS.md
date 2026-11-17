# ENGINEERING STANDARDS

> 适用：所有 SeedField 代码、文档、CI 与发布。  
> 日常操作速查见 [`CONTRIBUTING.md`](../../CONTRIBUTING.md)。

## 1. 真实来源

| 层 | 权威 |
|---|---|
| 产品/玩法决策 | `docs/design-bible/v1.9-rev2/` + `DECISION_REGISTER.md` |
| 世界状态 | `SeedField.Simulation` |
| 脚本执行 | `SeedField.Runtime.Js`（Jint 4.18.0） |
| 表现 | `game/SeedField.Game`，只消费 `SimulationEvent` |
| 工程状态/证据 | `docs/engineering/` |

- `[CONFIRMED]` 不得静默改写；技术阻断先写 ADR。
- `[PROPOSED]` 必须有替换能力，不得固化为商业规则。
- 任何跨文档冲突登记到 `OPEN_QUESTIONS_AND_ADRS.md`。

## 2. 分支与提交

- `main` 始终可构建、可测试；短期分支见 CONTRIBUTING。
- 提交格式：`<type>(<scope>): <摘要>`，首行 ≤72 字符。
- 只提交一个逻辑变更；生成物、日志、存档、密钥不入库。
- 本地 hook：`pwsh tools/setup-git-hooks.ps1`。
- CI 校验提交首行与 PR 依赖风险。

## 3. 验证与证据

最低门禁：

```text
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

每次交付必须说明：

1. 改了什么、对应哪条决策。
2. 实际执行的命令与输出。
3. 测试数量/哈希/性能/截图。
4. 未验证假设与风险。
5. 下一步或回滚方式。

**CI 绿 ≠ 游戏可玩；未执行的测试不能标成通过。**

分层测试（单元、黄金场景、不变量 fuzz、worker/包、Godot 冒烟）与结构门禁（篇幅、
架构、ADR/链接、运行产物、测试质量）见 [`TESTING.md`](TESTING.md)。

## 4. 确定性

- 仿真位置与结算使用整数；不使用浮点。
- 实体、消息、事件使用稳定 ID 与规范顺序。
- 随机性必须存入世界状态并进入哈希。
- 固定初始状态 + 输入 → 相同状态哈希序列。
- 不以渲染 FPS 或真实 CPU 毫秒决定结算。

## 5. Schema 与版本

| Schema | 当前 | 规则 |
|---|---|---|
| 世界存档 | v1 | 新增字段放末尾并给默认值；未知版本拒绝 |
| 游戏存档（含控制器） | v1 | 与世界 schema 独立版本化 |
| 蓝图版本 | 每个蓝图单调递增 | 已发布版本不可变 |
| JS API | 未定 | 修改需语义版本与错误提示 |

- schema 变更必须带往返/迁移测试。
- 保存**已部署**程序，不保存编辑器未部署文本。
- 破坏性变更走 ADR。

## 6. 依赖与安全

- 运行时 NuGet 依赖需记录原因；Dependabot 每周检查。
- Godot 版本固定 4.7.2 Mono；.NET 目标 `net10.0`。
- Jint 限制是降级防护，不是 OS 沙箱。
- 第三方离线文明包导入在进程隔离、IPC 校验、资源限额完成前保持关闭。
- 漏洞通过 GitHub Security Advisories 私密报告。

## 7. 发布

- 版本形如 `0.x.y-mN`；未到商业发布前不承诺日期。
- tag 只从 `main` 打；发布前必须：全量测试、保存迁移、Godot 真实截图、导出模板验证、CHANGELOG 更新。
- 发布产物必须可追溯到提交 SHA 与依赖版本。

## 8. 文件篇幅与自动 Lint

- 单个 `.cs` 文件 ≤ **600 行**；单个 `.md` 文件 ≤ **600 行**。
- 超限按职责拆分：仿真/存档等大类用 `partial class` 按领域分文件，测试按场景分组，
  不按行数硬切。
- 由 `tests/SeedField.Repository.Tests` 自动校验，`dotnet test SeedField.slnx` 即执行：
  - C# / Markdown 行数上限；
  - 所有项目目标框架为 `net10.0`；
  - `SeedField.Simulation` 不得引用 Godot；
  - `docs/engineering/adr/` 命名与编号唯一；
  - 设计基线 `MANIFEST.json` 的文件清单、size、SHA-256 与实际文件一致。
- 文档改动后运行 `python tools/refresh_design_manifest.py`，再跑测试确认清单同步。

## 9. 评审清单

- [ ] 没有第二套仿真/战斗/资源逻辑
- [ ] 没有绕过 `Observation / Intent / Validation`
- [ ] 没有把演示数值当作已确认平衡
- [ ] 没有新增未隔离的第三方代码执行路径
- [ ] 测试、证据、文档同步更新
- [ ] 回滚方式明确
