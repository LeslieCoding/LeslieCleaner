# Prompt / Development Bible 实现审计

审计日期：2026-09-01（Asia/Shanghai）  
审计范围：`D:\SoftWare\LeslieCleaner\PROMPT` 与 `D:\SoftWare\LeslieCleaner\V4`

## 结论先行

初始盘点发现目录共有 73 个 Markdown/TXT 文件，其中 41 个编号文件为 0 字节空文件。2026-09-01 已将这 41 个文件补齐为可执行规范，并新增 `CleanMyPCNext_Development_Bible_INDEX.md` 作为总索引。现在 73 个文件均有内容，但“规范文件已生成”不等于“所有功能已经实现”。

“已实现”只表示源码和自动化证据覆盖的范围；不等于所有硬件、所有用户权限、人工视觉和发布签名均已验收。

## 当前可审计范围

| Prompt 范围 | 文件数 | 当前状态 | 证据/边界 |
|---|---:|---|---|
| 顶层 Feature Enhancement / UI Performance prompt | 4 | 部分实现 | 启动项分类、安装体验、批量交互和 UI 性能已有代码；商业级视觉、全部批量页面和全部安装矩阵仍需逐项验收 |
| Main 00–07：总则、架构、编码、性能、发布 | 8 | 基础实现，部分验收 | WPF/MVVM/DI、取消、日志、Release 构建存在；冷启动和性能结果仅是受控机器证据，不能外推 |
| Main 08、15：数据库设计 | 2 | 已实现（自动化范围） | Schema v2、迁移回滚、在线备份、事务、分页和 20/20 聚焦测试；仍需真实升级矩阵 |
| Main 09、10、12：API、架构标准、质量 | 3 | 部分实现 | 分层和测试体系存在，但“每条规范规则”没有全部自动化静态门禁 |
| Main 11、23：AI | 2 | 已实现当前 P1 范围 | 本地规则、可选兼容 Provider、脱敏、危险输出过滤、离线降级和测试已存在；不能宣称自然语言模型质量或外部服务长期可用 |
| Main 13：UI/UX | 1 | 代码级实现 | 页面、导航、批量确认、可访问性属性已覆盖；100%–300% DPI、屏幕阅读器和人工视觉仍未最终验收 |
| Main 14：安装部署 | 1 | 已实现当前用户闭环 | Inno Setup 7.1.0 无警告编译，当前用户安装/启动/静默卸载通过；所有用户/UAC、升级回滚、签名仍待隔离机 |
| Main 16：清理引擎 | 1 | 已实现自动化范围 | 扫描、隔离、备份、取消、复验和失败分类已有测试；第三方系统对象仍受 Windows ACL/UAC 限制 |
| Main 17：注册表 | 1 | 已实现并持续加固 | 32/64 位物理视图、精确范围且完整性已验证的备份、双重回读、失败日志、UAC 恢复选择和大小写一致性已验证；HKLM 受保护对象仍需管理员/UAC 实机矩阵 |
| Main 18：智能卸载 | 1 | 部分实现 | 发现、指纹和残留安全清理存在；自动终止同名进程、完整失败恢复和所有第三方卸载器行为未承诺 |
| Main 19：启动项 | 1 | 部分实现 | Run、启动文件夹、计划任务、服务/驱动发现和安全禁用存在；Delayed、长期趋势和 ETW 实测缺失 |
| Main 20：隐私 | 1 | 已实现当前策略范围 | 多浏览器、Cookie 模式、时间范围、备份回滚和会话保护已有 4/4 聚焦验证；在线规则更新和所有真实浏览器组合仍有限制 |
| Main 21：文件分析 | 1 | 部分实现 | 大文件、重复、哈希、图片相似度和增量索引存在；驱动健康、长期缓存和完整 UI 分页仍待补齐 |
| Main 22：备份恢复 | 1 | 已实现核心范围 | 统一备份、哈希校验、冲突策略、锁定、保留和原子元数据存在；ZIP、计划备份、Recovery Mode 和完整报告仍待补齐 |
| Main 24：插件 | 1 | 部分实现 | 子进程隔离、命名管道、权限中介、超时/崩溃/资源限制已实现；发布者证书信任、行为扫描和官方更新源仍缺 |
| Main 25：更新 | 1 | 部分实现 | HTTPS、签名、哈希、增量/完整包、断点、回滚和迁移事务存在；真实供应链签名、全 OS/架构升级矩阵仍待验收 |
| Main 26：日志监控 | 1 | 已实现核心范围 | Serilog 文件日志、异步写入、结构化属性、性能采集、告警和诊断导出存在；外部 SIEM、长期运行和全部开销指标仍有限制 |
| Main 27：安全 | 1 | 已实现安全核心，持续加固 | 默认拒绝、路径/重解析点防护、审计哈希链、完整性基线、紧急只读和恢复条件存在；发布证书、硬件信任锚和跨账户安全模型仍需专项验收 |
| AI/Modules/UI/UX 编号文件 | 41 | 规范已补齐，功能按验收矩阵推进 | 本批次已写入目标、边界、性能、测试和验收；仍需将每条要求绑定源码和自动化证据 |

## 已有验证总览

