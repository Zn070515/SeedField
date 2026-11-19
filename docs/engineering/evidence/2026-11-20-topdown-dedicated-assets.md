# 2026-11-20 topdown 专用资产：sensing / comm / state / core 四态

- 分支：`feat/topdown-dedicated-assets`
- 关联：ADR-0041（废土正交俯视）、ADR-0043（资产包 v1）、ADR-0050/0051（支援建筑）
- 综述图：`art/renders/topdown_dedicated_family_v0.png`

## 1. 新增资产

| Asset | 用途 | Review |
|---|---|---|
| `waste_sensor` | 感知分支：偏移碟、扫描臂、四传感器吊舱、侧柜 | `art/renders/waste_sensor_review_v0.png` |
| `waste_comm` | 通信分支：桁架桅杆、横臂、大偏移碟、紫色信息灯 | `art/renders/waste_comm_review_v0.png` |
| `waste_state` | 状态分支：堆叠立方散热体、发光缝、侧散热器/控制舱 | `art/renders/waste_state_review_v0.png` |
| `waste_core_regenerating` | 核心再生：青色同心环 + 六束能量柱 | `art/renders/waste_core_regenerating_review_v0.png` |
| `waste_core_bootstrap` | 核心启动：琥珀脚手架 + X 梁 + 启动环 | `art/renders/waste_core_bootstrap_review_v0.png` |
| `waste_core_besieged` | 核心围困：红色警灯/环、焦痕、散落残片 | `art/renders/waste_core_besieged_review_v0.png` |

全部为 512×512、topdown 90°、`style-test-not-final`。

## 2. 映射与场景

- `VisualEntitySnapshot.AssetFor`：
  - `sensing` → `waste_sensor`
  - `comm` → `waste_comm`
  - `state` → `waste_state`
- `VisualAssetCatalog.GetCore`：
  - 优先 `waste_core_regenerating/bootstrap/besieged` / `waste_core`；
  - 找不到时回退 `seed_core_*` / `seed_core`，保持 oblique 场景可用。
- `Scenarios` 部署 sensing/comm/state 到 A staging 附近，并把 A 初始 M 从 500 调到 **900**
  （场景演示值，不是正式平衡；用于容纳新增建筑成本）。
- `tools/build_art_manifest.py` 注册 6 个资产、footprint 与 source；`STATES` 增加 waste_core 状态。

## 3. 验证

```text
python tools/build_art_manifest.py --check
  → ART_MANIFEST_OK

dotnet test tests/SeedField.Repository.Tests --filter "FullyQualifiedName~ArtAssetTests"
  → passed

dotnet test SeedField.slnx
  → 526 passed / 0 failed
    仿真 372 · Runtime.Js 124 · 仓库门禁 22 · Catalog 8
```

主场景截图（含 sensing/comm/state 与新核心状态）：

`game/SeedField.Game/screenshots/main-topdown.png`

## 4. 未覆盖 / 后续

- 当前是 review 级资产，不是正式美术验收。
- 核心四态共用 `waste_core` 底模，只叠加状态环/脚手架/警灯；后续可做真正的模型差异。
- sensing/comm/state 在 64px 下的识别度仍需实机密集场景复测。
- 未做正式平衡与性能基准（资产只影响渲染）。
