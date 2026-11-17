# ADR-0031：TypeScript 控制器契约

- 状态：**Accepted**
- 日期：2026-11-17
- 关联决策：T-02（JavaScript/Jint）、G-06/G-07（自主程序）
- 前置：Observation JSON 契约、Intent JSON 契约

## 背景

控制器脚本是 JavaScript，但 Observation/Intent 的字段只存在于 C# 和 JSON 边界。
脚本作者在编辑器里没有补全与类型检查，字段拼写错误只能等到运行时
`InvalidIntentSchema` 或读到 `undefined`。设计基线要求 TypeScript `.d.ts`
编辑声明。

## 决定

- 新增 `contracts/seedfield-controller.d.ts`：
  - `Observation` 及全部实体/消息/物流/控制点/比赛/外交类型；
  - `Intent` 判别联合：move/attack/send/wait/transfer/sendEnergy/drawEnergy；
  - `Decide` 函数类型。
- 新增 `contracts/example-controller.ts`，用严格模式演示字段访问与意图返回。
- `contracts/tsconfig.json`：`strict`、`noEmit`、ES2020、Bundler resolution。
- CI 在 build-test 中运行
  `npx --yes --package typescript@7.0.2 tsc -p contracts/tsconfig.json`。
- C# 侧 `ControllerContractTests`：
  - 用 `SerializeFullObservation` 生成包含全部 section 的 JSON，递归收集属性名，
    断言 `.d.ts` 包含每个字段；
  - 断言 `.d.ts` 包含全部 7 种 Intent kind 与通用字段。
- 契约是**编辑器声明**，不参与运行时；Jint 仍只接收 JSON/展开后的对象。

## 非目标

- 不做 TypeScript → JavaScript 编译/打包管线；脚本仍直接写 JS。
- 不做运行时 schema 校验（引擎/`JsController` 已做 Intent 校验）。
- 不发布 npm 包或 VS Code 扩展。
- 不声明 worker IPC/内部存档格式。

## 后果

- 字段变更必须同时更新 `.d.ts`，否则 C# 漂移测试失败；语法由 CI `tsc` 门禁。
- 直接对象、紧凑展开、完整 JSON 三条路径对脚本暴露同一对象形状，契约以完整
  形状为准。
- 新增 Intent 时必须同步 `.d.ts` 联合与 `ControllerContractTests` 的 kind 列表。

## 验证

- `tsc -p contracts/tsconfig.json` 通过（本地 TS 7.0.2）。
- `ControllerContractTests`（2 个）通过：全部 Observation 字段、全部 Intent
  kind/字段。
- 全量测试与 CI 通过。

## 回滚

删除 `contracts/`、CI 步骤与 `ControllerContractTests` 即可；不影响运行时、
存档或脚本兼容性。
