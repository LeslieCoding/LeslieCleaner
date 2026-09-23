# V4 卸载资源所有权清单补充报告

日期：2026-08-17

在 `V4_UNINSTALL_WORKFLOW_REPORT.md` 的安全工作流基础上，本轮新增只读 Windows 平台清单：

- Windows 服务：读取 `HKLM\SYSTEM\CurrentControlSet\Services` 的 `ImagePath`，仅当规范化后的可执行文件位于应用安装目录边界内时建立 Service 指纹；不使用服务名模糊匹配。
- 启动项：读取 HKCU、HKLM 64 位和 32 位 Run 位置，仅当命令可执行文件位于安装目录边界内时建立 Startup 指纹。
- 计划任务：只读解析 `%WINDIR%\System32\Tasks` XML 的 Command；递归时跳过重解析目录；仅对安装目录内的动作建立 Task 指纹。
- 命令路径支持引号、参数和环境变量展开；相对可执行文件、同前缀相邻目录（如 `Vendor-Evil`）均拒绝。
- 清单自动接入 UninstallWorkflow；调用方显式提供证据时仍可覆盖平台收集结果。

验证：`v4-uninstaller-safety-inventory-focused.trx`，10/10 通过；编译链 0 warning / 0 error。

安全边界：本功能只建立所有权证据，不会停止/删除服务、启动项或计划任务。后续删除仍必须满足“应用已卸载、可执行文件缺失、指纹匹配、非系统资源、备份和确认”全部条件。
