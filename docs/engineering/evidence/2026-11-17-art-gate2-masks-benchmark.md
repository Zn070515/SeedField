# 证据：Gate 2 法线/发光与设备性能（2026-11-17）

关联：ADR-0024、`docs/engineering/evidence/2026-11-17-art-vertical-slice.md`、
`docs/art/PIPELINE_ASSET_CONTRACT.md`。

## 1. 范围

- 表现层验证，不改 `SeedField.Simulation`、存档 schema、Jint、离线包。
- 资产：`power_node`、`seed_core` 四态、`worker_frame_sensor`、
  `worker_frame_drill`；法线/发光只做正面帧，控制仓库体积。
- 场景：`VisualBench.tscn`（只读资产目录，不构建 `WorldState`）。

## 2. 法线与发光资产

- 法线：Cycles 几何 Normal 经 `Vector Transform`（WORLD→CAMERA）后编码为
  RGBA；`Raw` 视图变换避免 sRGB 二次编码，蓝色通道翻转以匹配 Godot 2D。
- 发光：Cycles 透明渲染，非自发光物体替换为纯黑 Emission，自发光物体替换为
  纯 Emission；保留遮挡，输出 RGBA 供 `CanvasItemMaterial.BlendMode=Add`。
- 每张 512×512、RGBA/8bit；`ArtAssetTests` 校验 pass 路径、SHA-256、
  `alphaBBox` 与尺寸。
- 对比表：`art/renders/mask-sheet.png`；实机截图：
  `screenshots/art-mask-normal.png`（masks on）、`screenshots/art-mask-plain.png`
  （masks off）。

## 3. 实机验证

`VisualBench`：

- 1280×720、Godot 4.7.2、RTX 4090、驱动 610.62；
- `CanvasTexture`（diffuse + normal）+ `PointLight2D`（径向梯度）+
  `CanvasModulate`；emissive `Sprite2D` 加法叠加；
- 1.5s 预热，3s 采样；vsync 关闭（`DisplayServer.VSyncMode.Disabled`）；
- 结果写入 `logs/visual-bench-{devices}-{masks}.json`（gitignore）。

命令：

```powershell
godot --path game/SeedField.Game res://Scenes/VisualBench.tscn -- `
  --devices=100 --masks=on --seconds=3
```

## 4. 结果（开发机，非最低配置）

| 设备 | masks | FPS（均值） | process ms | draw calls | video mem |
|---:|---|---:|---:|---:|---:|
| 1 | off | 7303.16 | 0.849 | 1 | 101.9 MB |
| 1 | on | 6709.89 | 1.827 | 2 | 103.3 MB |
| 10 | off | 6455.94 | 0.791 | 10 | 101.9 MB |
| 10 | on | 4477.83 | 2.489 | 20 | 103.3 MB |
| 50 | off | 4442.26 | 2.611 | 50 | 101.9 MB |
| 50 | on | 2823.46 | 1.784 | 100 | 103.3 MB |
| 100 | off | 3244.69 | 1.849 | 100 | 101.9 MB |
| 100 | on | 1830.89 | 2.033 | 200 | 103.3 MB |

观察：

- masks 使 draw calls 从 N 变为 2N（emissive 叠层），100 台时 FPS 约为
  masks-off 的 56%；50 台约为 64%。仍远高于 60 FPS。
- 视频内存增加约 1.4 MB（法线/发光正面帧纹理），不是随设备线性增长。
- process 时间约 1–2.5 ms；vsync 关闭所以 FPS 是上限参考，不是实际展示帧率。
- 未测：其他 GPU、4K、独显/核显切换、最低配置、长时间稳定性。

## 5. 复现

```powershell
godot --headless --path game/SeedField.Game --import
foreach ($masks in @('on','off')) {
  foreach ($devices in @(1,10,50,100)) {
    godot --path game/SeedField.Game res://Scenes/VisualBench.tscn -- `
      "--devices=$devices" "--masks=$masks" --seconds=3
  }
}
```

```text
dotnet test SeedField.slnx
SeedField.Repository.Tests: 18 passed
SeedField.Simulation.Tests: 230 passed
SeedField.Runtime.Js.Tests: 85 passed
```

## 6. 结论与限制

- `CanvasTexture` 法线在 2D 灯光下可见，emissive 加法叠层可读；可作为表现层
  候选，但尚未接入正式 `Main` 光照（Main 保持无 2D 灯光）。
- 本轮只验证正面帧与开发机；正式量产需要全方向法线/发光、GPU 目标档位和
  最低配置测试。