- V4 Release 全解决方案：0 警告、0 错误。
- 普通自动化测试：433/433；插件宿主集成：4/4。
- 当前用户安装器真实闭环：安装、注册表页启动、静默卸载均退出 0；用户数据按策略保留。
- 注册表重点闭环：物理 32/64 位视图、备份、删除后回读、失败恢复和值名大小写一致性均有测试。
- 新板 USB 调试：Windows 当前能枚举健康的 `ADB Interface` 与 `OnePlus Ace 5`；主机尚无 `adb.exe`/`fastboot.exe`，因此不能声称 ADB 已授权。只执行了只读诊断，没有刷机或写入。

## 下一步执行顺序

1. 将新规范逐条绑定源码、测试、性能基线和已知限制，不以文档存在替代实现。
2. 优先落实 Residual Engine、Cache Rules、Rule Engine 和批量/历史/通知/搜索/收藏/撤销 UX。
3. 对 Main 18/19/21/24/25/27 的剩余 P1 建立独立测试和权限矩阵。
4. 在隔离 Windows 环境补做所有用户/UAC、升级回滚、签名、DPI/辅助功能和长稳测试。

## 2026-09-01 Incremental Verification

- Prompt inventory after detailed-contract expansion: 88 Markdown/TXT files including the index and Prompt Library; 69 numbered files; 0 empty files.
- Residual pipeline, rule query guard, and the new identity scorer: 22/22 passed.
- Broad regression excluding long-running Benchmark/Integration filters: 444/444 passed (latest run: 9m48s on 2026-09-02; prior run 434/434).
- A full/Integration run was started but safely stopped after sustained high-CPU file-index work without failure output. This is recorded as pending machine-dependent evidence, not as a pass.
- Release solution build after the scorer change: 0 warnings, 0 errors.
- Latest read-only USB probe: `NoDevice` (the board is not currently enumerated); no flash, install, or write operation was attempted.
- Read-only USB probe on 2026-09-02: Windows reports a healthy `ADB Interface` and `OnePlus Ace 5`, but no host `adb.exe`/`fastboot.exe` is installed. Probe classification was corrected to `DebugToolsMissing`, project-local portable tool discovery was added, and no device write was attempted.
- Cache Rules implementation increment: Smart Scan findings now carry stable RuleId, Provider, RuleVersion, and AccessState; structured scan logs include rule evidence. Focused Smart Scan tests: 14/14 passed; Release build: 0 warnings, 0 errors.
- Cache Rule Pack increment: Smart Scan definitions are now an embedded, versioned JSON Rule Pack with fail-closed validation for duplicate IDs, missing metadata, empty non-registry roots, and path traversal. Focused Rule Pack/Smart Scan tests: 16/16 passed; Release build: 0 warnings, 0 errors.
- Browser profile safety increment: Firefox profile traversal/rooted paths and reparse-point profiles are denied; cache sizing and cleanup use cancellable, reparse-safe traversal. Focused Browser tests: 13/13 passed; Release build: 0 warnings, 0 errors.
- Startup elevation recovery increment: UAC restart now returns to Startup Manager and restores the last stable item ID only after the real list is loaded. Control-character/oversized resume IDs are rejected, UAC cancellation is reported without claiming a change, and target-not-found remains an honest state. Focused Startup/UI tests: 26/26 passed; Release build: 0 warnings, 0 errors.
- Residual traversal safety increment: residual directory sizing and cleanup no longer use unbounded recursive enumeration; traversal is cancellation-aware, skips reparse points, and logs inaccessible subtrees while retaining quarantine/rollback semantics. Focused Residual/Startup/UI tests: 33/33 passed; Release build: 0 warnings, 0 errors.
- Durable logging increment: general events remain on a bounded backpressure async sink while Warning/Error events (including registry DeleteStart/DeleteFinish) are also written synchronously as structured JSONL. Serilog self-diagnostics have an independent fallback file, reserved fields cannot spoof EventKind, active logs permit cooperative reads, and both the log viewer and diagnostic export include audit JSONL. Focused logging/registry/UI tests: 33/33 passed; broad regression: 444/444 passed; Release build: 0 warnings, 0 errors.
- Registry accuracy increment (2026-09-02): Run command parsing now resolves bare Windows/PATH executables, supports `.exe/.com/.cmd/.bat/.scr`, and refuses unresolved environment variables rather than creating deletion candidates. Cache fingerprints now mirror physical HKCU/HKLM Classes views and correctly track font, COM, and command dependencies. Registry cleanup carries typed key/value metadata so legal `|` characters cannot alias a key with a value target; duplicate grouping and UI result mapping use the same unambiguous identity. Focused registry/Smart Scan/UI tests: 70/70 passed; Release build: 0 warnings, 0 errors.
- Registry privilege and backup-integrity increment (2026-09-02): permission failures now expose a user-confirmed whole-app UAC restart that stores a one-time, ten-minute, bounded target list, returns to Registry Cleaner, performs a fresh scan, and only reselects findings still present; it never auto-deletes. Corrupt/expired/wrong-token state is rejected and consumed once. Registry deletion now requires an exact key-vs-value backup scope and a fresh payload size/SHA-256 verification before mutation; damaged backups are rejected before any registry write. Precondition/write/readback ACL failures share stable `RequiresElevation`/`ProtectedResource` classification. UAC argument merging drops `--autoscan`, stale recovery tokens, and installer-only baseline provisioning so they cannot be replayed. Focused cross-module tests: 65/65 passed; Release build: 0 warnings, 0 errors.
