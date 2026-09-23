using System.Text.Json;
using LeslieCleaner.PluginSdk;

namespace LeslieCleaner.SamplePlugin;

/// <summary>Sample plugin used to prove isolation, lifecycle, cancellation and least privilege.</summary>
public sealed class SamplePlugin : IPlugin
{
    private IPluginContext? _context;

    /// <inheritdoc />
    public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _context = context;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<PluginExecutionResponse> ExecuteAsync(
        PluginExecutionRequest request,
        CancellationToken cancellationToken)
    {
        return request.Operation switch
        {
            "echo" => new PluginExecutionResponse(true, request.Payload),
            "crash" => CrashHost(),
            "hang" => await HangAsync(cancellationToken).ConfigureAwait(false),
            "request-file-delete" => await RequestDeleteAsync(cancellationToken).ConfigureAwait(false),
            _ => new PluginExecutionResponse(false, Empty(), "UNKNOWN_OPERATION", "Sample operation is unknown."),
        };
    }

    /// <inheritdoc />
    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _context = null;
        return ValueTask.CompletedTask;
    }

    private static PluginExecutionResponse CrashHost()
    {
        Environment.Exit(1);
        return default;
    }

    private static async ValueTask<PluginExecutionResponse> HangAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        return new PluginExecutionResponse(true, Empty());
    }

    private async ValueTask<PluginExecutionResponse> RequestDeleteAsync(CancellationToken cancellationToken)
    {
        if (_context is null) throw new InvalidOperationException("Plugin was not initialized.");
        PluginCapabilityResponse response = await _context.RequestCapabilityAsync(
            new PluginCapabilityRequest("File.Delete", "Delete", "forbidden.txt"), cancellationToken).ConfigureAwait(false);
        return new PluginExecutionResponse(response.Granted, response.Payload, response.ErrorCode, response.Message);
    }

    private static JsonElement Empty() => JsonSerializer.SerializeToElement(new { });
}
