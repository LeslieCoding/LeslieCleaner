using System.Collections.ObjectModel;

namespace LeslieCleaner.Modules.AI;

/// <summary>AI 助手只允许使用的聚合上下文；该模型不包含文件名、文档内容、账号或消息。</summary>
public sealed record AiAggregateContext(
    string OperatingSystemFamily,
    double CpuUsagePercent,
    double MemoryUsagePercent,
    double DiskFreePercent,
    TimeSpan SystemUptime,
    int InstalledApplicationCount,
    int HighImpactStartupCount,
    int RecentScanCount,
    int RecentCleanupCount,
    long ReclaimableBytes,
    int PrivacyRiskCount)
{
    /// <summary>返回经过边界限制的安全副本，防止异常数据污染 Prompt。</summary>
    public AiAggregateContext Normalize() => this with
    {
        OperatingSystemFamily = PrivacyRedactor.SanitizeCategory(OperatingSystemFamily, "Windows"),
        CpuUsagePercent = Math.Clamp(CpuUsagePercent, 0, 100),
        MemoryUsagePercent = Math.Clamp(MemoryUsagePercent, 0, 100),
        DiskFreePercent = Math.Clamp(DiskFreePercent, 0, 100),
        SystemUptime = SystemUptime < TimeSpan.Zero ? TimeSpan.Zero : SystemUptime,
        InstalledApplicationCount = Math.Max(0, InstalledApplicationCount),
        HighImpactStartupCount = Math.Max(0, HighImpactStartupCount),
        RecentScanCount = Math.Max(0, RecentScanCount),
        RecentCleanupCount = Math.Max(0, RecentCleanupCount),
        ReclaimableBytes = Math.Max(0, ReclaimableBytes),
        PrivacyRiskCount = Math.Max(0, PrivacyRiskCount),
    };
}

/// <summary>发送给 AI 助手的自然语言问题。</summary>
public sealed record AiQuestion(string Text, AiAggregateContext Context);

/// <summary>模型无关的 Provider 请求。</summary>
public sealed record AiProviderRequest(string Prompt, string Model, double Temperature);

/// <summary>模型无关的 Provider 结果。</summary>
public sealed record AiProviderResult(
    bool Success,
    string Content,
    string Provider,
    string Model,
    TimeSpan Duration,
    string? ErrorCode = null);

/// <summary>AI Provider 的非敏感配置；只保存凭据引用，不保存 Token。</summary>
public sealed record AiProviderOptions(
    string ProviderId,
    string Model,
    Uri? Endpoint,
    double Temperature,
    TimeSpan Timeout,
    string? CredentialReference = null)
{
    /// <summary>默认使用完全离线的本地规则知识库。</summary>
    public static AiProviderOptions OfflineDefault { get; } =
        new("local-rules", "leslie-rules-v1", null, 0, TimeSpan.FromSeconds(10));
}

/// <summary>AI 安全过滤结论。</summary>
public sealed record AiSafetyDecision(
    bool IsAllowed,
    bool RequiresUserConfirmation,
    string RiskExplanation,
    string BackupSuggestion,
    string ConfirmationRequirement);

/// <summary>最终交付给 UI 的安全响应。</summary>
public sealed record AiAssistantResponse(
    string Answer,
    string Provider,
    string Model,
    bool UsedOfflineFallback,
    AiSafetyDecision Safety,
    TimeSpan Duration);

/// <summary>经过脱敏、可用于本机历史展示的会话条目。</summary>
public sealed record AiConversationEntry(
    Guid Id,
    DateTimeOffset TimestampUtc,
    string Question,
    string Answer,
    string ContextSummary,
    string Provider,
    string Model);

/// <summary>建议优先级。</summary>
public enum AiRecommendationPriority
{
    /// <summary>可选改进。</summary>
    Low,
    /// <summary>一般优化建议。</summary>
    Medium,
    /// <summary>建议尽快处理。</summary>
    High,
    /// <summary>需要立即关注。</summary>
    Critical,
}

/// <summary>结构化 AI 建议。</summary>
public sealed record AiRecommendation(
    string Category,
    string Problem,
    string Reason,
    string Suggestion,
    AiRecommendationPriority Priority,
    int RiskLevel);

/// <summary>AI Provider 抽象，用于隔离本地模型与 OpenAI-compatible API。</summary>
public interface IAiProvider
{
    /// <summary>稳定 Provider 标识。</summary>
    string Id { get; }
    /// <summary>是否完全在本机处理。</summary>
    bool IsLocal { get; }
    /// <summary>生成回答；失败以结果返回，取消仍抛出取消异常。</summary>
    Task<AiProviderResult> GenerateAsync(AiProviderRequest request, CancellationToken cancellationToken = default);
}

/// <summary>向 Provider 提供经过规范化的非敏感配置。</summary>
public interface IAiProviderOptionsAccessor
{
    /// <summary>读取当前 Provider 配置。</summary>
    AiProviderOptions Get();
}
/// <summary>敏感凭据存储抽象；Provider 配置只持有引用。</summary>
public interface IAiSecretStore
{
    /// <summary>保存凭据并返回不含秘密的引用。</summary>
    ValueTask<string> StoreAsync(string secret, CancellationToken cancellationToken = default);
    /// <summary>按引用读取凭据。</summary>
    ValueTask<string?> RetrieveAsync(string reference, CancellationToken cancellationToken = default);
    /// <summary>删除凭据。</summary>
    ValueTask DeleteAsync(string reference, CancellationToken cancellationToken = default);
}

/// <summary>AI 历史存储抽象；调用者必须仅传入脱敏条目。</summary>
public interface IAiConversationHistoryStore
{
    /// <summary>追加脱敏历史。</summary>
    Task AppendAsync(AiConversationEntry entry, CancellationToken cancellationToken = default);
    /// <summary>读取最近的脱敏历史。</summary>
    Task<IReadOnlyList<AiConversationEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    /// <summary>清空历史。</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>仅进程内保存秘密的默认实现；不会把 Token 写入配置或磁盘。</summary>
public sealed class ProcessAiSecretStore : IAiSecretStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <inheritdoc />
    public ValueTask<string> StoreAsync(string secret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        cancellationToken.ThrowIfCancellationRequested();
        string reference = $"process:{Guid.NewGuid():N}";
        lock (_gate) { _secrets[reference] = secret; }
        return ValueTask.FromResult(reference);
    }

    /// <inheritdoc />
    public ValueTask<string?> RetrieveAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) { return ValueTask.FromResult(_secrets.GetValueOrDefault(reference)); }
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) { _secrets.Remove(reference); }
        return ValueTask.CompletedTask;
    }
}

/// <summary>线程安全的本机内存历史；只接收已经脱敏的条目。</summary>
public sealed class InMemoryAiConversationHistoryStore : IAiConversationHistoryStore
{
    private const int Capacity = 200;
    private readonly LinkedList<AiConversationEntry> _entries = [];
    private readonly object _gate = new();

    /// <inheritdoc />
    public Task AppendAsync(AiConversationEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _entries.AddFirst(entry);
            while (_entries.Count > Capacity) { _entries.RemoveLast(); }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AiConversationEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        count = Math.Clamp(count, 0, Capacity);
        lock (_gate)
        {
            IReadOnlyList<AiConversationEntry> result = new ReadOnlyCollection<AiConversationEntry>([.. _entries.Take(count)]);
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) { _entries.Clear(); }
        return Task.CompletedTask;
    }
}
