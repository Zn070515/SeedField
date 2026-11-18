# 废土正交俯视地图样片证据（2026-11-18）

## 变更

- 新增 `MindustryStyle` 样片场景：严格 90° 俯视、64 px 地块、四类地貌、四种矿脉、
  块状锈蚀建筑、俯视旋转单位与资源条。
- 美术方向确认：ADR-0041、决策 `A-02`。
- 该场景是表现层样片，不读取 `WorldState`，不改变仿真/存档/哈希。

## 运行

```powershell
dotnet build game/SeedField.Game/SeedField.Game.csproj
$env:DOTNET_gcServer = "1"
godot --path game/SeedField.Game res://Scenes/MindustryStyle.tscn -- --capture-art
```

实际输出：

```text
ART_SCREENSHOT_SAVED .../screenshots/art-mindustry-style.png
```

截图：`game/SeedField.Game/screenshots/art-mindustry-style.png`。

## 样片包含

| 元素 | 说明 |
|---|---|
| 地貌 | 焦土/沙地/灰烬/锈蚀四类区块，带碎石、裂缝、焦痕与残骸 |
| 矿脉 | 铜（橙）/铅（灰紫）/钛（氧化青）/煤（深灰），直接铺在地块 |
| 建筑 | 种子核心、电力、炮塔、钻机、中继、铸厂、敌对基地；块状轮廓 + 暖色状态灯 |
| 单位 | 玩家/敌对俯视剪影，按角度旋转 |
| HUD | 四种资源计数与样片声明 |

## 边界

- 全部为程序化占位，不是正式美术资产；标签明确 `DEMO DATA · NOT FINAL ART`。
- 地块/矿脉尚未绑定真实区域资源分布；正式接入时只读场景/仿真数据。
- 未做最低分辨率、低端 GPU 与色盲可读性验收。
- 现有 Main/WorldBackdrop 仍是旧斜俯视风格；迁移顺序见 ADR-0041。
