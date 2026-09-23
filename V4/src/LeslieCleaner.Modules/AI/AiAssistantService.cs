using System.Diagnostics;
using System.Globalization;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Modules.AI;

/// <summary>AI 请求总控：最小上下文、Prompt、Provider、降级、安全过滤、历史与审计闭环。</summary>
public sealed class AiAssistantService
{
    private readonly IReadOnlyDictionary<string, IAiProvider> _providers;
    private readonly LocalRuleKnowledgeProvider _localProvider;
    private readonly AiProviderConfigurationService _configuration;
    private readonly AiPromptBuilder _promptBuilder;
    private readonly AiSafetyFilter _safetyFilter;
    private readonly IAiConversationHistoryStore _historyStore;
    private readonly ILogService _logService;

    /// <summary>创建 AI 助手服务。</summary>
    public AiAssistantService(
        IEnumerable<IAiProvider> providers,
        LocalRuleKnowledgeProvider localProvider,
        AiProviderConfigurationService configuration,
        AiPromptBuilder promptBuilder,
        AiSafetyFilter safetyFilter,
        IAiConversationHistoryStore historyStore,
        ILogService logService)
    {
        _providers = providers.GroupBy(provider => provider.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _localProvider = localProvider;
        _configuration = configuration;
        _promptBuilder = promptBuilder;
        _safetyFilter = safetyFilter;
        _historyStore = historyStore;
        _logService = logService;
    }

    /// <summary>回答自然语言问题；远程失败、超时或无网络时自动安全降级到离线规则。</summary>
    public async Task<AiAssistantResponse> AskAsync(AiQuestion question, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(question.Text);
        long started = Stopwatch.GetTimestamp();
        string sanitizedQuestion = PrivacyRedactor.Redact(question.Text);
        AiAggregateContext context = question.Context.Normalize();
        string prompt = _promptBuilder.Build(sanitizedQuestion, context);
        AiProviderOptions options = _configuration.Get();
        IAiProvider preferred = _providers.GetValueOrDefault(options.ProviderId) ?? _localProvider;

        _logService.LogEvent(LogEventKind.UserAction, "AI request started", new Dictionary<string, object?>
        {
            ["RequestTimeUtc"] = DateTimeOffset.UtcNow,
            ["Provider"] = preferred.Id,
            ["Model"] = options.Model,
        });

        AiProviderResult generated = await preferred.GenerateAsync(
            new AiProviderRequest(prompt, options.Model, options.Temperature), cancellationToken).ConfigureAwait(false);
        bool fallback = false;
        if (!generated.Success && !preferred.IsLocal)
        {
            fallback = true;
            generated = await _localProvider.GenerateAsync(
                new AiProviderRequest(prompt, "leslie-rules-v1", 0), cancellationToken).ConfigureAwait(false);
        }
        if (!generated.Success || string.IsNullOrWhiteSpace(generated.Content))
        {
            throw new InvalidOperationException("No AI provider produced a valid response.");
        }

        (string answer, AiSafetyDecision safety) = _safetyFilter.Apply(sanitizedQuestion, generated.Content);
        answer = PrivacyRedactor.Redact(answer);
        TimeSpan total = Stopwatch.GetElapsedTime(started);
        string contextSummary = string.Create(CultureInfo.InvariantCulture,
            $"CPU {context.CpuUsagePercent:F1}%; Memory {context.MemoryUsagePercent:F1}%; Disk free {context.DiskFreePercent:F1}%; Startup {context.HighImpactStartupCount}; Scans/Cleanups {context.RecentScanCount}/{context.RecentCleanupCount}; Privacy risks {context.PrivacyRiskCount}");
        await _historyStore.AppendAsync(new AiConversationEntry(
            Guid.NewGuid(), DateTimeOffset.UtcNow, sanitizedQuestion, answer, contextSummary,
            generated.Provider, generated.Model), cancellationToken).ConfigureAwait(false);

        _logService.LogEvent(LogEventKind.PerformanceMetrics, "AI request completed", new Dictionary<string, object?>
        {
            ["Provider"] = generated.Provider,
            ["Model"] = generated.Model,
            ["ResponseTimeMs"] = total.TotalMilliseconds,
            ["UsedOfflineFallback"] = fallback,
            ["Error"] = fallback ? "PreferredProviderFailed" : null,
        });
        return new AiAssistantResponse(answer, generated.Provider, generated.Model, fallback, safety, total);
    }
}
