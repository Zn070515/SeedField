# 废土正交俯视资产包 v1 证据（2026-11-18）

## 产出

| 类别 | 资产 |
|---|---|
| 建筑 9 | core / generator / relay / turret / drill / foundry / hostile / scout / sentry |
| 道具 3 | barrels / sandbags / scrap |
| 矿脉 4 | copper / lead / titanium / coal |
| 地表 4 | dirt / sand / ash / rust（无缝 512） |
| 概念参考 1 | `art/reference/concepts/wasteland-outpost-flux-v1.png`（FLUX.1-dev，非商用，仅内部参考） |

全部运行时资产为 512×512 RGBA、90° 正交、`style-test-not-final`，位于
`game/SeedField.Game/Art/style_topdown/`。

## 生成命令

```powershell
blender --background --factory-startup --python art/blender/render_wasteland_assets.py -- `
  --output game/SeedField.Game/Art/style_topdown --size 512

python tools/generate_terrain_tiles.py --output game/SeedField.Game/Art/style_topdown

python art/blender/make_wasteland_sheet.py
python tools/build_art_manifest.py
```

实际输出：

```text
WASTE_ASSETS_RENDERED 16 ...
TERRAIN_TILE terrain_dirt/sand/ash/rust
WASTELAND_SHEET art/renders/wasteland_pack_v1.png 1114x1278
ART_MANIFEST_WRITTEN .../Art/manifest.json
```

## 视觉证据

- 资产总览：`art/renders/wasteland_pack_v1.png`（16 格，暗底）。
- Godot 集成截图：`game/SeedField.Game/screenshots/art-mindustry-style.png`。
  - 四类地表（灰烬/沙地/焦土/锈蚀）
  - 四种矿脉铺在地表
  - 种子核心、发电、炮塔、钻机、中继、铸造厂、敌对基地
  - 侦察/哨戒单位按朝向旋转
  - 沙袋、油桶、废料堆与贴地阴影

## 门禁

```powershell
python tools/build_art_manifest.py --check
dotnet test tests/SeedField.Repository.Tests --filter "FullyQualifiedName~ArtAssetTests"
dotnet test SeedField.slnx
```

- `ART_MANIFEST_OK`
- `ArtAssetTests` 通过（新增资产 512/RGBA/SHA/alphaBBox/topdown 单方向）
- 全量测试结果见本次提交说明

## ComfyUI 使用边界

- 模型：`flux1-dev.safetensors`（FLUX.1-dev，**非商用许可**）。
- 输出只写入 `art/reference/concepts/`，不进入 manifest、不进入运行时、不随发行包。
- 用途：配色/材质/细节密度参考（暖沙、锈铁、橄榄绿、碎石、沙袋、裂缝）。
- 若未来要使用生成式运行时纹理，必须换可商用模型或人工重绘并另立 ADR。

## 已知不足

- 仍为程序化低模，缺少高模雕刻、PBR 贴图集、手工 decal 与角色动画。
- 建筑在 1× 缩放下偏小，正式版需要镜头/占地重新标定。
- 地表为程序化无缝纹理，缺少大尺度地貌（悬崖、沟壑、沉陷区）。
- 资产仍是 `style-test-not-final`，不能作为商店素材或正式验收。
