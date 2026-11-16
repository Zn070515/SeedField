# 数据模型、存档、蓝图、程序和离线文明包

## 五类数据边界 [PROPOSED]

1. **Definition**：元件/模块/能力规则，属于游戏版本化基础数据。
2. **BlueprintVersion**：玩家确认的设备/模块/程序设计，是可以导出和复用的不可变版本。
3. **DeployedInstance**：在本局世界中真实存在的对象，引用其部署版本且保存当前库存、模块损伤、记忆和控制权。
4. **WorldSave**：区域、时间、文明关系、库存、关键事件、随机种子与恢复状态。
5. **OpponentPackage**：离线可移植的文明设计/程序/策略与兼容元数据，导入后才生成当前世界实例。

**不能**将某个玩家本地完整世界存档直接当成默认公开导出的对手包，防止泄漏个人路径、私有资产及历史记录。

## 推荐的存档目录概念 [PROPOSED]

```text
world-save/
  manifest.json       # 存档版本、游戏版本、时间、随机种子
  regions/            # 区域真实状态或分片状态
  civilizations/      # 各文明持久状态、关系与合法知识
  blueprint-registry/ # 设计版本及引用
  controllers/        # 程序状态与部署程序 ID
  recovery/           # 不可毁核心、五分支最新确认产品、保底 E/M
  events/             # 可复现/调试所需事件或摘要
```

这里是逻辑组织建议，不要求磁盘一定散落为多个文本文件。正式产品可能使用紧凑二进制存档、版本迁移和压缩。

## 对手包建议目录 [PROPOSED]

```text
seed-opponent-package/
  manifest.json
  blueprints/
  programs/
  strategies/
  assets/        # 仅受限/可选资产
```

建议最少元数据：`schemaVersion, packageId, version, creatorDisplayName, engineCompat, requiredCapabilities, contentHash, maxRuntimeProfile, entryPoints, licenses`。其中作者信息需可选、避免强制实名/账号。[PROPOSED]

## 版本与兼容性

- 引擎内容版本、脚本 API 版本、蓝图 schema 版本、存档版本分别管理；不要使用一个混乱的大版本号控制所有迁移。
- JS API 修改时必须有语义版本和错误提示；已存对手包不能悄悄改行为。
- 世界保存时记录**实际部署程序/装配版本**，而非仅记录工坊“当前最新版”。
- 导入包的版本更新不应隐式覆盖现存对手文明；允许新存档使用新包、旧存档保留旧引用。[PROPOSED]
- 必须有恢复/迁移失败时的完整性提示和可回退存档。

## 安全与隐私

- 检查包大小、入口数量、递归展开上限、相对路径、禁止 `../` 等路径穿越。
- 任何外部包代码不得执行 DLL、系统命令或获得存档目录访问权。
- 只有在安全隔离验证通过后才能开放“执行未知第三方 JS”路径。
- 游戏离线也能浏览内置内容、制作蓝图、游玩所有核心功能。

## 最小示范 manifest（仅示范，无格式冻结）

```json
{
  "schemaVersion": 1,
  "packageId": "example.offline-civilization",
  "version": "0.1.0",
  "kind": "offline-opponent",
  "supportedApi": "design-preview",
  "entryPoints": ["programs/controller.js"],
  "requiredCapabilities": ["tactical-control"],
  "contentHash": "PLACEHOLDER"
}
```

其运行时声明不可被信任，接收端仍必须做完整校验。

## 自动化测试

- 同世界版本读写后状态与核心区登记一致。
- 改设计蓝图不会无故改变已有实例；部署升级显式发生。
- 对手包内外部路径或异常文件被拒绝，不能穿透本地目录。
- 不兼容 API 版本拒绝执行并提供可理解错误。
- 存档迁移后保底五分支永远可以被恢复。
