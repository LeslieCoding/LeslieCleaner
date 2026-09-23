# LeslieCleaner Android Adaptation - CleanMyPCNext_Development_Bible_INDEX

> Android Prompt ID: AP-LEGACY-CLEANMYPCNEXT-DEVELOPMENT-BIBLE-INDEX
> Source Prompt: `CleanMyPCNext_Development_Bible_INDEX.md`
> Target platform: Android 10+ (API 29+)
> Status: generated Android adaptation; source content is retained below for traceability.

## Android execution boundary

Translate desktop-only requirements into public Android APIs and explicit user actions. Do not emulate Windows registry, services, drivers, unrestricted filesystem access, silent deletion, root access, hidden APIs, accessibility abuse, or undocumented OEM behavior. Use Kotlin, Jetpack Compose, typed capability results, scoped storage, SAF, MediaStore, PackageManager, WorkManager, app-private storage, read-back verification, redacted JSONL audit logs, and reversible operations where mutation is permitted.

Every implementation must follow: Read -> Analyze -> Plan -> Implement -> Build -> Test -> Debug -> Retest -> Regression -> Document -> Continue. Each change must record requirement IDs, assumptions, risk, rollback, commands, evidence, and acceptance status in `Android_App/AI_SPEC`.

## Source content

# CleanMyPC Next Development Bible

代号：CleanMyPC Next  
版本：1.0（规范补齐批次，2026-09-01）  
性质：Single Source of Truth

## 产品定位

CleanMyPC Next 不是 CleanMyPC 的仿制品，而是面向 Windows 10/11 的可回滚系统维护平台。核心差异是：规则优先的快速定位、多阶段残留证据、受控深度扫描、明确权限边界、可验证备份恢复和 AI 解释层。

## 规范层级

1. `00_Execution_Contract.md` 定义所有 AI/开发任务的统一执行、证据和交付契约。
2. `Main/00–27` 定义系统、架构、安全、数据库、模块和发布的强制约束。
3. `AI/50–57` 定义 AI 开发、代码审查、测试、安全、日志、基准、工作流和 Prompt 执行。
4. `Modules/20–37` 定义具体业务模块的扫描、执行、数据、权限和验收。
5. `UI/10–18` 定义设计令牌、组件、布局、动效、可访问性、交互和 UI 实现契约。
6. `UX/40–49` 定义安装、批量、历史、通知、搜索、收藏、菜单、撤销、AI 建议和 UX 实现契约。
7. `Prompt_Library/` 提供可直接交给开发代理执行的系统级和模块级详细提示词。

## 强制开发闭环

需求 ID → 数据/权限边界 → 扫描或检索策略 → 用户确认 → 备份/隔离 → 执行 → 回读验证 → 日志/历史 → 自动化测试 → Release 冒烟。

任何模块都不得以“页面存在”“代码能编译”或“调用未抛异常”作为完成标准。未有测试证据的需求状态为 `PendingEvidence`；未实现或空规范不得标记为完成。

## 当前目录清单

| 区域 | 文件数 | 状态 |
|---|---:|---|
| 顶层 prompt | 4 | 已有内容，待逐项映射 |
| Main | 29 | 执行契约 + 系统规范 |
| AI | 8 | AI 规则与 Prompt 执行契约 |
| Modules | 18 | 模块规范与统一实现契约 |
| UI | 9 | 设计规范与 UI 实现契约 |
| UX | 10 | 交互规范与 UX 实现契约 |
| Prompt Library | 9 | 全局、Residual、Registry、Uninstaller、Cleaner、Cache、Startup、Privacy/Browser、AI 专用提示词 |
| 规范文件合计 | 87 | 文件级规范完成；功能实现仍按验收矩阵推进 |
| 本索引 | 1 | 总入口，不计入上面的规范文件数量 |

当前 88 个 Markdown/TXT 文件全部非空。1200–2000 页目标作为后续模块细化、规则样例、API 契约和验收证据扩展的持续开发基线；总索引中的规模数字必须以每次审计实测为准，不将当前文档量误报为最终规模。

## 产品重点

- Residual Engine：安装数据库、日志、卸载字符串、发布者、ProductCode、GUID、签名规则、定向检查、最后才深度扫描。
- Rule Engine：垃圾、浏览器缓存、AI 软件缓存和已知残留优先直接定位，不做默认全盘递归。
- Registry Engine：物理 32/64 位视图、证据和风险评分、备份、权限、回读和失败恢复。
- AI Layer：最小聚合上下文、隐私脱敏、解释和建议；AI 永远不直接执行破坏性动作。
- Platform Safety：默认拒绝、显式 UAC、完整性基线、审计哈希链和可恢复操作。

## 状态定义

- `Implemented`：源码、聚焦测试和必要集成证据齐全。
- `Partial`：核心路径存在，但仍有明确缺口或未覆盖矩阵。
- `PendingEvidence`：已有实现，但缺少对应的可复现验收证据。
- `NotImplemented`：没有可执行实现，或需求仍为空/未定义。

后续所有开发、重构和验收均以本目录及其文件为准。
