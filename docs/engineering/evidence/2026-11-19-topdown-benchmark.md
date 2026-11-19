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

## 3.1 Release 近似（2026-11-19）

Godot 导出模板未安装，无法生成真正的 Windows Release 导出包；本轮用
`dotnet build -c Release` 的 `SeedField.Game/Simulation/Runtime.Js` 程序集替换
Godot 的 Debug bin 做近似测量（跑完已恢复 Debug 程序集）。

同一时段机器存在 Steam 后台下载、VS Code、Claude 等负载，Release 三次结果：

| 采样 | avgTick | p95Tick | FPS | faults |
|---|---:|---:|---:|---:|
| Release #1 | 13.31 ms | 40.07 | 364.0 | 1 |
| Release #2 | 12.88 ms | 22.64 | 383.0 | 0 |
| Release #3 | 13.94 ms | 33.06 | 312.3 | 0 |
| Debug 背靠背 | 11.94 ms | 21.06 | 365.8 | 3 |
| 早前较空闲 Debug | 8.58 ms | 15.22 | 507.8 | 0 |

结论：**Release 近似值与 Debug 的差异落在当前机器噪声内**；不能据此宣称
Release 更快或更慢，也不能作为发行验收。正式 Release/Steam 数值必须在空闲机
或 CI perf runner 上、用真实导出包复测。

## 4. 方法说明 / 局限

- **Debug 构建**：A/B 表比的是 Godot 默认游戏程序集，不是 Release 导出包；
  绝对值不能直接与 PERFORMANCE_GUIDE 的 9950X Release Headless 表比较，但 A/B
  相对结论成立。
- Release 近似见 §3.1；当前机器负载导致 8.58–13.94 ms 的跨采样波动。
- 单次 10 s、6 Tick/s；不是 60 Hz 或最低配置验收。
- oblique 基线来自 `a2c298b` 工作树，临时给 `barricade` 补了 `Power(0,0)` 以
  通过蓝图分支校验；该改动未提交。
- 未覆盖 Steam 包、真实 Release 导出、4/6/8 核实机分档与 30 min 浸泡。

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
