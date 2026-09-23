using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;

namespace LeslieCleaner.Modules.AI;

/// <summary>OpenAI-compatible Chat Completions Provider，支持远程 HTTPS 与本机回环 API。</summary>
public sealed class OpenAiCompatibleProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly IAiSecretStore _secretStore;
    private readonly ILogService _logService;
    private readonly Func<AiProviderOptions> _optionsAccessor;

    /// <summary>创建 Provider；HttpClient 和配置访问器均可替换以进行离线测试。</summary>
    public OpenAiCompatibleProvider(
        HttpClient httpClient,
        IAiSecretStore secretStore,
        ILogService logService,
        Func<AiProviderOptions> optionsAccessor)
    {
        _httpClient = httpClient;
        _secretStore = secretStore;
        _logService = logService;
        _optionsAccessor = optionsAccessor;
    }

    /// <summary>使用可注入的配置访问器创建 Provider。</summary>
    public OpenAiCompatibleProvider(
        HttpClient httpClient,
        IAiSecretStore secretStore,
        ILogService logService,
        IAiProviderOptionsAccessor optionsAccessor)
        : this(httpClient, secretStore, logService, optionsAccessor.Get)
    {
    }
    /// <inheritdoc />
    public string Id => "openai-compatible";

    /// <inheritdoc />
    public bool IsLocal => false;

    /// <inheritdoc />
    public async Task<AiProviderResult> GenerateAsync(AiProviderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        long started = Stopwatch.GetTimestamp();
        AiProviderOptions options = _optionsAccessor();
        try
        {
            Uri endpoint = ResolveEndpoint(options.Endpoint);
            string? token = string.IsNullOrWhiteSpace(options.CredentialReference)
                ? null
                : await _secretStore.RetrieveAsync(options.CredentialReference, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(options.CredentialReference) && string.IsNullOrWhiteSpace(token))
            {
                return Failure("CredentialUnavailable", request.Model, started);
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
            if (!string.IsNullOrWhiteSpace(token))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            message.Content = JsonContent.Create(new
            {
                model = request.Model,
                temperature = request.Temperature,
                messages = new[] { new { role = "user", content = request.Prompt } },
            });

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Timeout);
            using HttpResponseMessage response = await _httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return Failure("AuthenticationFailed", request.Model, started);
            }
            if (!response.IsSuccessStatusCode)
            {
                return Failure($"Http{(int)response.StatusCode}", request.Model, started);
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            if (!TryReadContent(document.RootElement, out string? content) || string.IsNullOrWhiteSpace(content) || content.Length > 32_000)
            {
                return Failure("InvalidResponse", request.Model, started);
            }
            return new AiProviderResult(true, content, Id, request.Model, Stopwatch.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("Timeout", request.Model, started);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or UriFormatException)
        {
            _logService.LogException(ex, "AI.OpenAiCompatibleProvider", new Dictionary<string, object?>
            {
                ["Provider"] = Id,
                ["Model"] = request.Model,
                ["DurationMs"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            });
            return Failure(ex is JsonException ? "InvalidResponse" : "NetworkError", request.Model, started);
        }
    }

    private static Uri ResolveEndpoint(Uri? configured)
    {
        if (configured is null || !configured.IsAbsoluteUri || !string.IsNullOrEmpty(configured.UserInfo) || !string.IsNullOrEmpty(configured.Query))
        {
            throw new InvalidOperationException("AI endpoint must be an absolute URI without credentials or query parameters.");
        }
        bool loopbackHttp = configured.Scheme == Uri.UriSchemeHttp && configured.IsLoopback;
        if (configured.Scheme != Uri.UriSchemeHttps && !loopbackHttp)
        {
            throw new InvalidOperationException("AI endpoint must use HTTPS, except for a loopback local model endpoint.");
        }
        string path = configured.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            path += "/chat/completions";
        }
        var builder = new UriBuilder(configured) { Path = path, Query = string.Empty, Fragment = string.Empty };
        return builder.Uri;
    }

    private static bool TryReadContent(JsonElement root, out string? content)
    {
        content = null;
        return root.TryGetProperty("choices", out JsonElement choices) &&
            choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out JsonElement message) &&
            message.TryGetProperty("content", out JsonElement contentNode) &&
            (content = contentNode.GetString()) is not null;
    }

    private AiProviderResult Failure(string errorCode, string model, long started)
    {
        TimeSpan duration = Stopwatch.GetElapsedTime(started);
        _logService.LogEvent(LeslieCleaner.Core.Models.LogEventKind.UserAction, "AI provider request failed", new Dictionary<string, object?>
        {
            ["Provider"] = Id,
            ["Model"] = model,
            ["DurationMs"] = duration.TotalMilliseconds,
            ["Error"] = errorCode,
        });
        return new AiProviderResult(false, string.Empty, Id, model, duration, errorCode);
    }
}
