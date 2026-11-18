# ADR-0043：废土正交俯视资产包 v1 与 Blender/ComfyUI 管线

- 状态：**Accepted（样片资产，非正式验收）**
- 日期：2026-11-18
- 关联决策：A-02、ADR-0041（废土正交俯视方向）
- 前置：`MindustryStyle` 程序化样片、`ArtAssetTests`、`build_art_manifest.py`

## 背景

ADR-0041 把世界层方向定为 Mindustry 式正交俯视 + 末日废土，但当时的样片只有
程序化色块：建筑平、矿脉是圆点、地表是纯色。用户指出“一看就是敷衍、细节经不起
推敲”。需要一轮真实的 Blender 资产与更细的地表/矿脉，而不是继续手绘方块。

同时，ComfyUI/FLUX.1-dev 已在本地可用，但 FLUX.1-dev 是**非商用许可**，且 AI
概念图没有可编辑源与透明 alpha，不能直接成为 Steam 运行时资产。

## 决定

### 1. Blender 程序化资产包 v1

新增 `art/blender/render_wasteland_assets.py`，统一 90° 正交、512×512 RGBA、
暖色主光/冷色轮廓光、程序化锈蚀/粗糙度/凹凸、居中 pivot：

- 建筑 9 件：`waste_core`、`waste_generator`、`waste_relay`、`waste_turret`、
  `waste_drill`、`waste_foundry`、`waste_hostile`、`waste_scout`、`waste_sentry`。
- 道具 3 件：`prop_barrels`、`prop_sandbags`、`prop_scrap`。
- 矿脉 4 件：`ore_copper/lead/titanium/coal`，由碎石、晶簇、砂砾组成，
  不含大块黑色底片。
- 细节手段：多层板件、螺栓环、危险条纹、格栅、管线/阀门、锈迹、焦痕、残骸、
  状态灯；而不是单一盒体 + 纯色。

### 2. 地表与样片集成

- `tools/generate_terrain_tiles.py` 新增 `terrain_dirt/sand/ash/rust` 四张
  无缝 512 地表；与既有 concrete/road 共用管线。
- `MindustryStyle` 改为加载 `Art/style_topdown/*` 真实纹理，缺失时回退程序化块；
  结构/单位/矿脉/道具/地表全部走纹理，单位按朝向旋转。

### 3. ComfyUI/FLUX 的定位

- 仅用于**内部概念参考**：`art/reference/concepts/`，不进入 `Art/manifest.json`、
  不进入运行时、不随发行包分发。
- 记录模型与许可：FLUX.1-dev 为非商用许可；若未来要用生成式纹理/资产，需换成
  可商用模型或人工重绘，并单独写 ADR。
- 概念图用于配色、材质与细节密度参考（暖沙、锈铁、橄榄绿、碎石/沙袋/裂缝），
  实际运行时资产由 Blender 程序化源生成。

### 4. 门禁与清单

- `build_art_manifest.py` 注册 20 个新资产与 footprint/source；地形资产加入
  `TERRAIN_ASSETS` 的全覆盖/贴边豁免。
- `ArtAssetTests` 同步地形豁免；所有新资产仍要求 512×512、RGBA、SHA-256、
  alphaBBox、topdown 单方向与居中 pivot。

## 非目标

- 不宣称达到 3A/正式美术验收；资产仍是 `style-test-not-final`。
- 不引入高模雕刻、PBR 贴图集、手工绘制 decal、角色动画或正式 VFX。
- 不改仿真、存档、哈希、Jint 或离线包契约。
- 不把 FLUX 输出当运行时资产，不绕过许可审查。

## 后果

- 世界层样片从“色块地图”升级为有地表、矿脉、建筑、道具与阴影的可读场景。
- 运行时 Art 体积增加；manifest 与 ArtAssetTests 覆盖同步扩大。
- 程序化材质/几何仍不是最终质量；下一步需要高模源、材质分层、手工 decal 与
  光照/AO 预算，才能接近商业品质。
- 资产 id/路径稳定，后续版本可并存；旧斜俯视资产保留为历史对照。

## 验证

- `python tools/build_art_manifest.py` → `ART_MANIFEST_WRITTEN`。
- `python tools/build_art_manifest.py --check` → `ART_MANIFEST_OK`。
- `dotnet test ... --filter ArtAssetTests` → 通过。
- `art/blender/make_wasteland_sheet.py` → `art/renders/wasteland_pack_v1.png`。
- Godot `MindustryStyle` `--capture-art` → `screenshots/art-mindustry-style.png`
  显示四类地表、四种矿脉、建筑/单位/道具与阴影。

## 回滚

删除 `render_wasteland_assets.py`、新 PNG/import、地表变体与 manifest 注册；
`MindustryStyle` 自动回退到程序化块。ComfyUI 参考图可单独删除，不影响运行时。
