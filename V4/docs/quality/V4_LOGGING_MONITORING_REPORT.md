# V4 日志与性能监控模块报告

日期：2026-08-17  
需求基线：`26_Logging_And_Monitoring_Architecture.md`  
实现范围：Core 必要契约、Infrastructure/Diagnostics、Infrastructure/Logging、Infrastructure/Monitoring、聚焦测试。

## 交付结果

- 新增统一 `IPerformanceMonitor`，提供主动/常规/空闲三档固定采样周期：1 秒、5 秒、30 秒。
- 采集当前进程 CPU、Working Set、Private Bytes；Windows 下通过进程 I/O 计数器计算磁盘读写吞吐。
- 不伪造进程网络数据：轻量监控未启用 ETW 时，收发速率以 `IsAvailable=false`、`Value=null` 和明确原因输出。
- 采样历史采用固定容量 FIFO，默认最多 360 条；快照读取返回不可变副本，不向调用方暴露内部集合。
- CPU/私有内存告警要求连续超阈值，且按规则去抖；订阅方异常被记录，不会终止采样循环。
- 后台循环支持模式动态切换、幂等启动、取消停止和异步释放；采集异常会记录并安全结束，不产生失控重试循环。
- Serilog 保留原有异步文件 Sink，并新增可配置最小级别、保留文件数、文件大小上限；默认 100 MiB 后按大小滚动，仍按天滚动。
- 新增完成日志的原子 ZIP 归档维护：先写临时归档、重新打开校验条目长度、发布归档，最后才删除源日志；取消时不修改源文件。
- 新增崩溃循环保护：默认 5 分钟内第 3 次崩溃后禁止立即重启；稳定运行后可清除状态。崩溃报告仅保存技术字段并脱敏路径、凭据和邮箱。
- 全局异常挂钩保持异常日志，并接入可选崩溃报告；挂钩幂等，崩溃报告 I/O 失败被隔离，避免二次崩溃。
- 诊断 ZIP 在原有日志、错误汇总、系统信息、配置快照和 SHA-256 完整性清单基础上新增：
  - `performance-history.json`
  - `crash-records.json`
  - `diagnostics-summary.txt`
- 所有新增诊断条目均进入现有脱敏与完整性清单流程。

## 需求对照

| 第 26 章要求 | V4 状态 | 证据 |
|---|---|---|
| 异步结构化日志 | 已实现 | 既有 Serilog Async Sink 与 `ILogService` 保留 |
| 级别、按日/大小轮转、保留 | 已实现 | `LoggingOptions`、`SerilogLoggingBootstrapper` |
| 旧日志压缩 | 已实现为显式维护任务 | `LogArchiveMaintenance` 原子压缩与校验 |
| CPU/内存/磁盘监控 | 已实现 | `ProcessPerformanceMetricCollector` |
| 网络监控 | 明确不可用而非伪造 | 每条快照返回 unavailable 原因；未引入高开销 ETW |
| 1/5/30 秒采样 | 已实现 | `PerformanceSamplingMode`、`IntervalFor` |
| 阈值、连续判定、去抖 | 已实现 | `PerformanceMonitor.EvaluateAlerts` |
| 有限历史、取消与释放 | 已实现 | 固定容量 Queue、`StopAsync`、`DisposeAsync` |
| 崩溃记录与重启保护 | 已实现 | `CrashLoopProtector`、`GlobalExceptionHandler` |
| ZIP/TXT/JSON 诊断 | 已实现 | ZIP 容器内同时包含 TXT 与 JSON；原有 ZIP 接口兼容 |
| 隐私与完整性 | 已实现 | 脱敏、路径约束、SHA-256 manifest、原子发布 |
| 性能数据库、日志数据库 | 本次未修改 | 父任务明确禁止触碰 Data；由数据库模块统一交付 |
| 模块级 CPU、磁盘队列 | 未伪造 | .NET 轻量进程采集无法可靠归因；保留为更高开销 ETW/系统计数器扩展项 |

## 测试数据

### 第一轮：功能、异常、诊断、隐私、兼容回归

- 结果：20/20 通过，0 失败，0 跳过。
- TRX：`artifacts/test-results/v4-logging-monitoring-round1-final.trx`
- 覆盖：新增监控测试、既有 `DiagnosticsServiceTests`、既有 `LogServiceTests`。
- 测试期间先发现并修复一项真实缺陷：`crash-loop-state.json` 曾被 `crash-*.json` 模式误识别为崩溃报告；最终 TRX 是修复后的结果。

### 第二轮：性能、边界、取消、泄漏风险

- 结果：11/11 通过，0 失败，0 跳过。
- TRX：`artifacts/test-results/v4-logging-monitoring-round2.trx`
- 覆盖：2,000 次采样开销门槛、历史容量、告警去抖、取消不留快照、停止后不再采集、采集异常、真实进程指标、网络 unavailable、压缩取消、崩溃隐私与诊断格式。
- 第二轮整个测试类由 TRX 记录约 455 ms 的测试执行时间；其中性能用例自身门槛为 2,000 次采样低于 2 秒，且历史始终限制为 32 条。

## 构建证据与并行状态

- 本模块 Infrastructure Release 构建曾完成：0 warning、0 error。
- 本模块 Tests 隔离编译（不重建并行项目）完成：0 warning、0 error。
- 最终全解决方案 Release 集成构建中，Core、Data、Infrastructure、各功能 Modules、Updater、PluginHost 均成功；当前唯一阻断为并行 UI `MainViewModel.cs(77)` 调用新增签名时缺少 `generation` 参数（0 warning、1 error）。该文件不属于本模块范围，因此没有越界修改。
- 最终全解决方案构建应在 UI 并行模块收口后由总控再次执行；本报告不把部分成功误记为全解通过。

## 主要文件

- `src/LeslieCleaner.Core/Abstractions/IPerformanceMonitor.cs`
- `src/LeslieCleaner.Core/Models/PerformanceMonitoringModels.cs`
- `src/LeslieCleaner.Infrastructure/Monitoring/IPerformanceMetricCollector.cs`
- `src/LeslieCleaner.Infrastructure/Monitoring/ProcessPerformanceMetricCollector.cs`
- `src/LeslieCleaner.Infrastructure/Monitoring/PerformanceMonitor.cs`
- `src/LeslieCleaner.Infrastructure/Logging/LoggingOptions.cs`
- `src/LeslieCleaner.Infrastructure/Logging/LogArchiveMaintenance.cs`
- `src/LeslieCleaner.Infrastructure/Logging/CrashLoopProtector.cs`
- `src/LeslieCleaner.Infrastructure/Diagnostics/DiagnosticsService.cs`
- `src/LeslieCleaner.Tests/Diagnostics/LoggingMonitoringTests.cs`

