# ADR-0037：观测消息历史确定性上限

- 状态：**Accepted**
- 日期：2026-11-17
- 关联：ADR-0027（设备观测）、ADR-0028（并行控制器）、ADR-0035（本地目录）、ADR-0036（超时边界）
- 触发：正式客户端混合负载基准（Issue #15 / 审核“混合负载”）

## 背景

混合基准（Godot windowed + 真实 scout 控制器 + 渲染）发现：

- 1000 台真实 scout：约 **309 ms/Tick**、24 个 `SCRIPT_TIMEOUT`、2.9 GB/Tick；
- 同一 1000 台、但不发消息的 quiet 脚本：约 **5.2 ms/Tick**、0 Fault、64 MB/Tick。

根因是消息/观测放大：

1. `program.scout.v1` 每个有通信模块的设备每 Tick 发送 contact；
2. 收件箱容量按舰队所有 State 模块容量求和（1000 × 64 = 64,000），
   旧消息在容量内保留；
3. `ObservationBuilder`/`ObservationCache` 把该文明/区域的**全部**已送达
   消息放进每个控制器观测；
4. 于是每 Tick 成本接近 `设备数 × 收件箱大小`，消息对象在每个 Jint 控制器里
   重复展开，最终触发脚本墙钟超时。

这不是 Jint 或渲染的瓶颈：quiet 基准证明 1000 台真实渲染 + 控制器仍在
6 Hz 预算内。

## 决定

1. 新增 `ObservationLimits.MaxMessages = 32`。
2. `ObservationBuilder.Build`（文明全量观测）与 `ObservationCache.Messages`
   （设备作用域观测）只暴露**收件箱投递顺序的最后 32 条**消息；投递顺序由
   引擎按 `(DeliverTick, MessageId)` 稳定追加，不再对全收件箱排序。
3. 收件箱本身的容量/过期规则、世界状态、存档、哈希、网络带宽均不变；
   旧消息仍在世界内，只是不再进入观测。
4. 设备观测先按区域过滤，再取该区域最后 32 条，避免其他区域消息挤掉本区。
5. 这是**观测语义**变更：脚本不再看到完整历史；需要历史的脚本应依赖自身
   状态或未来的显式档案/摘要机制，而不是无限观测。

## 非目标

- 不改变收件箱存储容量或过期规则。
- 不按 topic/发送者过滤消息。
- 不改通信带宽、路由、消息延迟或 MessageId。
- 不改世界存档 schema 或 `WorldStateHasher`。
- 不承诺脚本能看到任意久远的历史消息。

## 后果

- 混合客户端 1000 台从约 309 ms/Tick 降到约 **28 ms/Tick**，0 脚本 Fault；
  500 台约 10 ms/Tick；100 台约 1.9 ms/Tick。
- 1000 台混合负载的渲染仍约 28 FPS（1280×720，vsync off），不是 60 FPS；
  这是渲染/呈现层的后续工作，不是控制器/消息问题。
- 旧脚本若依赖第 33 条以前的消息，会观察到行为变化；需在脚本契约/文档中
  明确“最近 32 条”。
- 观测对象数量下降，降低 GC 压力和 worker/IPC 负载。
- 世界状态与回放哈希不受影响；回放仍只记录 Intent。

## 验证

- `DeviceObservationTests.DeviceMessagesKeepOnlyTheMostRecentHistory`：
  40 条消息只返回最后 32 条（9..40），顺序稳定。
- 全量测试（257 Simulation，含既有消息过滤/存储测试）。
- 混合基准：
  - 真实 scout 100/500/1000：1.91 / 9.82 / 27.96 ms/Tick，0 Fault；
  - quiet 500/1000：3.12 / 5.22 ms/Tick，0 Fault。

## 回滚

删除 `ObservationLimits` 与两处 `Skip` 即可恢复完整历史；世界/存档/哈希
无需迁移。
