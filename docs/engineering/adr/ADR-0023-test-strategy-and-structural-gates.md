# ADR-0023：测试分层、黄金场景与结构门禁

- 状态：**Accepted**
- 日期：2026-11-17
- 关联决策：T-01、T-02、O-01
- 前置：ADR-0002、ADR-0022

## 背景

测试数量增长后，单靠“跑通测试”不足以防止：

- 文件/架构在无人注意时退化（超长文件、Simulation 依赖 Godot、测试跳过）；
- 行为在重构中静默改变（没有固定场景哈希）；
- 随机/浮点/真实时钟重新进入仿真核心；
- 文档、ADR、计划与代码脱节；
- 覆盖率在新增代码后缓慢下降。

ArtFlow 的 structural gates/golden fixtures/coverage ratchet 提供了可借鉴模式。
本 ADR 固化 SeedField 的测试分层与门禁。

## 决定

### 1. 测试分层

| 层 | 位置 | 作用 |
|---|---|---|
| 仓库结构门禁 | `tests/SeedField.Repository.Tests` | 篇幅、目标框架、分层、ADR/链接、运行产物、测试质量、包边界 |
| 仿真单元 | `tests/SeedField.Simulation.Tests` | 规则、确定性、存档、电网/恢复/外交/物流 |
| 黄金场景 | `GoldenScenarioTests` | 固定世界/意图/哈希，防行为漂移 |
| 不变量 fuzz | `InvariantFuzzTests` | 固定种子随机场景，断言资源/容量/ID/存档哈希 |
| Jint/Worker | `tests/SeedField.Runtime.Js.Tests` | 沙箱、IPC、worker 生命周期、包校验、fuzz |
| Godot 冒烟 | `game/SeedField.Game` | 保存/恢复/截图；CI 不跑 Godot |

详解见 [`docs/engineering/TESTING.md`](../TESTING.md)。

### 2. 结构门禁（自动失败）

- `.cs`/`.md` ≤ 600 行；所有项目 `net10.0`。
- `SeedField.Simulation` 不引用 Godot/Runtime.Js，且无 ProjectReference。
- Simulation 源码禁止 `System.Random`、`new Random(`、`DateTime.Now`、
  `Stopwatch`、`Thread.Sleep`、`Task.Delay`、`double/float/decimal`、`Godot`。
- 测试禁止 `Skip=`、`Thread.Sleep`、`Task.Delay`、无种子 `new Random()`、
  墙钟时间、`Assert.True(true)`、`Assert.False(false)`、`#pragma warning disable`。
- `git ls-files` 不得包含 bin/obj/logs/TestResults/.godot/密钥/数据库/日志/zip；
  `.gitignore` 必须继续覆盖这些根目录。
- 每个 ADR 被 README/实现基线链接；ADR-0013+ 必须有 `## 非目标`/`## 后果`。
- 本地 Markdown 链接必须解析；计划必须有 Verification/Rollback 且无 TBD/TODO。
- `Runtime.Js/Opponents` 禁止文件/网络/进程/程序集 API；PackageHost 禁止网络/进程 API；
  `ThirdPartyImportEnabled=false`。

### 3. 黄金场景

- `GoldenScenarioTests` 固定 40 Tick 场景，当前哈希
  `0x4624154b5bc8c8d6`。
- 行为变化导致失败时，必须在同一提交更新常量并说明原因；不得只改输入掩盖变化。
- 另有一条双跑一致测试，防止场景本身不确定。

### 4. 不变量 fuzz

- 固定种子 1..30，每个 20 Tick，随机 move/draw/sendEnergy/transfer。
- 每 Tick 断言区域库存非负、核心储备在容量内、实体能量/生命在范围内。
- 结束断言 ID 单调、存档往返哈希一致。
- 新增资源字段时必须扩不变量，而不是只加 happy-path 测试。

### 5. 覆盖率棘轮

- `tools/check_coverage.py` 解析 Cobertura：
  - Simulation ≥80% 行/65% 分支；
  - Runtime.Js ≥70%/55%；
  - PackageHost ≥70%/60%。
- 只允许上调；下调需在 ADR/提交中说明原因。
- CI 收集并上传 `coverage.cobertura.xml` artifact。

### 6. CI

- `dotnet test` 带 `--collect:"XPlat Code Coverage"`；
- 覆盖率门禁；
- `--worker-opponent` 双跑哈希一致；
- `--package-check` fixture（未签名 + 显式同意）；
- Godot 冒烟保持本地执行，CI 不运行。

## 非目标

- 完整 mutation testing 平台（Stryker）与 100% 覆盖率。
- 引入 property-based testing 框架（当前用固定种子 fuzz）。
- 在 CI 中运行 Godot（导出模板/显示环境未就绪）。
- 性能基准/内存基准门禁。
- 自动生成 ADR/测试映射。

## 后果

- 结构退化会在 `dotnet test` 中直接失败，而不是靠 review 记忆。
- 黄金场景让“看起来无害”的重构改变行为时立即显形。
- 覆盖率棘轮防止新增代码稀释核心覆盖，但阈值保持保守，避免阻塞开发。
- 门禁本身需要维护；调整阈值/规则应走 ADR 或明确提交说明。
