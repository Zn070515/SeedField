# M1 完成证据（2026-11-18）

## 变更

- 内置策略模板 `builtin.idle/scout/sentry/relay`（ADR-0042）。
- 构造挑战目录：5 个分支挑战 + `network.minimum`（AND）+ `field.minimum`（OR）。
- 正常启动从 Tick 0 开始；截图/冒烟/基准保留预跑。
- HUD 面板列出五步、两个挑战、`NEXT` 与 `PVE READY`。
- 新增 `--challenges`（Headless）与 `--tutorial-smoke`（Godot）。

## 测试

```powershell
dotnet test SeedField.slnx
```

结果：**423 通过 / 0 失败**

- 仿真 276（含 13 tutorial + 6 challenge）
- Runtime.Js 120（含 13 DevicePrograms）
- 仓库门禁 19
- Catalog 8

关键用例：

- `DeviceProgramsTests.RelayTemplateCompletesTheCommunicationBranchWithoutPlayerCode`
- `DeviceProgramsTests.SentryTemplateAttacksAnEnemyInRange`
- `ConstructionChallengeTests.NetworkChallengeRequiresAllThreeBranches`
- `ConstructionChallengeTests.FieldChallengeAcceptsEitherBranch`
- `TutorialProgressTests.NextStepFollowsCanonicalOrder`

## Headless

```powershell
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --tutorial
dotnet run --project src/SeedField.Headless/SeedField.Headless.csproj -- --challenges
```

实际输出：

```text
TUTORIAL completed=5/5 energy=1 sensing=1 communication=2 state=2 execution=2 next=none pveReady=True
CHALLENGE id=branch.energy completed=True detail="verified at tick 1"
CHALLENGE id=branch.sensing completed=True detail="verified at tick 1"
CHALLENGE id=branch.communication completed=True detail="verified at tick 2"
CHALLENGE id=branch.state completed=True detail="verified at tick 2"
CHALLENGE id=branch.execution completed=True detail="verified at tick 2"
CHALLENGE id=network.minimum completed=True detail="requires branch.energy + branch.communication + branch.execution"
CHALLENGE id=field.minimum completed=True detail="requires branch.sensing or branch.communication"
CHALLENGES completed=7/7 pveReady=True
```

## Godot 首启

```powershell
$env:DOTNET_gcServer = "1"
godot --path game/SeedField.Game -- --tutorial-smoke
```

实际输出：

```text
SEEDFIELD_READY args=--tutorial-smoke gcServer=True
TUTORIAL_SMOKE ok=True tick=0 tutorial=0/5 pve=False
```

## 截图

`godot --path game/SeedField.Game -- --capture`（预跑）输出
`screenshots/m0-combat.png`，左下面板显示五步、`AND · Minimum Network`、
`OR · Field Awareness` 与 `PVE READY · free world unlocked`。

## 未覆盖

- 模板尚未接入工坊“新建设计”作者 UI；目前通过蓝图 `ControllerProgramId` 选择。
- 挑战目录尚无玩家可编辑/关卡评分；AND/OR 只是第一组固定定义。
- 教程文案仍是英文短句，未做本地化与首次试玩节奏验证。
