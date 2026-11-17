# TESTING — 分层测试与结构门禁

> 参考 ArtFlow 的 structural gates/golden fixtures 思路，结合 SeedField 的
> 固定 Tick、确定性哈希、Jint 与 Godot 分层整理。
> 决策：`ADR-0023`；日常命令：`dotnet test SeedField.slnx`；
> CI 见 `.github/workflows/ci.yml`。

## 1. 测试分层

| 层 | 位置 | 作用 |
|---|---|---|
| 仓库结构门禁 | `tests/SeedField.Repository.Tests` | 篇幅、目标框架、Godot 隔离、ADR/链接、运行产物、测试质量、架构约束、包边界 |
| 仿真单元 | `tests/SeedField.Simulation.Tests` | 规则、确定性、存档往返、电网/恢复/外交/物流等 |
| 黄金场景 | `GoldenScenarioTests` | 固定世界 + 固定意图 + 固定哈希，防行为漂移 |
| 不变量 fuzz | `InvariantFuzzTests` | 固定种子随机场景，断言资源非负/容量/ID/存档哈希 |
| Jint/Worker | `tests/SeedField.Runtime.Js.Tests` | 沙箱、进程内控制器、IPC 帧、worker 生命周期、包校验、fuzz |
| 包信任 | `PackageHostTests` | 内容哈希、ECDSA 签名、信任库、显式同意、路径/大小/UTF-8 |
| Godot 冒烟 | `game/SeedField.Game` | `--save-smoke`、`--recovery-smoke`、截图；CI 不跑 Godot |

## 2. 结构门禁（会自动失败）

| Gate | 规则 |
|---|---|
| 文件篇幅 | `.cs` / `.md` ≤ 600 行 |
| 目标框架 | 所有 `.csproj` 为 `net10.0` |
| 分层 | `SeedField.Simulation` 不引用 Godot / Runtime.Js / 任何 ProjectReference |
| 仿真确定性 | Simulation 源码禁止 `System.Random`、`new Random(`、`DateTime.Now`、`Stopwatch`、`Thread.Sleep`、`Task.Delay`、`double/float/decimal`、`Godot` |
| 测试质量 | 禁止 `Skip=`、`Thread.Sleep`、`Task.Delay`、无种子 `new Random()`、墙钟时间、`Assert.True(true)`、`Assert.False(false)`、`#pragma warning disable` |
| 运行产物 | `git ls-files` 不得包含 bin/obj/logs/TestResults/.godot/密钥/数据库/日志/zip/`__pycache__`/`*.pyc` |
| `.gitignore` | 必须继续覆盖 bin/obj/logs/TestResults/.godot/`__pycache__`/`*.pyc` 等根目录 |
| 美术资产 | 清单 schemaVersion 3；PNG 解码校验 alpha 覆盖率/留边/`alphaBBox`/pivot/方向帧/pass/SHA-256；`python tools/build_art_manifest.py --check` 防清单过期 |
| ADR | 每个 ADR 被 README 或实现基线链接；ADR-0013+ 必须有 `## 非目标` 与 `## 后果` |
| 文档链接 | 本地 Markdown 链接必须解析到文件/目录 |
| 计划 | `docs/superpowers/plans/*.md` 必须有 Verification 与 Rollback，且无 TBD/TODO |
| 包边界 | `Runtime.Js/Opponents` 禁止文件/网络/进程/程序集 API；PackageHost 禁止网络/进程 API；`ThirdPartyImportEnabled=false` |

## 3. 黄金场景

`GoldenScenarioTests.ExpectedHash` 是刻意固定的：

1. 行为改变导致测试失败时，先确认变化是预期的；
2. 用失败输出中的 `Actual` 更新 `ExpectedHash`；
3. 在同一提交里说明为什么改变；
4. 不要为了让测试通过而改场景输入而不说明。

当前基准：`0x77e84c31f71f63ba`（40 Tick，移动/等待/攻击/冷却；v18 战斗规则字段加入哈希）。

性能基线：`docs/engineering/evidence/perf-baseline.json` + `tools/check_perf.py`
用于固定机长窗口（avg/p95 +25%）回归；CI 只保留宽松 `--budget-ms` 门禁。

## 4. 不变量 fuzz

- 固定种子 `1..30`，每个场景 20 Tick，随机 move/draw/sendEnergy/transfer。
- 每 Tick 断言：区域材料/能量非负、核心储备在容量内、实体能量/生命在范围内。
- 结束断言：`NextEntityId > max entity id`，存档往返哈希一致。
- 新增资源字段时，把对应不变量加到这里，而不是只加一个 happy-path 测试。

## 5. Worker / 包测试

- Worker 测试依赖已构建的 `src/SeedField.Worker/bin/{Debug|Release}/net10.0/SeedField.Worker.exe`。
  `ProjectReference ReferenceOutputAssembly=false` 会把 apphost 复制到测试输出但不复制 dll；
  `WorkerLocator` 会检查同目录 dll 并回退源码树。
- 覆盖：正常意图、同 Observation 确定性、无限循环限额故障、非法脚本启动失败、
  Dispose 杀进程、Windows Job Object 分配。
- IPC：帧往返、超长/截断/非法 UTF-8、随机帧/随机 envelope fuzz。
- 包：签名/内容哈希/信任库/同意/路径/额外文件/UTF-8/大小。

## 6. CI

```powershell
dotnet restore SeedField.slnx
dotnet build SeedField.slnx -c Release --no-restore
dotnet test SeedField.slnx -c Release --no-build --collect:"XPlat Code Coverage"
dotnet run --project src/SeedField.Headless -c Release --no-build -- --worker-opponent
dotnet run --project src/SeedField.Headless -c Release --no-build -- --package-check tests/fixtures/unsigned-package --allow-unsigned --confirm
```

- 覆盖率收集为 `coverage.cobertura.xml` artifact；`tools/check_coverage.py` 是
  棘轮门禁：Simulation ≥80% 行/65% 分支，Runtime.Js ≥70%/55%，
  PackageHost ≥70%/60%。只允许上调，不要静默下调。
- Worker 哈希必须与进程内一致；失败即 CI 红。
- 包检查只验证/同意，不执行；输出必须包含 `EXECUTION_DISABLED`。

## 7. 本地 Godot 冒烟

```powershell
dotnet build game/SeedField.Game/SeedField.Game.csproj
godot --headless --path game/SeedField.Game -- --save-smoke
godot --headless --path game/SeedField.Game -- --recovery-smoke
godot --headless --path game/SeedField.Game -- --power-breaker-smoke
godot --path game/SeedField.Game -- --capture-recovery
```

Godot 不在 CI 中运行；改 UI 后必须实机截图检查，不能只看代码。

## 8. 新增测试的默认顺序

1. 写失败测试（或可复现日志）；
2. 加最小实现；
3. 更新不变量/黄金场景（如果是行为变更）；
4. 跑 `dotnet test SeedField.slnx`；
5. 确认 `.cs`/`.md` 不超过 600 行；
6. 提交说明里写实际命令与结果。
