# V4 Startup Manager 生命周期实现与验证报告

日期：2026-08-17  
依据：`PROMPT/Cleaner/LeslieCleaner_Development_Bible/Main/19_Startup_Manager_Architecture.md`  
范围：`LeslieCleaner.Modules.Startup`、Startup 聚焦测试；未修改 UI、Data、Infrastructure Security/Logging。

## 1. 交付结论

本轮在原有注册表 Run/RunOnce、启动文件夹、计划任务、服务/驱动扫描与备份式禁用/恢复基础上，完成以下 P1 生命周期能力：

- 受控延迟启动事务：只允许 `UserApplication`，必须显式确认；固定支持 30/60/120/300 秒。
- 原入口先备份并禁用，系统状态重读确认后，才创建 Task Scheduler 登录延迟任务。
- 创建、验证、持久化、超时或取消失败时，删除半成品任务并自动恢复原入口。
- 恢复延迟启动时先删除受控任务、恢复原入口并重读状态；恢复失败会尝试重建延迟任务，避免应用彻底失去启动路径。
- 七日扫描缓存、显式强制刷新、模块变更即时失效。
- 操作历史、开机趋势、估算影响分和 JSON 原子导出报告。
- 禁用/可恢复删除统一安全入口：受保护项精确阻止、权限失败结构化为 `RequiresElevation`、二次确认后才允许通过宿主 broker 提权重试、成功前必须重读系统状态。
- 数据诚实性：未接入 ETW 时返回 `Unavailable`；启发式影响值始终为 `Estimated`，不伪装实测值。

## 2. 架构要求对照

| Bible 要求 | V4 实现 | 状态 |
|---|---|---|
| 多来源扫描与并行聚合 | 保留 `StartupManagerEngine` 的注册表、启动文件夹、计划任务、服务/驱动扫描 | 已有并保留 |
| Microsoft/User/Unknown 分类、驱动保护 | 保留签名/路径/发布商多因素分类；新增动作层对 Microsoft、驱动、保护项给出具体阻止原因 | 达成 |
| 禁用前备份、恢复与记录 | 保留备份式禁用/恢复；新增动作结果验证、历史记录 | 达成 |
| 延迟启动 Task Scheduler | `WindowsDelayedStartupTaskController`；使用 `ArgumentList`，不经 shell；受控命名空间 `\LeslieCleaner\Delayed\*` | 达成 |
| 延迟启动安全限制 | 仅 `UserApplication`；Microsoft、驱动、服务、保护项、既有计划任务均阻止 | 达成 |
| 延迟事务失败恢复 | 创建/验证/写状态失败、超时、取消均执行删除任务 + 恢复原入口 | 达成 |
| 缓存 7 天 | `StartupLifecycleManager.ScanAsync` 默认七日缓存；`ForceRefresh` 显式刷新 | 达成 |
| 变更后缓存失效 | 配置/恢复延迟启动、显式失效均删除缓存 | 达成 |
| 启动历史与趋势 | 最多 1000 条操作历史、180 个外部观测点、首末变化趋势 | 达成 |
| 优化报告 | 当前项数、禁用/延迟数、估算前后影响分、趋势质量、近期操作，原子导出 JSON | 达成 |
| 扫描 <5 秒 | 本机真实扫描 363 项，994.005 ms | 达成（本机证据） |
| 异常、取消、超时、幂等 | 可取消、30 秒默认超时、2 分钟硬上限、相同配置幂等命中、失败恢复 | 达成 |
| 发现后可执行禁用/删除 | `StartupVerifiedActionManager.ExecuteAsync`；删除仅开放给可完整恢复的注册表/启动文件夹来源 | 安全范围内达成 |
| 权限不足可提权重试 | `RequiresElevation` + 二次显式确认 + 2 分钟短时请求 + `IStartupElevationBroker` + 结果复查 | 契约与模块流程达成；宿主必须注册 broker |
| 执行后不得假成功 | `IStartupSystemStateProbe` 重读注册表、文件、任务 XML、服务 Start 值；仍启用则返回失败并尝试恢复 | 达成 |

## 3. 安全设计

### 3.1 延迟任务防注入

- 可执行路径必须是规范化绝对 `.exe` 路径且文件存在。
- 拒绝 NUL、CR、LF 和不完整引号。
- 任务名由启动项 ID 的 SHA-256 派生，用户输入不能控制任务命名空间。
- `schtasks.exe` 参数通过 `ProcessStartInfo.ArgumentList` 逐项传入，`UseShellExecute=false`。
- Windows `/DELAY` 使用 `mmmm:ss` 格式。
- 任务运行级别为 `LIMITED`，不借延迟任务提升权限。

