# 代码审查：medium（2026-11-17）

范围：`2c66509..54478d7`（ADR-0013、抢占式取电/断路器维护、文件拆分、仓库 Lint）。
方式：隔离 worktree + `/code-review medium --fix`；审查修复后移植回主线
（主线此时已包含 ADR-0014 的确定性随机提交）。

## 已修复

| # | 位置 | 缺陷 | 修复 |
|---|---|---|---|
| 1 | `Main.Draw.cs` `IsLogworthy` | `BreakerLockedOut`/`BreakerMaintained` 未进入事件日志白名单，UI 静默丢弃，证据文档却声称可见 | 两个事件加入白名单与配色 |
| 2 | `WorldSaveSerializer.LoadPower.cs` | 允许 `MaintenanceRequired=true` 且 `BreakerOpen=false` 的存档，读档后可绕过锁定合闸/取电 | 拒绝为 `INVALID_POWER_GRID_STATE`，并加回归测试 |
| 3 | `Main.Smoke.cs` | 维护冒烟只检查状态（TripCount 本来为 0），未证明处理器执行；`state!` 可能 NRE | 要求 `BreakerMaintained` 事件；null 安全 |
| 4 | `RepositoryConventionTests.IsGenerated` | 按绝对路径分段，`.claude/worktrees` 副本被 Lint；`D:/bin/...` 检出会跳过全部源码 | 改为仓库相对路径分段，并排除 `.claude` |
| 5 | `SimulationEngine.cs` | 排序比较器对每个意图调用 `FindEntity`，扫描所有区域 | 每 Tick 构建一次优先级映射 |
| 6 | `evidence/2026-11-17-power-maintenance.md` | 拆分前行数无法从提交复核 | 改为 `git show 049545e:<path>` 实测值 |
| 7 | `SimulationEngine.Power.cs` | 摘要仍写“open/close” | 改为 open/close/maintenance |
| 8 | `WorldSaveSerializer.ToData.cs` | 策略按字典插入顺序写盘，哈希却排序，存档字节非规范 | 保存前按文明 ID 排序，并加回归测试 |

## 保留不改

- **维护健康断路器仍扣费/发事件**（`MaintainBreakerCommand`）：是否要求
  `MaintenanceRequired`/`TripCount>0` 是玩法决策，不是缺陷；当前面板按钮始终可用，
  Godot 冒烟也依赖该路径覆盖接线。
- **功能与约 7k 行重构同一提交**（`6726fac`）：违反“一提交一逻辑变更”，但修复需要
  重写已推送历史；以 `7a2114e30b9bdd38` 哈希证明纯重构，并在证据中记录。

## 新增回归测试

- `LoadRejectsALockedOutBreakerThatIsClosed`
- `PowerPolicyOrderDoesNotChangeHashOrSaveOutput`

## 主线验证（含 ADR-0014）

```text
dotnet build SeedField.slnx       0 warnings / 0 errors
dotnet test  SeedField.slnx       6 + 191 + 48 = 245 passed
主切片双跑                         9e86aabc336e8ab8
POWER_MAINTENANCE                 ok=True
POWER_FAULT                       ok=True（hashOk、randomOk）
POWER_BREAKER                     ok=True
POWER_BREAKER_SMOKE               ok=True
SAVE_SMOKE                        ok=True
```

## 备注

- 审查 worktree 的 `docs/design-bible/v1.9-rev2/MANIFEST.json` 显示为 modified，但
  SHA-256 与 HEAD 相同，是 CRLF blob 与 `eol=lf` 属性的幻影差异；未移植、未暂存。
- 审查使用的 `IsGenerated` 负向探针：`.claude/probe` 下 700 行文件不再被 Lint；
  `src/SeedField.Simulation` 下 610 行文件仍会失败。
