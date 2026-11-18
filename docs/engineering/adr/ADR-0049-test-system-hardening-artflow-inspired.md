# ADR-0049：测试体系加强（ArtFlow 风格 golden/结构门禁/不变量）

- 状态：**Accepted**
- 日期：2026-11-18
- 关联决策：ADR-0023（测试分层与结构门禁）、ArtFlow `docs/gates/README.md` 与 ADR-0005
- 前置：448 项测试、黄金场景、不变量 fuzz、覆盖率棘轮

## 背景

对照 ArtFlow 的测试体系后，SeedField 的主要差距不是测试数量，而是**规则没有
被固化成可执行契约**：

- 经济配方只有单元测试，没有实现无关的 golden fixture；改数值时无法区分
  “规则变了”与“实现回归”。
- 新 `SimulationEvent` / `IntentKind` 靠记忆同步展示层与脚本契约，容易静默漏掉。
- 不变量 fuzz 只覆盖移动/能量/物流，没有覆盖采矿/冶炼/副产物。
- 恶意/损坏存档只验证了少数字段，模块参数缺少加载期校验。
- 最近 ADR 没有强制“验证/回滚”段，测试计划可能缺失。

## 决定

1. **经济 golden fixture**
   - `tests/fixtures/economy/smelting-recipes.v1.json` 记录每种矿的配方与一 Tick
     可观测量；`EconomyGoldenFixtureTests` 同时校验 `SmeltingRecipes` 与真实引擎。
   - 规则变化必须同提交更新 fixture；实现回归不能让 fixture 迁就。
2. **经济不变量 fuzz**
   - `EconomyInvariantFuzzTests` 固定种子 1..20，随机采矿/生产开关 40 Tick。
   - 断言：四类矿/M/构件/能量非负；矿脉余量不增且不越界；
     `OreMinedTotal == OreSmeltedTotal + 当前 Ore`；存档往返哈希一致。
3. **展示覆盖门禁**
   - `PresentationCoverageTests` 从 `SimulationEvents.cs` 提取全部事件；
   - 每个事件必须有 `PlayerEventFilter` 分支；
   - 非日志事件使用显式 `NonLogEvents` 白名单（只减不增：若已加入日志则失败）；
   - 日志事件必须在 `Describe` 中有 case。
4. **意图契约门禁**
   - `IntentContractTests` 枚举 `IntentKind`，要求每个成员有 wire name、
     Jint `case` 与 TypeScript 契约条目；新增意图漏改任一处即失败。
5. **最近 ADR 结构门禁**
   - ADR 编号 ≥ 40 必须包含 `## 验证` 与 `## 回滚`，保证新规则带测试计划与回退路径。
6. **损坏存档矩阵**
   - `SaveValidationMatrixTests` 逐字段破坏：负矿库存、重复/未知发现、非法
     Smelter 偏好、非法 Fabricator 比例、负装甲；要求 typed `SaveFormatException`。
   - 加载器新增实体模块参数校验（Smelter/Fabricator/Armor/Shield）。

## 非目标

- 不引入属性测试库（FsCheck/fast-check）；当前手写固定种子足够。
- 不做浏览器/Godot E2E 自动化；Godot 仍靠冒烟命令与截图。
- 不做变异测试、模糊测试集群或覆盖率阈值上调。
- 不把演示数值固化成平衡；fixture 只固定“规则形态”。

## 后果

- 规则变更与实现回归被 fixture/门禁分开；静默漏同步会直接红。
- 新增事件/意图/ADR 有明确的机械检查，减少评审轮才发现的问题。
- 经济系统的守恒不变量从 happy-path 扩展到随机路径。
- 加载器对损坏存档更严格；旧档若含非法模块会被拒绝而不是带病运行。
- 测试数从 448 增至 500；运行时间仍在秒级。

## 验证

- `dotnet test SeedField.slnx` → 500 通过（仿真 346 / Jint 124 / 仓库 22 / Catalog 8）。
- `PresentationCoverageTests`、`IntentContractTests`、`EconomyGoldenFixtureTests`、
  `EconomyInvariantFuzzTests`、`SaveValidationMatrixTests` 全绿。
- 负向探针：临时把 `SmeltingRecipes` 铜配方改为 2:1，golden fixture 必须失败；
  临时新增未处理事件/意图，对应门禁必须失败（本地验证后回退）。

## 回滚

删除 `tests/fixtures/economy/`、新增测试文件与三个门禁测试；恢复加载器模块校验
如需要。生产代码行为不变，回滚只影响测试覆盖。
