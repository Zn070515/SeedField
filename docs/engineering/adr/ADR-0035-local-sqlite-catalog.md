# ADR-0035：本地 SQLite 派生目录（回放/存档/基准）

- 状态：**Accepted**
- 日期：2026-11-17
- 关联决策：ADR-0023（证据/门禁）、ADR-0030（回放）、ADR-0034（离线安全）
- 前置：JSON 世界/游戏存档、回放格式、基准 CLI

## 背景

项目坚持完整离线单机：没有开发者服务器，设计基线明确排除在线数据库。存档与
回放是版本化 JSON，基准结果目前只写日志。需要在不引入服务端的前提下提供：

- 回放/存档的列表、搜索、缺失检测与元数据；
- 基准历史与回归趋势；
- 可删除、可重建、损坏可恢复的本地索引；
- 数据库 schema 迁移与并发写入的明确边界。

这些是“数据库任务”的真正难点：**派生数据与权威状态分离、迁移、损坏恢复、
重建/对账、并发、路径大小写、大文件、隐私与离线约束**。

## 决定

### 1. 只做派生目录，不做权威状态

- 新增 `SeedField.Catalog`，依赖 `Microsoft.Data.Sqlite 11.0.0`；
- 只索引元数据：路径、SHA-256、schema、Tick/回放 Tick 数、最终哈希、文件大小、
  最后写入时间、缺失标志；不存世界/回放正文；
- 仿真、存档、回放从不读取目录；删除 SQLite 文件只会丢失索引，JSON 仍是权威；
- 目录可完全由文件系统重建。

### 2. 表与 schema 迁移

- 表：`replays`、`saves`、`packages`、`benchmark_runs`；
- `PRAGMA user_version` 作为 schema 版本；迁移按版本逐段在事务中执行；
- **未来版本拒绝启动**（`UNSUPPORTED_CATALOG_VERSION`），不静默重建，避免
  降级时悄悄丢掉更新版本的索引。

### 3. 写入与并发

- 每次操作独立连接；WAL + `busy_timeout=5000`；
- 批量重建在单个事务里完成；
- `Pooling=false`：目录不是热路径，且避免 Windows 上残留连接句柄阻碍损坏恢复；
- 损坏恢复前清理连接池并重试文件移动。

### 4. 损坏恢复与对账

- 打开时 `PRAGMA integrity_check`；非 `ok` 或非 SQLite 头 → 把主文件与
  `-wal`/`-shm` 移到 `.corrupt-<timestamp>`，重建空库并暴露备份路径；
- 重建扫描 `*.json`，按内容分类 replay/game save/world save；
- 先 `missing=1` 标记全部，再 upsert 本次见到的文件；删除/移动的文件保留为
  `missing=1`，查询默认过滤；
- 无效/无关 JSON 跳过并收集错误，不中断整次扫描；
- 路径使用 `COLLATE NOCASE`，避免 Windows 大小写变体重复。

### 5. 基准记录与 CLI

- `--catalog-rebuild <dir> [--catalog-db <db>]`：扫描并重建文件索引；
- `--catalog-list`：回放/存档/基准摘要；
- `--catalog-check`：完整性 + 计数 + 缺失数；
- `--benchmark-jint-parallel ... --catalog-db <db>`：基准完成后写入
  `benchmark_runs`（设备数、tick、obs/decide/step、ticks/s、峰值内存、DOF、
  GC 模式、git commit、UTC 时间）。

### 6. 隐私与仓库卫生

- 数据库只在本机 `%LOCALAPPDATA%\SeedField\catalog.db`（或显式路径）；
- 不上传、不联网、不作为服务器；
- `.gitignore` 排除 `*.db`/`*.sqlite*`；测试使用临时目录并清理；
- 索引中的时间戳是应用元数据，不进入仿真 Tick/哈希。

## 非目标

- 不把世界状态、实体、事件流正文放进数据库。
- 不做在线/多人/同步数据库。
- 不做事件全文检索、跨设备同步或包执行。
- 不提交任何数据库文件；不把目录当存档备份。
- 不替代 JSON 存档迁移/回放验证。

## 后果

- 可在无服务器下查询回放/存档/基准历史；目录可随时删除重建。
- 目录 schema 与世界存档 schema 独立；新字段需迁移与测试。
- 索引重建需要读取并哈希 JSON 文件；大目录是显式运维操作，不在 Tick 热路径。
- 新增 SQLite 依赖与原生库；CI/发布需包含 `SQLitePCLRaw` 资产。
- 损坏恢复会保留 `.corrupt-*` 文件供人工检查，不自动删除。

## 验证

- `SeedField.Catalog.Tests`（8 个）：
  - upsert/list、回放/世界存档/游戏存档索引；
  - 缺失标记不删除、无效 JSON 跳过；
  - 基准写入/读取/并发写入；
  - 损坏库备份重建、未来 schema 拒绝。
- CLI：`--catalog-rebuild`、`--catalog-list`、`--catalog-check`、
  `--benchmark-jint-parallel --catalog-db`。
- 全量 383 测试、CI 通过。

## 回滚

删除 `SeedField.Catalog`、两个 CLI 与 `benchmark_runs` 记录；JSON 存档/回放
不受影响。数据库文件可直接删除。
