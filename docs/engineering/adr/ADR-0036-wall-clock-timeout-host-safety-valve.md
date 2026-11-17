# ADR-0036：Jint 墙钟超时是宿主安全阀

- 状态：**Accepted**
- 日期：2026-11-17
- 关联决策：ADR-0028、ADR-0030、审核 P1“墙钟超时与确定性冲突”
- 前置：Jint `MaxStatements`/`MemoryLimitBytes`/`Timeout`、回放

## 背景

`JsControllerOptions` 默认同时启用：

- `MaxStatements = 50_000`
- `MemoryLimitBytes = 8_000_000`
- `Timeout = 50 ms`

语句与内存上限对相同脚本/输入是可复现的；`Timeout` 是墙钟，受宿主负载、
线程调度与 GC 暂停影响。同一脚本可能在一台机器正常返回，在另一台超时产生
`SCRIPT_TIMEOUT` Fault，导致仿真/回放分叉。

回放记录 Intent 并校验世界哈希，能复现已发生的行动，但不能证明重新执行玩家
脚本会得到一致 Intent。

## 决定

1. **`SCRIPT_TIMEOUT` 是宿主安全阀，不是游戏内 C 算力结算规则**：
   - 不参与正式平衡/算力预算；
   - 与 `SCRIPT_STATEMENT_LIMIT`/`SCRIPT_MEMORY_LIMIT` 明确区分；
   - 超时只产生 Fault 并丢弃该控制器本轮意图，不直接改世界。
2. **确定性边界用语句/内存上限**：
   - `SCRIPT_STATEMENT_LIMIT` 在顺序/并行/不同 DOF 下必须一致；
   - 测试覆盖顺序 vs 并行、不同执行器；
   - 新增脚本若需要确定性失败，应使用语句/内存预算，不要依赖墙钟。
3. **回放/审计语义**：
   - 回放记录 Intent，不重新执行脚本；
   - 若审计重新执行脚本并遇到 Timeout，应标记“宿主非确定”，不能当作
     游戏规则差异；
   - 正式回放/分叉告警仍以世界哈希为准。
4. **压力验证**：
   - 增加 Timeout Fault 显式测试；
   - 在并行/CPU 压力/GC 干扰下记录 Timeout 是否出现，作为宿主健康信号，
     不作为确定性断言；
   - 未来若需要严格 C 预算，改为指令/语句预算，而不是墙钟。

## 非目标

- 不删除 Timeout（死循环/灾难性脚本仍需要宿主安全阀）。
- 不用 Timeout 实现游戏内 C 算力上限。
- 不承诺跨机器脚本执行在超时边界上的完全一致。
- 不改 Jint 的语句/内存限制语义。

## 后果

- 审核/回放必须区分确定性 Fault 与宿主 Timeout。
- 高负载下脚本可能被安全阀切断；这是宿主保护，不是游戏规则。
- 性能/确定性测试应使用语句/内存预算做断言，Timeout 只断言“会发生且被分类”。

## 验证

- `ControllerBatchRunnerTests.StatementLimitFaultIsDeterministicAcrossExecutors`：
  顺序与并行执行器均得到 `SCRIPT_STATEMENT_LIMIT`。
- `ControllerBatchRunnerTests.TimeoutFaultIsHostSafetyValve`：短墙钟超时得到
  `SCRIPT_TIMEOUT`。
- 现有并行/顺序战斗哈希测试继续通过。

## 回滚

删除 ADR 与两组测试即可；`Timeout` 行为不变。Jint 选项与脚本契约不变。
