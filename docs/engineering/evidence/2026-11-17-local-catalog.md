# 证据：本地 SQLite 派生目录

- 日期：2026-11-17
- 决策：ADR-0035
- 依赖：`Microsoft.Data.Sqlite 11.0.0`
- 命令：

```powershell
dotnet run --project src/SeedField.Headless -- --catalog-rebuild logs --catalog-db $db
dotnet run --project src/SeedField.Headless -- --catalog-list --catalog-db $db
dotnet run --project src/SeedField.Headless -- --benchmark-jint-parallel 100 --ticks 10 --catalog-db $db
dotnet run --project src/SeedField.Headless -- --catalog-check --catalog-db $db
```

## 输出

```text
CATALOG_REBUILD db=... replays=1 saves=0 skipped=11 missing=0 errors=0
CATALOG db=... replays=1 saves=0 missingReplays=0 missingSaves=0 benchmarks=0 packages=0
REPLAY path=...\logs\replay-demo.json ticks=20 hash=0x2bc626aab35b6fdf size=10194
JINT_PARALLEL devices=100 ... avgTickMs=2.25 ticksPerSec=444.3 ...
CATALOG_RECORDED benchmarkId=1 db=...
CATALOG db=... replays=1 saves=0 ... benchmarks=1 packages=0
BENCH id=1 kind=jint-parallel devices=100 avgMs=2.25 ticks/s=444.3 dof=32 gcServer=True
CATALOG_CHECK db=... integrity=ok replays=1 saves=0 missing=0 benchmarks=1
```

- `skipped=11`：扫描目录里的非 catalog JSON/无效 JSON 被跳过，不影响回放索引；
- 回放文件仍是权威数据，目录只存路径/哈希/元数据；
- 基准记录只写元数据，不影响基准计时。

## 测试

`tests/SeedField.Catalog.Tests`（8 个）：

- replay upsert/list；
- replay + world save + game save 索引；
- 缺失文件标记而不是删除；
- 无效 JSON 跳过并报告；
- 基准写入/排序/并发；
- 损坏数据库备份 + 重建；
- 未来 schema 拒绝。

全量：**383 tests**（256 simulation / 8 catalog / 19 repository / 100 Runtime.Js）。

## 难点与边界

- **派生而非权威**：删除 DB 不影响世界；仿真不读 DB。
- **迁移**：`user_version` 逐版本事务迁移；未来版本拒绝，不静默重建。
- **损坏**：integrity check + SQLite 头检查；`.corrupt-*` 备份；连接池清理与
  文件移动重试。
- **对账**：`missing=1` 标记，不物理删除；重建扫描 `*.json` 并跳过无效文件。
- **并发**：每操作独立连接、WAL、busy timeout 5s；并发测试 8 个写入。
- **路径**：`COLLATE NOCASE`；完整路径入索引。
- **大文件**：只存元数据 + SHA-256，不存 blob；重建是显式操作。
- **隐私**：默认 `%LOCALAPPDATA%`，不上传；`.db`/`.sqlite*` 不入库。
- **未做**：事件全文检索、目录跨设备同步、包执行、把目录当存档备份。
