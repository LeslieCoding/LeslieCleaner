# V4 AI Assistant P1 闭环实现与验证报告

日期：2026-08-16  
需求基线：`23_AI_Assistant_Architecture.md`  
实现范围：`LeslieCleaner.Modules.AI`、AI 测试与本报告；未修改 Data、Security 或其他业务模块。

## 1. 交付结论

AI Assistant 已从原有“健康评分 + 维护计划”扩展为可运行的完整建议闭环：

`聚合上下文 -> 隐私脱敏 -> 五段式 Prompt -> Provider -> 安全过滤 -> 自然语言响应 -> 脱敏历史 -> 结构化审计`

AI 不注入任何清理、注册表、启动项或安全设置执行服务，只能分析、解释和建议。涉及文件、注册表、启动项、强制清理或安全状态的回答会强制给出风险、备份和用户确认要求；模型输出若包含直接危险命令，会被替换为拒绝执行的安全响应。

## 2. 架构实现

### Context

- `AiAggregateContext` 只包含 CPU、内存、磁盘、运行时间、应用数量、启动项数量、扫描/清理次数、可回收字节和隐私风险数量。
- `AiContextCollector` 从现有真实 `SystemMetrics` 和运行时数值构建上下文，不读取文件名、文档、浏览器数据库、账号或消息。
- 所有百分比与计数经过边界规范化，分类字符串经过短值净化。

### Knowledge / Recommendation

- `LocalRuleKnowledgeProvider` 是真实可执行的离线规则知识 Provider，无网络、无模型文件也能回答“为什么电脑变慢”等自然语言问题。
- 规则使用 Prompt 中的真实聚合指标，覆盖高 CPU、高内存、低磁盘、高影响启动项、可清理容量和隐私风险。
- `AiRecommendationEngine` 输出 Storage、Startup、Privacy、Performance 分类及 Critical/High/Medium/Low 优先级、原因、建议和风险级别。
- 保留并兼容原 `HealthScoreEngine`、`MaintenancePlanner` 与原有 8 项 AI Optimizer 测试。

### Provider

- `IAiProvider` 隔离模型差异。
- 本地优先默认配置为 `local-rules / leslie-rules-v1`。
- `OpenAiCompatibleProvider` 支持 `/chat/completions`、注入 `HttpClient`、模型/温度、Bearer 凭据、响应流解析、取消和配置超时。
- 远程只允许 HTTPS；本机模型允许 HTTP loopback；拒绝 URI 中的用户信息和查询参数，避免凭据进入 URL/日志。
- 远程网络错误、超时、鉴权错误、HTTP 错误或无效响应均返回明确错误码，`AiAssistantService` 自动降级到离线规则 Provider。

### Prompt / Safety

- `AiPromptBuilder` 固定生成 `[ROLE] / [CONTEXT] / [TASK] / [RESTRICTIONS] / [OUTPUT]` 五段结构。
- Restrictions 明确禁止直接删除、修改注册表、禁用安全软件、改变系统保护，以及索取密码、浏览器密码库、文档内容、私信、文件名、用户名或账号标识。
- `AiSafetyFilter` 检查中英文危险意图与模型输出，危险输出不向用户透传。
- 高风险建议必须附加风险说明、备份建议和“由用户查看明细并明确确认”。

### Privacy / History / Token

- `PrivacyRedactor` 在 Prompt 前、模型响应后和历史写入前统一脱敏密码/Token/API Key/Bearer、电子邮件、Windows 用户路径、浏览器密码、文档内容和私信标记。
- `InMemoryAiConversationHistoryStore` 只保存脱敏问题、脱敏回答和数值汇总，容量固定为最近 200 条，不持久化个人内容。
- `AiProviderOptions` 只保存 `CredentialReference`，不含 Token 字段。
- `AiProviderConfigurationService.SaveAsync` 将 Token 交给 `IAiSecretStore`，JSON 配置中只写凭据引用。
- 默认 `ProcessAiSecretStore` 不落盘，进程结束即失效；接口允许后续接入 Windows Credential Manager 而无需改变 Provider。

### Logging / DI

- 开始日志记录请求时间、Provider、Model。
- 完成日志记录 Provider、Model、响应耗时、是否发生离线降级和错误状态。
- Provider 错误记录错误码、模型和耗时；捕获异常进入统一 `ILogService.LogException`。
- `AddLeslieCleanerAiModule` 注册上下文、Prompt、安全、建议、配置、秘密、历史、本地与远程 Provider、总控服务，并继续注册原 AI 模块/维护计划。

## 3. 自动化验证

### Release 编译

- `LeslieCleaner.Modules.AI.csproj`：成功，0 warning，0 error。
- `LeslieCleaner.Tests.csproj`（复用共享工作区中已编译的其他项目引用）：成功，0 warning，0 error。
- 首次尝试完整依赖构建时，另一轮并行长期测试占用 `LeslieCleaner.Core.dll` 中间产物，出现 1 个文件锁错误；随后采用 `BuildProjectReferences=false` 仅验证本模块与测试项目，未把文件锁记录为代码失败。

### 聚焦测试

最终结果：19 passed，0 failed，0 skipped，228 ms。

覆盖项目：

1. Prompt 五段结构稳定性及密码、邮箱、用户路径脱敏。
2. Context 只含聚合字段及异常数值边界规范化。
3. 危险删除/注册表/关闭安全软件输出拦截，备份与确认强制附加。
4. 本地 Provider 完全离线自然语言回答，并引用真实汇总数值。
5. OpenAI-compatible 响应解析、Authorization Header 和 Token 不进入请求正文。
6. 超时在限定时间内返回 `Timeout`，无挂起。
7. 无效响应返回 `InvalidResponse`。
8. 网络失败自动降级本地，并验证历史不保存密码或私信内容。
9. Provider 配置文件不出现明文 Token，只出现凭据引用。
10. 完整 DI 图可解析，两个 Provider 均可发现。
11. 结构化建议覆盖 Critical Storage、Startup、Privacy。
12. 原有健康评分、建议与维护计划 8 项回归测试继续通过。

测试证据：`V4/artifacts/test-results/v4-ai-assistant-focused-rerun.trx`

首轮测试为 17/19，两项失败分别为多 Provider 的 DI 描述符登记方式，以及测试将产品名 `LeslieCleaner` 误判为用户名；修正后独立重跑达到 19/19。首轮失败证据保留为 `v4-ai-assistant-focused.trx`，未删除或覆盖。

## 4. 已知边界

- 本模块默认秘密存储是“不落盘”的进程级实现，因此重启后需要重新录入远程 Token；这是安全降级行为，不会回退到明文配置。生产环境若需要持久凭据，应注入 Windows Credential Manager 的 `IAiSecretStore` 实现。
- 本地 Provider 是确定性规则知识引擎，不伪装为大语言模型；Qwen/DeepSeek/Llama/Ollama 可通过 OpenAI-compatible loopback 接口接入。
- 本报告仅证明 AI 模块及其聚焦测试。全解决方案双轮回归、UI 自动化和安装包验证由 V4 总体验收阶段统一执行。
