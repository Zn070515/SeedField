# 游戏内编程与自动化玩法

## 已定语言与地位 [CONFIRMED]

- 玩家脚本语言：**JavaScript**。
- 执行运行时：**Jint 4.18.0**（嵌入 C# 游戏仿真）。
- 编辑器类型与补全：TypeScript `.d.ts` 类型声明仅用于开发体验，不表示玩家提交 TypeScript 可直接运行。
- Python、Lua/Luau、自研 DSL 均不作为首发替代；QuickJS 仅在性能或安全证据显示 Jint 产生实质阻断时进入 ADR 评估。

## 编程是什么，不是什么

玩家真正编写逻辑：目标选择、函数、状态机、队形/任务协调、故障恢复、消息协议、局部任务分配、能源/带宽节流。不是只把几个选项从“保守/激进”切换。[CONFIRMED/BASELINE]

不要求玩家从第一分钟就能编程。入门靠五分支最低元件与模板；获得可编程控制器后逐步打开更高级自由度。[CONFIRMED]

## 推荐控制器契约 [PROPOSED，统一形态]

```js
// 仅为概念示例，不是已冻结 API。
function onTick(ctx, memory) {
  const nearby = ctx.visibleContacts || [];
  const target = nearby.find(x => x.hostile && x.inRange);
  return {
    intents: target ? [{type: 'shoot', targetId: target.id}] : [{type: 'wait'}],
    memory: {...memory, lastTarget: target?.id ?? null}
  };
}
```

正式契约需由唯一 SimulationCore/ScriptHost schema 定义，不得让多个 Agent 发明 `ctx` 和 `obs` 的多份互相不兼容 API。

## 受限观察与行动 [BASELINE]

`WorldState → Visibility Filter → Observation → JS Controller + LocalState/Inbox + ComputeBudget → Intent[] → Validate/Resolve → Events`

- JS 获取的是值快照、有限观察，不持有任何可变 `WorldState` 引用。
- 每个控制器存在隔离状态与明确保存/恢复规则。
- 没有通信时只能用本地观察、已收到的命令和本地记忆。
- 本 Tick 发送的消息最早后续 Tick 到达；链路/带宽/延迟影响协作。
- 指令可能因能源、射程、未知目标、模块失效、预算超额等失败；UI 必须解释。
- 有权限差异与设备能力约束：没有传感器不能读敌情，没有武器不能射击。

## 游戏资源 C 与宿主安全预算分离

1. 游戏层预算：可复现的有限计算能力，硬件/分布式协作可升级。[CONFIRMED]
2. 宿主层限制：Jint 语句/超时/内存/递归/对象图等约束，防止异常/恶意脚本拖死游戏。[BASELINE]

超额后的游戏行为（暂停、丢弃、延迟、降频）仍未冻结。[OPEN] 宿主限制出现时必须发 `SCRIPT_FAULT` 等结构化事件并维持世界一致性。[PROPOSED]

## 编辑与调试体验 [BASELINE / PROPOSED]

- 提供模板、设备 API 帮助、自动补全、`.d.ts`、行号诊断、实时/暂停单步、状态/预算可视化。
- 能查看每 Tick 的 `Observation / Memory / Intent / Failure`，帮助理解为什么结果不同。
- 至少支持固定种子重跑、程序版本差异对比和上次成功版本回滚。[PROPOSED]
- 工坊/编辑器默认暂停，用户可主动改为实时。[CONFIRMED]
- 修改工坊代码不无条件改写已部署设备；更新需要玩家明确选择。[CONFIRMED]

## 不得擅自开放的 API

文件系统、网络、系统命令、`eval` 逃逸、CLR 类型调用、原生模块、无预算后台线程、任意设备引用、直接编辑存档/世界状态、绕过生产系统的“制造”指令。具体 JS 标准库白名单/禁用项需要测试并版本化。

## 安全警告

Jint 的进程内执行预算不等于不可信社区 JS 的操作系统级沙盒。**在独立进程隔离与严格 IPC 通过安全验收前，不得自动执行导入的任意第三方 JS。** 内置可信对手可先实现完整体验；文件包可以先只验证结构和哈希。
