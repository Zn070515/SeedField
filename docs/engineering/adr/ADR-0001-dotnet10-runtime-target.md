# ADR-0001：C# 运行时目标统一为 .NET 10.0

- 状态：**Accepted（用户决定）**
- 日期：2026-11-16
- 关联决策：T-01

## 背景

设计基线确认 Godot 4.7.2 .NET + C# 仿真，但未固定具体 .NET 版本。本机仅安装 .NET SDK 10.0.404 / Runtime 10.0.15；用户明确指示不再考虑 net8.0，统一到 net10.0。

## 决定

- `SeedField.Simulation`、`SeedField.Runtime.Js`、`SeedField.Headless` 及测试项目全部目标 `net10.0`。
- 未来 Godot 客户端必须使用 .NET 10 兼容的构建方式；设计文档中的技术栈表述同步标注“.NET 10.0 构建目标”。
- 不使用多目标框架，不为 net8.0 保留兼容分支。

## 后果

- 构建链简化，减少还原与测试矩阵。
- 需在安装 Godot 4.7.2 .NET 后实测其是否支持 net10.0 项目引用。
- 若 Godot 版本不支持 net10.0，必须提交新的 ADR 与实测证据，由用户批准后才能调整；不得静默改回旧框架。

## 验证

- `dotnet build` / `dotnet test` 在 SDK 10.0.404 下通过。
- Jint 4.18.0 的 net10.0 资产可用。
- Godot 兼容性：**待验证（未安装）**。
