using LeslieCleaner.Core.Abstractions;

namespace LeslieCleaner.Modules.AI;

/// <summary>AI Provider 配置服务；持久化数据只包含凭据引用。</summary>
public sealed class AiProviderConfigurationService : IAiProviderOptionsAccessor
{
    /// <summary>非敏感 Provider 配置键。</summary>
    public const string ConfigKey = "ai:provider";

    private readonly IConfigService _configService;
    private readonly IAiSecretStore _secretStore;

    /// <summary>创建配置服务。</summary>
    public AiProviderConfigurationService(IConfigService configService, IAiSecretStore secretStore)
    {
        _configService = configService;
        _secretStore = secretStore;
    }

    /// <summary>读取并规范化配置。</summary>
    public AiProviderOptions Get() => Normalize(_configService.GetValue(ConfigKey, AiProviderOptions.OfflineDefault));

    /// <summary>保存配置；Token 单独进入凭据存储，绝不进入 JSON 配置对象。</summary>
    public async Task<AiProviderOptions> SaveAsync(
        AiProviderOptions options,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        AiProviderOptions current = Get();
        string? reference = options.CredentialReference;
        if (!string.IsNullOrWhiteSpace(token))
        {
            reference = await _secretStore.StoreAsync(token, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(current.CredentialReference))
            {
                await _secretStore.DeleteAsync(current.CredentialReference, cancellationToken).ConfigureAwait(false);
            }
        }
        AiProviderOptions safe = Normalize(options with { CredentialReference = reference });
        await _configService.SetValueAsync(ConfigKey, safe, cancellationToken).ConfigureAwait(false);
        return safe;
    }

    private static AiProviderOptions Normalize(AiProviderOptions options)
    {
        string provider = string.IsNullOrWhiteSpace(options.ProviderId) ? "local-rules" : options.ProviderId.Trim();
        string model = PrivacyRedactor.SanitizeCategory(options.Model, provider == "local-rules" ? "leslie-rules-v1" : "default");
        TimeSpan timeout = options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(30)
            ? TimeSpan.FromSeconds(provider == "local-rules" ? 10 : 5)
            : options.Timeout;
        return options with
        {
            ProviderId = provider,
            Model = model,
            Temperature = Math.Clamp(options.Temperature, 0, 2),
            Timeout = timeout,
        };
    }
}
