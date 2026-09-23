# V4 数据库 v2 实现与验证报告

日期：2026-08-16  
规范基线：`15_Data_Model_And_Database_Design.md`  
范围：`LeslieCleaner.Data`、`IDatabase`、数据库测试

## 结论

数据库架构已从 v1 升级到 v2，并保留原有 `file_index`、`scan_cache`、`quarantine_meta`、`residual_rules`、`settings` 的兼容性。数据库项目 Release 构建为 0 警告、0 错误；数据库聚焦测试最终 20/20 通过。

## Schema v2

新增规范要求的九张表：

- `UserSettings`
- `ScanRecords`
- `ScanItems`
- `CleanupRecords`
- `Applications`
- `StartupItems`
- `RegistryBackups`
- `OperationLogs`
- `AIRecommendations`

实现要点：

- 所有表使用规范中的 PascalCase 表名与列名。
- 时间使用 UTC ticks 存储，仓储读写统一转换为 UTC `DateTimeOffset`。
- `ScanItems.ScanRecordId -> ScanRecords.Id` 使用外键和级联删除。
- `CleanupRecords.ScanRecordId -> ScanRecords.Id` 使用外键和 `SET NULL`。
- 每次数据库连接均显式开启 `foreign_keys`，而非只在初始化连接设置。
- 数量、大小、布尔字段使用 `CHECK` 约束阻止非法值。
- 建立 Applications 名称/发布者、StartupItems 分类/启用状态、ScanItems 扫描/类型、OperationLogs 模块/时间及历史时间索引。

## v1 到 v2 迁移

迁移流程：

1. 读取 `schema_migrations`，拒绝高于当前程序支持版本的数据库。
2. 若是全新数据库，先应用兼容 v1 基础结构。
3. v1 升级前使用 SQLite Online Backup API 创建一致性快照。
4. 快照发布前执行 `PRAGMA integrity_check`；临时文件通过校验后才原子改名为正式备份。
5. v2 DDL、索引、旧 `settings` 数据迁移和版本记录在同一事务中完成。
6. 任一迁移语句失败会回滚事务、清理连接池，并用已校验快照恢复原数据库。

测试使用预制 v1 数据库验证了旧设置迁移、旧表保留、迁移前快照为 schema v1；另用故意畸形的 `Applications` 表触发真实 DDL/索引失败，确认最终数据库仍为 schema v1 且旧数据未丢失。

## 备份、校验与恢复

`IDatabase` 新增向后兼容 API：

- `CreateBackupAsync`：SQLite 一致性在线备份、完整性校验、临时文件原子发布。
- `VerifyBackupAsync`：只读打开并执行完整 `integrity_check`。
- `RecoverFromBackupAsync`：先校验输入和 schema 版本，再暂存、复验、原子替换；原数据库保留为 `pre-recovery` 回退副本，恢复后再次校验并重新初始化。
- `ExecuteInTransactionAsync`：为 Repository 工作单元提供自动提交、异常/取消自动回滚。

测试覆盖成功备份/恢复、回退副本、损坏备份拒绝且当前库不变、事务异常零落盘。

## Repository、分页与参数化

新增 `DatabaseV2Repository`：

- 设置使用参数化 UPSERT。
- 扫描头和全部扫描项在单一事务内保存。
- 扫描历史、扫描详情、应用查询使用 `LIMIT/OFFSET` 分页。
- 页码从 1 开始，页大小限制 1–500，防止无界加载。
- 应用查询对名称/发布者使用索引友好的精确匹配。
- SQL 注入样例作为设置 Key 写入时仅作为参数值保存，表结构保持完整。

## 隐私

- `UserSettings` 拒绝密码、Cookie、Authorization、API Key、Token、PrivateContent 等敏感键。
- `OperationLogs` 写入前对密码/Cookie/API Key/Token/Bearer Authorization 值脱敏，并限制单字段长度。
- Schema 未包含浏览器 Cookie、密码或私密正文专用字段；AI 表只保存结构化建议结果，不保存对话或私密内容。

## 性能结果

可重复数据集：单机 SQLite、本地临时目录、10,000 条 `ScanRecords`，预热后对普通分页查询采样 5 次并取中位数；大查询一次读取 10,000 个索引排序后的 Id。

- 环境：Microsoft Windows NT 10.0.26200.0
- .NET Runtime：8.0.30
- 普通分页/索引查询中位数：**0.151 ms**，目标 `<50 ms`，通过。
- 10,000 行大查询：**12.395 ms**，目标 `<500 ms`，通过。

结果为当前开发机实测，不推断其他硬件的绝对耗时；自动测试保留相同数据规模和硬阈值，可重复执行。

## 构建与测试证据

- `LeslieCleaner.Data.csproj` Release：0 警告，0 错误。
- 聚焦测试：20 通过、0 失败、0 跳过，总耗时 766 ms。
- TRX：`V4/artifacts/test-results/v4-database-v2-focused-final.trx`

覆盖的关键行为包括：WAL、幂等/并发初始化、v1→v2、迁移失败恢复、九表/索引、外键、种子规则、文件索引兼容、备份校验/恢复、事务回滚、UTC、分页、参数化、防敏感存储/脱敏及两档性能目标。

## 边界说明

本模块提供数据库基础设施和仓储 API。其他功能模块是否将全部业务历史接入这些表，需要在各自模块验收时继续核对；本报告不将“表已存在”误报为“所有上层模块均已接入”。