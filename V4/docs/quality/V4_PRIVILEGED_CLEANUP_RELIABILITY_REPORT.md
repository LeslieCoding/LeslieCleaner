# V4 清理权限与执行可靠性报告

- 日期：2026-08-17
- 范围：文件垃圾、隔离区、注册表删除、隐私/卸载共用的 `ICleanEngine` 执行管线
- 结论：通过。安全目标要么完成并通过回读复验，要么返回稳定失败分类；不再把“调用未报错”当成清理成功。

## 实现结果

### 1. 结构化失败与管理员重试

`CleanedItem` 新增 `FailureKind`、`RequiresElevation`、`WasVerified`。失败被稳定归类为：

- `NotFound`
- `InUse`
- `RequiresElevation`
- `ProtectedResource`
- `BackupFailed`
- `VerificationFailed`
- `InvalidTarget`
- `SecurityGateBlocked`
- `Unknown`

权限判断不解析本地化错误文本。标准权限下的 ACL 拒绝返回 `RequiresElevation=true`，可由现有“以管理员身份重新启动”入口在用户确认 UAC 后重试同一清理；已经处于管理员模式仍被拒绝时，按受保护资源处理，不尝试接管 ACL 或 TrustedInstaller 所有权。没有新增任意命令执行器或任意路径提权助手。

### 2. 文件清理事务与后置复验

- 同卷使用原子移动进入隔离区。
- 跨卷先写唯一 `.partial`，复验长度后原子发布载荷，再删除源文件。
- 隔离元数据通过唯一临时文件和原子移动写入。
- 成功前独立确认原路径消失、隔离载荷存在且长度一致、元数据可反序列化且身份/路径/长度一致。
- 元数据或操作清单落盘失败时自动把载荷还原到源路径；回退失败会明确给出隔离载荷恢复位置。
- 恢复操作同样验证隔离载荷消失、目标存在且长度一致后才移除元数据。
- 文件被占用时返回 `InUse`，保留源文件，不伪报成功。
- 释放空间使用 `AllocatedSizeBytes ?? SizeBytes`，避免 2 KB 逻辑文件实际占用 8 KB 时仍只报告 2 KB。

### 3. 最终文件安全边界

生产依赖注入已注册 `WindowsCleanupTargetPolicy`，每个文件在进入隔离区前再次执行：

- 规范化绝对路径和卷根保护；
- 应用核心、配置、数据库、备份、隔离区边界保护；
- 符号链接、目录联接和重解析点越界保护；
- 启动文件、分页/休眠文件及 Windows 核心目录保护；
- `SfcIsFileProtected` Windows Resource Protection 查询。

任何提权状态都不会绕过上述边界，也不会修改 ACL、接管所有权或绕过 WRP。

### 4. 注册表执行闭环

- 删除前按 ID 直接取得备份，强制核对键/值精确范围、注册表类型与可恢复状态，并重新验证载荷存在、大小与 SHA-256；损坏备份在任何写操作前拒绝。
- 删除前识别 TrustedInstaller/WRP 目标并返回 `ProtectedResource`。
- Unauthorized/SecurityException 在标准权限下返回 `RequiresElevation`；管理员模式仍失败时返回 `ProtectedResource`。
- RegistryEngine 删除后重开键回读；CleanEngine 再做一次独立回读。
- 首次复验发现目标仍存在后重新打开并二次确认；确认失败时停止流程、恢复备份、再次确认恢复态，并记录 `RestorationAttempted` / `Restored` 审计字段，最终仍返回失败而不是假成功。
- 即使注册表删除失败，结果仍保留 `BackupId`，用户可显式恢复并审计。
- 标准权限失败可在用户二次确认后整体 UAC 重启；一次性恢复文件最多 200 项、10 分钟有效、随机令牌且至多消费一次。管理员实例重新扫描后只恢复仍存在项目的勾选，绝不自动删除。
- 提权重启会丢弃旧页面、自动扫描、旧恢复令牌和安装期可信基线参数，避免页面竞态和一次性安全授权重放。

## 测试数据

| 验证 | 结果 | 证据 |
|---|---:|---|
| 最终聚焦测试第 1 轮 | 43/43 通过 | `v4-privileged-final-round1.trx` |
| 最终聚焦测试第 2 轮 | 43/43 通过 | `v4-privileged-final-round2.trx` |
| V4 全解决方案 Release 构建 | 0 警告 / 0 错误 | `LeslieCleaner.V4.sln` |

聚焦范围包含：隔离/恢复、缺失目标、文件占用、逻辑字节与分配字节差异、管理员重试分类、注册表备份删除恢复、32/64 位视图、WRP/TrustedInstaller、防护闸门、回收站清理既有闭环与文件最终安全策略。

## 验收边界

- “扫描到即可清理”定义为：安全目标执行并复验成功；不安全或当前无法处理的目标必须返回可操作的精确原因。
- 被其他进程锁定的文件不会强制终止进程或注入解锁；返回 `InUse`，关闭占用程序后重试。
- WRP、Windows 核心、启动资源、应用自身安全数据不会因管理员权限而被强制删除。
- 清理成功数与释放字节只统计 `WasVerified=true` 的成功项。