### 3.2 权限与保护

- `StartupChangeResult` 新增 `RequiresElevation`、`Verified`、`BlockedReason`，调用方无需解析模糊成功文本。
- Microsoft/Windows、驱动、硬件/OEM、保护项不会因提权而绕过。
- 提权重试必须再次明确确认；请求带随机 ID 和两分钟有效期。
- 当前模块不自行启动任意提升权限命令；宿主未注册 `IStartupElevationBroker` 时明确失败且不改变系统。
- broker 返回成功仍不是最终成功，系统状态探针必须再次确认启动项已不再启用。

### 3.3 持久化

缓存、延迟记录、历史、观测和导出报告均采用同目录临时文件 + 原子替换，避免半写 JSON 被当作有效状态。延迟记录保存原启动项快照，用于恢复后的真实来源复查。

## 4. 数据质量

- 影响评分基于规则引擎，只能标记 `Estimated`。
- 未提供开机观测时，趋势返回 `Unavailable`，说明“未采集或伪造 ETW 数值”。
- 外部观测必须显式声明 `Measured`、`Estimated` 或 `Unavailable`；Measured/Estimated 必须有正数时长，Unavailable 禁止携带时长。
- 报告不会使用文档示例中的 45s→28s 等数字作为真实结果。

## 5. 测试证据

### Release 编译

`LeslieCleaner.Modules.Startup.csproj -c Release --no-restore`

- 警告：0
- 错误：0

### 第一轮：功能、异常、工作流

TRX：`V4/artifacts/test-results/v4-startup-round1-final.trx`

- 60/60 通过
- 覆盖原有真实注册表禁用/恢复、计划任务/服务异常显性化、分类/签名，以及新增缓存、延迟事务、恢复、取消、超时、注入、数据质量、保护和提权流程。

### 第二轮：安全、恢复、性能

TRX：`V4/artifacts/test-results/v4-startup-round2-security-performance.trx`

- 17/17 通过
- 真实扫描：363 个启动项
- 总耗时：994.00 ms
- 均值：994.005 ms
- Bible 目标：<5 秒
- 结果：本机本轮余量约 4.006 秒

关键回归包括：

- 执行器谎报成功时，状态复查拒绝成功并触发恢复。
- Unauthorized/Access denied 转为 `RequiresElevation=true`。
- 提权需第二次确认，broker 结果仍需状态复查。
- Microsoft/驱动保护项不调用执行器和 broker，并返回精确原因。
- 注册表/启动文件夹删除保持可恢复；服务等缺少完整删除快照的来源被阻止。
- 任务创建失败与超时均恢复原入口。
- 缓存七日命中、强制刷新、过期刷新、变更失效。
- 无实测数据时趋势不伪装为 Measured。

## 6. 明确边界

- 开机趋势的 `Measured` 数据需要宿主或未来性能采集器提交；本轮没有声称已实现 ETW 采集。
- 非管理员进程遇到权限不足会返回 `RequiresElevation=true`。当前宿主尚未向 Startup 注入按项 `IStartupElevationBroker`，因此不能声称按项提权已经端到端完成；未注入时明确返回“未配置”，不会假成功。宿主已有 `ElevationHelper` 的全应用 UAC 管理员重启入口，Startup UI 需要把 `RequiresElevation` 导向该真实入口，并在管理员进程内由用户重试；管理员进程中的操作仍须经过状态探针验证。
- 删除仅对能依靠现有备份完整恢复的注册表 Run/RunOnce 与启动文件夹项开放。计划任务、服务和驱动的“删除”被阻止，仍可按安全策略使用禁用。
- 七日缓存对本模块执行的变更立即失效；外部程序修改启动项时，用户可使用 `ForceRefresh` 获取实时状态。

## 7. 主要代码

- `StartupLifecycleModels.cs`：缓存、延迟、历史、趋势、报告和抽象契约。
- `StartupLifecycleManager.cs`：缓存与延迟启动事务编排。
- `WindowsDelayedStartupTaskController.cs`：Task Scheduler 受控执行与命令安全。
- `StartupVerifiedActionManager.cs`：保护、禁用/删除、提权重试和状态复查。
- `StartupManagerEngine.cs`：保持现有 API，接入 `IStartupEntryOperations` 并返回结构化权限状态。
- `ServiceCollectionExtensions.cs`：模块 DI 注册。
- `StartupLifecycleV4Tests.cs`、`StartupVerifiedActionsV4Tests.cs`：新增聚焦验证。