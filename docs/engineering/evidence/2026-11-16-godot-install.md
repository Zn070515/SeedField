# 证据：Godot 4.7.2 Mono 安装与 net10.0 冒烟（2026-11-16）

## 安装

```powershell
winget install --id GodotEngine.GodotEngine.Mono --exact --version 4.7.2 `
  --location "D:\Dev\Godot\4.7.2-mono" `
  --accept-package-agreements --accept-source-agreements --disable-interactivity
```

结果：

```text
Downloading https://github.com/godotengine/godot/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip
Successfully verified installer hash
Successfully extracted archive
Successfully installed
```

入口：`D:\Dev\bin\godot.cmd`（GUI）、`D:\Dev\bin\godot-console.cmd`（控制台）。

## 版本

```text
4.7.2.stable.mono.official.ed1daf0bf
```

## net10.0 冒烟工程

`Net10Smoke.csproj` 使用 `Godot.NET.Sdk/4.7.2`、`TargetFramework=net10.0`，脚本在 `_Ready` 打印后退出。

```powershell
dotnet build          # exit 0
godot-console --headless --path .
```

输出：

```text
Godot Engine v4.7.2.stable.mono.official.ed1daf0bf - https://godotengine.org

NET10_SMOKE_OK
```

## 结论与限制

- Godot 4.7.2 Mono 可构建并运行 net10.0 C# 工程。
- GodotSharp 自带 `GodotPlugins.runtimeconfig.json` 为 `net8.0` + `rollForward=LatestMajor`；本机仅 .NET 10 运行时，实际运行成功。
- 未安装 mono 导出模板与标准导出模板；M0 编辑器运行不受影响，导出 Windows 包前需补装。
- 尚未创建正式 `game/SeedField.Game` 工程，也未产出画面截图。
