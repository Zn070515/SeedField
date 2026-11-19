# SeedField asset brief workflow

用途：用一个 JSON brief 驱动 Blender 渲染单件废土资产，先出**评审图**，通过后再登记进运行时 manifest。

## 管线

```text
brief.json
  → Blender (topdown 90°, 512px, RGBA)
  → 主图 + 4 角度评审图
  → review_sheet.png（512 全图 / 128px 可读性 / 剪影）
  → 人工评审
  → -Register：复制到 runtime + 登记 manifest + ArtAssetTests
```

概念阶段可选：

- **FLUX.1-dev**：`art/comfy/generate_concept.py`，只做颜色/材质/密度参考，非商用许可，不进运行时。
- **MiniMax H3**：本地 `E:\AI_ComfyUI\Models` 已具备 t2v/i2v/r2v；用于运动参考、分镜、预告片，不直接抽帧当 sprite。

## 命令

```powershell
# 评审渲染（默认 1 主图 + 4 角度 + sheet）
art/workflow/run_asset_workflow.ps1 -Brief art/workflow/briefs/waste_shield_generator.json

# 只渲染，不生成 sheet
art/workflow/run_asset_workflow.ps1 -Brief ... -SkipSheet

# 评审通过后：复制到 runtime，并打印 manifest 登记清单
art/workflow/run_asset_workflow.ps1 -Brief ... -Register
```

输出目录：

```text
art/workflow/out/<assetId>/
  <assetId>_topdown_000.png
  review/<assetId>_angle_000.png
  review/<assetId>_angle_090.png
  review/<assetId>_angle_180.png
  review/<assetId>_angle_270.png
  review_sheet.png
  meta.json
```

## Brief 字段

| 字段 | 必填 | 说明 |
|---|---|---|
| `assetId` | 是 | 与 `BUILDERS` 注册名一致 |
| `displayName` | 是 | 评审标题 |
| `category` | 是 | building / unit / prop / ore |
| `branch` | 否 | 五分支或 support |
| `camera` | 是 | 当前固定 `topdown-90` |
| `resolution` | 否 | 默认 512 |
| `directions` | 否 | 建筑 1，单位 8 |
| `orthoScale` / `targetZ` | 是 | 与 builder 一致 |
| `silhouette` | 是 | 一句话剪影语言 |
| `palette` | 是 | 只从 `render_wasteland_assets.py` 的材质名取 |
| `emissive` | 否 | 状态光位置，控制在主体 5–10% |
| `readsAt30Percent` | 是 | 最低缩放下的识别依据 |
| `license` | 是 | 程序化源写 `original-procedural-source` |
| `status` | 是 | 评审期 `style-test-not-final` |

## 添加新资产

1. 在 `art/blender/render_brief_asset.py` 写 builder，并注册：
   ```python
   BUILDERS = {
       "waste_shield_generator": (build_shield_generator, 3.8, 0.55),
       "waste_xxx": (build_xxx, 3.6, 0.5),
   }
   ```
2. 复制 `briefs/waste_shield_generator.json`，改 `assetId`/剪影/色板。
3. 跑评审命令，看 `review_sheet.png`。
4. 通过后 `-Register`，再把 assetId 加进 `tools/build_art_manifest.py` 的
   `ASSETS` / `TOPDOWN_ASSETS` / `TOPDOWN_ONLY` / `FOOTPRINTS` / `SOURCES`。
5. 跑门禁：
   ```powershell
   python tools/build_art_manifest.py
   dotnet test tests/SeedField.Repository.Tests --filter ArtAssetTests
   ```

## 评审标准

- 128px 缩略图仍能区分：核心 / 能源 / 感知 / 通信 / 状态 / 执行 / 护盾。
- 剪影优先：先看黑白 alpha，再看材质。
- 发光面积 5–10%，只用于状态。
- 颜色不是唯一通道：故障/断供/过载同时有形状或图标。
- 固定正交镜头，不引入透视或屏幕空间缩放。

## 许可

- 程序化 Blender 源：`original-procedural-source`。
- FLUX.1-dev：非商用，只放 `art/reference/concepts/`。
- MiniMax H3：Community License 排除 US/EU/UK/South Korea；商用/Steam 分发前做法务确认。
- 第三方 kitbash：保留 license 文件，记录来源。
