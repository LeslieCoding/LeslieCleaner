# LeslieCleaner V3 升级审计

## 交付范围

V3 位于本目录，与原始项目并存，不覆盖 `D:\SoftWare\leslieCleaner` 下的原始源码。
版本为 `3.0.0`，构建输出位于 `artifacts\bin`。

## 已修复的关键风险

| 范围 | 原始风险 | V3 控制措施 |
|---|---|---|
| 扫描 | 请求的扫描目标未被严格过滤；无权限目录可能被静默跳过 | 只扫描请求目标；不可访问路径计入错误并在界面明确标记结果不完整 |
| 扫描并发 | 同时扫描可能交叉写入状态 | `SmartScanEngine` 使用异步互斥锁；目录遍历跳过重解析点并逐目录记录失败 |
| 回收站 | 将回收站当普通文件处理，统计/清理不可靠 | 使用 `SHQueryRecycleBinW` 精确读取数量与字节数，使用 `SHEmptyRecycleBinW` 清空并回读验证 |
| 清理事务 | 进程中断可能遗失隔离或备份记录 | 清单在操作前创建，每一成功项后以写穿透临时文件原子替换；清理和回滚串行化 |
| 注册表 | 仅扫描 64 位视图；根键句柄泄漏；值级回滚会覆盖同键其它改动 | 支持 `HK*32`/`HK*64` 视图；全部句柄确定释放；值级快照按原类型恢复且不改动同键其它值 |
| 注册表权限 | 无权限删除可能被误报成功 | 删除后回读验证；权限不足返回显式失败，绝不报告假成功 |
| 启动项 | 备份在改动后写入；计划任务无还原记录；启动文件备份名冲突 | 状态变更前先备份，失败时移除记录；计划任务纳入备份；启动文件使用稳定哈希命名 |
| 文件粉碎 | 已从待执行列表移除的任务仍可能被工作线程执行 | 工作线程启动前再次在锁内确认任务仍为 `Pending` |

## 权限和不可逆操作

- 受 Windows 保护的注册表键不会作为可修复项展示；其他键删除失败会明确要求管理员权限。
- 回收站清空不可回滚，必须由调用方显式设置 `AllowIrreversibleRecycleBin`。
- 文件清理进入隔离区；注册表键和值都必须先产生匹配备份，删除接口会再次核验备份目标。
- 注册表路径可用标准 64 位前缀（例如 `HKCU`）或 32 位后缀（例如 `HKCU32`）。

## 验证命令

```powershell
Set-Location D:\SoftWare\leslieCleaner\V3
dotnet build LeslieCleaner.V3.sln -c Release --no-restore
dotnet test LeslieCleaner.V3.sln -c Release --filter "Category!=LongRunning"
```

常规功能回归不包含环境型性能压测。以下测试类已标记 `Category=LongRunning`，应在专用机器单独运行：

- `EngineBenchmarkTests`
- `ModuleBenchmarkTests`
- `PerformanceBenchmarkTests`
- `UninstallerBenchmarkTests`
- `PerformanceIntegrationTests`

这些测试会创建大量文件、枚举真实磁盘或执行长时间连续扫描，不适合开发机的每次回归。
