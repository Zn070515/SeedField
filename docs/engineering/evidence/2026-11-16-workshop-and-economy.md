# 证据：工坊、蓝图版本、存档与 E/C/B 约束（2026-11-16）

## 构建与测试

```powershell
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

```text
SeedField.Simulation.Tests: 39 passed
SeedField.Runtime.Js.Tests: 13 passed
```

新增覆盖：

| 领域 | 用例 |
|---|---|
| 蓝图/实例 | 发布 v2 不改已部署实例；显式升级；未发布蓝图不可部署 |
| 五分支 | 能源/感知/通信/状态/执行全部覆盖才 `IsMinimumUsable` |
| 核心区 | 每分支登记最新产品；存档后可从核心区恢复 |
| 存档 | 世界/在途消息/核心区/蓝图/统计往返哈希一致；拒绝未知 schema |
| E/C/B | 本地发电封顶；算力预算拒绝第二个意图；带宽拒绝第二条消息 |
| 控制器 | 保存脚本与版本；坏脚本读取失败 |

## Godot 运行

```powershell
godot --path game/SeedField.Game -- --capture-workshop
godot --path game/SeedField.Game -- --save-smoke
```

输出：

```text
SEEDFIELD_PRERUN tick=41 shots=3 entities=3
SCREENSHOT_SAVED .../screenshots/m0-workshop.png (Ok)

SAVE_SMOKE ok=True before=cf8469ead9793a2f mutated=6bb3e5559010d383 after=cf8469ead9793a2f tick=41 A=v1 B=v1
```

![Workshop panel](../../../game/SeedField.Game/screenshots/m0-workshop.png)

画面内容：

- `FIVE BRANCHES 5/5 · PvE READY`：蓝图注册表覆盖全部五个分支。
- 每个设计列出最新版本与分支标签（E/S/C/T/X）。
- A 侧三个实例：Scout Mk2（由 v1 显式升级）、Strike Frame、Power Cell。
- 部署与升级都由 `SimulationCore` 执行；Godot 只发请求并显示结果。
- 部署材料成本与存储 S 容量尚未强制，已在面板中标注为 M1 原型缺口。

## 存档行为

- `WorldSaveSerializer`：区域、实体、蓝图注册表、核心区最新产品、在途消息、收件箱、统计与下一 ID。
- `GameSaveSerializer`：在外层追加 A/B 控制器脚本与版本。
- `F5`/`F9` 落盘 `user://seedfield-save.json`；读取失败保留当前世界。
- `--save-smoke` 证明保存、推进 10 Tick、读取后哈希回到保存点。

## 未覆盖

- 多区域、跨区通信/物流。
- 工坊扣 M 与制造时间。
- 存储 S 容量强制。
- 旧存档迁移（当前仅 schema v1）。
- 正式美术与导出包。
