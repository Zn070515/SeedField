# 2026-11-19 topdown 客户端 1000 设备混合负载 A/B

- 分支：`feat/support-buildings`
- 对照：Main 迁移前 `a2c298b`（oblique 实体目录）vs 迁移后 `fb8d70e`（topdown）
- 命令：`--benchmark-mixed=1000 --benchmark-mixed-seconds=10`
- 日志：`logs/baseline-oblique-1000b.log`、`logs/topdown-mixed-1000.log`

## 1. 环境

| 项 | 值 |
|---|---|
| CPU | AMD Ryzen 9 9950X，16C/32T |
| OS | Windows 11 专业工作站版 |
| GPU | RTX 4090 |
| 引擎 | Godot 4.7.2 Mono / .NET 10 |
| GC | `DOTNET_gcServer=1`，`gcServer=True` |
| 构建 | Debug（Godot 默认游戏程序集） |
| 采样 | 1.5 s 预热 + 10 s 测量，6 Tick/s，vsync off |

## 2. 结果

| 指标 | oblique（迁移前） | topdown（迁移后） | 变化 |
|---|---:|---:|---:|
| entities | 1006 | 1008 | +2（主场景新增维修/护盾） |
| frames | 4876 | 5079 | +4.2% |
| FPS | 487.5 | **507.8** | +4.2% |
| avgTickMs | 10.25 | **8.58** | **−16.3%** |
| p95TickMs | 19.23 | **15.22** | **−20.9%** |
| presentMs | 0.72 | **0.64** | −11.1% |
| drawCalls | 268 | 269 | +1 |
| allocMBPerTick | 26.58 | 26.69 | +0.4% |
| peakWorkingSetMb | 623.9 | **594.3** | −4.7% |
| faults | 1（SCRIPT_TIMEOUT） | **0** | — |

## 3. 结论

- **没有退化。** topdown 实体目录在同机、同构建、同负载下 avgTick 快约 16%，
  p95 快约 21%，FPS +4%，draw calls 只多 1。
- topdown 分支还多 2 个实体（主场景部署的维修坞/护盾发生器），所以不是靠
  “实体更少” 赢的。
- 峰值内存降低约 30 MB；alloc/Tick 基本持平。
- oblique 基线出现 1 次 `SCRIPT_TIMEOUT`（宿主安全阀，不参与确定性算力），
  topdown 为 0；单次 10 s 采样，不能当作严格统计结论。

## 4. 方法说明 / 局限

- **Debug 构建**：对比的是 Godot 默认游戏程序集，不是 Release 发行包；绝对值
  不能直接与 PERFORMANCE_GUIDE 的 9950X Release Headless 表比较，但 A/B 相对
  结论成立。
- 单次 10 s、6 Tick/s；不是 60 Hz 或最低配置验收。
- oblique 基线来自 `a2c298b` 工作树，临时给 `barricade` 补了 `Power(0,0)` 以
  通过蓝图分支校验；该改动未提交。
- 未覆盖 Steam 包、Fixed/Release 导出、4/6/8 核实机分档与 30 min 浸泡。

## 5. 复现

```powershell
$env:DOTNET_gcServer = "1"

# topdown（当前分支）
godot --path game/SeedField.Game -- `
  --benchmark-mixed=1000 --benchmark-mixed-seconds=10

# oblique 基线（临时工作树）
git worktree add C:\Users\16275\Desktop\SeedField-baseline a2c298b
# 临时给 barricade 补 Power(0,0)，build 后运行同一命令
```
