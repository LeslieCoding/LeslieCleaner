using System.Net;
using System.Text;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Modules.AI;
using LeslieCleaner.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace LeslieCleaner.Tests.AI;

public sealed class AiAssistantTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;

    public AiAssistantTests()
    {
        _config = new JsonConfigService(Path.Combine(_workspace.PathService.ConfigDirectory, "ai-settings.json"), _workspace.Log);
    }

    public void Dispose()
    {
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public void PromptBuilder_HasStableStructure_AndRedactsPersonalContent()
    {
        var context = Context();
        string prompt = new AiPromptBuilder().Build(
            @"为什么慢？ password=abc123 user@example.com C:\Users\Leslie\Documents\x.txt", context);

        Assert.Contains("[ROLE]", prompt);
        Assert.Contains("[CONTEXT]", prompt);
        Assert.Contains("[TASK]", prompt);
        Assert.Contains("[RESTRICTIONS]", prompt);
        Assert.Contains("[OUTPUT]", prompt);
        Assert.DoesNotContain("abc123", prompt);
        Assert.DoesNotContain("user@example.com", prompt);
        Assert.DoesNotContain(@"C:\Users\Leslie", prompt);
        Assert.Contains("[REDACTED]", prompt);
    }

    [Fact]
    public void ContextCollector_UsesOnlyAggregates_AndNormalizesBounds()
    {
        var metrics = new SystemMetrics(100, -2, 1, 120, -3, 0);
        AiAggregateContext context = new AiContextCollector().Collect(metrics, -1, 150, TimeSpan.FromHours(-2), -5, -3, -1);

        Assert.Equal(0, context.CpuUsagePercent);
        Assert.Equal(100, context.MemoryUsagePercent);
        Assert.Equal(100, context.DiskFreePercent);
        Assert.Equal(0, context.InstalledApplicationCount);
        string[] fields = typeof(AiAggregateContext).GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain(fields, name => name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, name => name.Contains("Document", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, name => name.Contains("Message", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, name => name.Contains("FileName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SafetyFilter_BlocksDangerousExecution_AndRequiresBackupConfirmation()
    {
        var filter = new AiSafetyFilter();
        (string answer, AiSafetyDecision decision) = filter.Apply(
            "帮我修改注册表并关闭杀毒软件",
            "运行 reg delete 后 disable defender");

        Assert.False(decision.IsAllowed);
        Assert.True(decision.RequiresUserConfirmation);
        Assert.Contains("不能提供", answer);
        Assert.Contains("备份", answer);
        Assert.Contains("确认", answer);
        Assert.DoesNotContain("reg delete", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("disable defender", answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalProvider_AnswersNaturalLanguageOffline_FromRealAggregateValues()
    {
        string prompt = new AiPromptBuilder().Build("为什么电脑越来越慢？", Context() with
        {
            CpuUsagePercent = 91,
            DiskFreePercent = 8,
            HighImpactStartupCount = 4,
        });
        AiProviderResult result = await new LocalRuleKnowledgeProvider().GenerateAsync(
            new AiProviderRequest(prompt, "leslie-rules-v1", 0));

        Assert.True(result.Success);
        Assert.Equal("local-rules", result.Provider);
        Assert.Contains("91.0%", result.Content);
        Assert.Contains("8.0%", result.Content);
        Assert.Contains("4 个高影响启动项", result.Content);
        Assert.Contains("建议", result.Content);
    }

    [Fact]
    public async Task OpenAiProvider_ParsesCompatibleResponse_AndKeepsTokenOutOfBody()
    {
        string? body = null;
        string? authorization = null;
        var handler = new DelegateHandler(async (request, cancellationToken) =>
        {
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            authorization = request.Headers.Authorization?.ToString();
            return JsonResponse("{\"choices\":[{\"message\":{\"content\":\"安全建议\"}}]}");
        });
        var secrets = new ProcessAiSecretStore();
        string reference = await secrets.StoreAsync("top-secret-token");
        AiProviderOptions options = RemoteOptions(reference);
        var provider = new OpenAiCompatibleProvider(new HttpClient(handler), secrets, _workspace.Log, () => options);

        AiProviderResult result = await provider.GenerateAsync(new AiProviderRequest("safe prompt", "test-model", 0.2));

        Assert.True(result.Success);
        Assert.Equal("安全建议", result.Content);
        Assert.Equal("Bearer top-secret-token", authorization);
        Assert.DoesNotContain("top-secret-token", body);
    }

    [Fact]
    public async Task OpenAiProvider_Timeout_ReturnsFailureWithoutHanging()
    {
        var handler = new DelegateHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });
        var provider = new OpenAiCompatibleProvider(
            new HttpClient(handler), new ProcessAiSecretStore(), _workspace.Log,
            () => RemoteOptions() with { Timeout = TimeSpan.FromMilliseconds(50) });

        AiProviderResult result = await provider.GenerateAsync(new AiProviderRequest("prompt", "model", 0));

        Assert.False(result.Success);
        Assert.Equal("Timeout", result.ErrorCode);
        Assert.True(result.Duration < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task OpenAiProvider_InvalidPayload_ReturnsInvalidResponse()
    {
        var provider = new OpenAiCompatibleProvider(
            new HttpClient(new DelegateHandler((_, _) => Task.FromResult(JsonResponse("{\"choices\":[]}")))),
            new ProcessAiSecretStore(), _workspace.Log, () => RemoteOptions());

        AiProviderResult result = await provider.GenerateAsync(new AiProviderRequest("prompt", "model", 0));

        Assert.False(result.Success);
        Assert.Equal("InvalidResponse", result.ErrorCode);
    }

    [Fact]
    public async Task Assistant_RemoteNetworkFailure_FallsBackOffline_AndStoresOnlyRedactedHistory()
    {
        var secrets = new ProcessAiSecretStore();
        var configuration = new AiProviderConfigurationService(_config, secrets);
        await configuration.SaveAsync(RemoteOptions(), "secret-value-123");
        var remote = new OpenAiCompatibleProvider(
            new HttpClient(new DelegateHandler((_, _) => throw new HttpRequestException("offline"))),
            secrets, _workspace.Log, configuration.Get);
        var local = new LocalRuleKnowledgeProvider();
        var history = new InMemoryAiConversationHistoryStore();
        var assistant = new AiAssistantService(
            [remote, local], local, configuration, new AiPromptBuilder(), new AiSafetyFilter(), history, _workspace.Log);

        AiAssistantResponse response = await assistant.AskAsync(new AiQuestion(
            "为什么慢？ password=do-not-store private message: hello", Context()));
        IReadOnlyList<AiConversationEntry> entries = await history.GetRecentAsync(10);

        Assert.True(response.UsedOfflineFallback);
        Assert.Equal("local-rules", response.Provider);
        Assert.Single(entries);
        Assert.DoesNotContain("do-not-store", entries[0].Question);
        Assert.DoesNotContain("hello", entries[0].Question);
        Assert.DoesNotContain("do-not-store", entries[0].Answer);
        Assert.Contains("[REDACTED]", entries[0].Question);
        Assert.True(_workspace.Log.HasEvent(LeslieCleaner.Core.Models.LogEventKind.PerformanceMetrics));
    }

    [Fact]
    public async Task Configuration_NeverPersistsPlaintextToken()
    {
        var configuration = new AiProviderConfigurationService(_config, new ProcessAiSecretStore());
        AiProviderOptions saved = await configuration.SaveAsync(RemoteOptions(), "never-write-this-token");
        string settings = await File.ReadAllTextAsync(Path.Combine(_workspace.PathService.ConfigDirectory, "ai-settings.json"));

        Assert.NotNull(saved.CredentialReference);
        Assert.DoesNotContain("never-write-this-token", settings);
        Assert.Contains("process:", settings);
    }

    [Fact]
    public void DependencyInjection_ResolvesCompleteAssistantGraph()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfigService>(_config);
        services.AddSingleton<ILogService>(_workspace.Log);
        services.AddLeslieCleanerAiModule();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<AiAssistantService>());
        Assert.Equal(2, provider.GetServices<IAiProvider>().Count());
    }

    [Fact]
    public void RecommendationEngine_CoversCriticalStorageStartupAndPrivacy()
    {
        IReadOnlyList<AiRecommendation> recommendations = new AiRecommendationEngine().Analyze(
            new SystemMetrics(2L * 1024 * 1024 * 1024, 3, 0, 5, 7, 0));

        Assert.Contains(recommendations, item => item.Category == "Storage" && item.Priority == AiRecommendationPriority.Critical);
        Assert.Contains(recommendations, item => item.Category == "Startup");
        Assert.Contains(recommendations, item => item.Category == "Privacy");
        Assert.All(recommendations, item => Assert.InRange(item.RiskLevel, 0, 5));
    }

    private static AiAggregateContext Context() => new(
        "Windows", 20, 45, 60, TimeSpan.FromHours(8), 80, 0, 3, 2, 0, 0);

    private static AiProviderOptions RemoteOptions(string? reference = null) => new(
        "openai-compatible", "test-model", new Uri("https://ai.example.test/v1"), 0.2, TimeSpan.FromSeconds(2), reference);

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
