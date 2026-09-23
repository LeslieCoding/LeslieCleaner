using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.PluginSdk;

namespace LeslieCleaner.Infrastructure.Plugins;

/// <summary>
/// Main-process proxy for isolated plugins. This assembly has no reflection-based plugin loader; it only launches
/// the dedicated host and exchanges authenticated, versioned JSON frames over a local named pipe.
/// </summary>
public sealed class PluginRuntime : IPluginRuntime, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPluginManager _pluginManager;
    private readonly IPluginStateStore _stateStore;
    private readonly ILogService _log;
    private readonly PluginRuntimeLimits _limits;
    private readonly string _hostPath;
    private readonly ConcurrentDictionary<string, HostSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PluginRuntimeTelemetry> _lastTelemetry = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _crashCounts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the runtime using the deployed host executable and production limits.</summary>
    public PluginRuntime(IPluginManager pluginManager, IPluginStateStore stateStore, ILogService log)
        : this(pluginManager, stateStore, log, ResolveDefaultHostPath(), PluginRuntimeLimits.Default)
    {
    }

    /// <summary>Creates a runtime with explicit host location and budgets, primarily for deterministic testing.</summary>
    public PluginRuntime(
        IPluginManager pluginManager,
        IPluginStateStore stateStore,
        ILogService log,
        string hostPath,
        PluginRuntimeLimits limits)
    {
        _pluginManager = pluginManager;
        _stateStore = stateStore;
        _log = log;
        _hostPath = Path.GetFullPath(hostPath);
        _limits = limits;
    }

    /// <inheritdoc />
    public async Task<PluginRuntimeResult> StartAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ValidatePluginId(pluginId);
        if (_sessions.TryGetValue(pluginId, out HostSession? current) && current.IsAlive)
            return Success(pluginId, null, current.SampleTelemetry());
        if (current is not null) await RemoveSessionAsync(pluginId, current, "previous-host-exited").ConfigureAwait(false);

        PluginDescriptor? descriptor = (await _pluginManager.DiscoverAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item => string.Equals(item.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (descriptor is null) return Error(pluginId, "PLUGIN_NOT_FOUND", "The plugin is not installed.");
        if (descriptor.TrustStatus != PluginTrustStatus.Ready)
            return Error(pluginId, "PLUGIN_NOT_ENABLED", "Only a validated and enabled plugin can execute.");
        PluginStateRecord? state = (await _stateStore.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item => string.Equals(item.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
        if (state is null || !state.Enabled) return Error(pluginId, "PLUGIN_NOT_ENABLED", "The plugin is disabled.");
        if (!File.Exists(_hostPath)) return Error(pluginId, "HOST_NOT_FOUND", $"Plugin host was not found at '{_hostPath}'.");

        string[] granted = (state.ApprovedPermissions ?? [])
            .Intersect(descriptor.Permissions ?? [], StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string authToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string permissionSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string pipeName = $"LeslieCleaner.Plugin.{Environment.ProcessId}.{Guid.NewGuid():N}";
        HostSession session;
        try
        {
            session = await HostSession.StartAsync(
                pluginId,
                descriptor.AssemblyPath,
                descriptor.ApiVersion,
                granted,
                _hostPath,
                pipeName,
                authToken,
                permissionSecret,
                _limits,
                () => _crashCounts.GetValueOrDefault(pluginId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error(pluginId, "START_TIMEOUT", "The isolated plugin host did not initialize before the deadline.");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            _log.LogException(exception, "PluginRuntime.Start", new Dictionary<string, object?> { ["PluginId"] = pluginId });
            return Error(pluginId, "HOST_START_FAILED", exception.Message);
        }

        if (!_sessions.TryAdd(pluginId, session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
            return await StartAsync(pluginId, cancellationToken).ConfigureAwait(false);
        }

        PluginRuntimeResult initialized = await session.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (!initialized.Success)
        {
            await RemoveSessionAsync(pluginId, session, initialized.ErrorCode ?? "initialization-failed").ConfigureAwait(false);
            return initialized;
        }

        _lastTelemetry[pluginId] = session.SampleTelemetry();
        _log.LogEvent(LogEventKind.UserAction, "Plugin isolated host started.", new Dictionary<string, object?>
        {
            ["PluginId"] = pluginId,
            ["ProcessId"] = session.ProcessId,
            ["GrantedPermissionCount"] = granted.Length,
        });
        return initialized;
    }

    /// <inheritdoc />
    public async Task<PluginRuntimeResult> ExecuteAsync(
        string pluginId,
        PluginRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Operation);
        PluginRuntimeResult started = await StartAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (!started.Success) return started;
        HostSession session = _sessions[pluginId];
        TimeSpan timeout = request.Timeout ?? _limits.DefaultExecutionTimeout;
        PluginRuntimeResult result = await session.ExecuteAsync(request, timeout, cancellationToken).ConfigureAwait(false);
        PluginRuntimeTelemetry telemetry = session.SampleTelemetry();
        _lastTelemetry[pluginId] = telemetry;
        if (!result.Success && result.ErrorCode is "EXECUTION_TIMEOUT" or "HOST_CRASHED" or "RESOURCE_LIMIT_EXCEEDED")
            await RemoveSessionAsync(pluginId, session, result.ErrorCode).ConfigureAwait(false);
        return result with { Telemetry = telemetry };
    }

    /// <inheritdoc />
    public async Task<PluginRuntimeResult> StopAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ValidatePluginId(pluginId);
        if (!_sessions.TryGetValue(pluginId, out HostSession? session))
            return Success(pluginId, null, _lastTelemetry.GetValueOrDefault(pluginId));
        PluginRuntimeResult result = await session.StopAsync(cancellationToken).ConfigureAwait(false);
        await RemoveSessionAsync(pluginId, session, result.Success ? "stopped" : result.ErrorCode ?? "stop-failed").ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public Task<PluginRuntimeTelemetry?> GetTelemetryAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePluginId(pluginId);
        if (_sessions.TryGetValue(pluginId, out HostSession? session))
        {
            PluginRuntimeTelemetry telemetry = session.SampleTelemetry();
            _lastTelemetry[pluginId] = telemetry;
            return Task.FromResult<PluginRuntimeTelemetry?>(telemetry);
        }
        return Task.FromResult<PluginRuntimeTelemetry?>(_lastTelemetry.GetValueOrDefault(pluginId));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        HostSession[] sessions = _sessions.Values.ToArray();
        _sessions.Clear();
        foreach (HostSession session in sessions) await session.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RemoveSessionAsync(string pluginId, HostSession session, string reason)
    {
        _sessions.TryRemove(new KeyValuePair<string, HostSession>(pluginId, session));
        if (reason is not "stopped" and not "previous-host-exited") _crashCounts.AddOrUpdate(pluginId, 1, static (_, count) => count + 1);
        PluginRuntimeTelemetry final = session.SampleTelemetry(reason, _crashCounts.GetValueOrDefault(pluginId));
        _lastTelemetry[pluginId] = final;
        await session.DisposeAsync().ConfigureAwait(false);
        _log.LogEvent(reason == "stopped" ? LogEventKind.UserAction : LogEventKind.Exception,
            "Plugin isolated host terminated.", new Dictionary<string, object?>
            {
                ["PluginId"] = pluginId,
                ["ProcessId"] = final.ProcessId,
                ["Reason"] = reason,
                ["WorkingSetBytes"] = final.WorkingSetBytes,
                ["CpuMilliseconds"] = final.TotalProcessorTime.TotalMilliseconds,
                ["ThreadCount"] = final.ThreadCount,
            });
    }

    private static string ResolveDefaultHostPath()
    {
        string executable = OperatingSystem.IsWindows() ? "LeslieCleaner.PluginHost.exe" : "LeslieCleaner.PluginHost";
        return Path.Combine(AppContext.BaseDirectory, executable);
    }

    private static PluginRuntimeResult Success(string id, string? payload, PluginRuntimeTelemetry? telemetry) =>
        new(true, id, payload, Telemetry: telemetry);

    private static PluginRuntimeResult Error(string id, string code, string message) => new(false, id, ErrorCode: code, Message: message);

    private static void ValidatePluginId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80 ||
            value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_')))
            throw new ArgumentException("Plugin identifier contains unsafe characters.", nameof(value));
    }

    private sealed class HostSession : IAsyncDisposable
    {
        private readonly string _pluginId;
        private readonly string _assemblyPath;
        private readonly string _apiVersion;
        private readonly PluginPermissionToken _permissionToken;
        private readonly string _authenticationToken;
        private readonly PluginRuntimeLimits _limits;
        private readonly Func<int> _crashCount;
        private readonly Process _process;
        private readonly NamedPipeClientStream _pipe;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _commandGate = new(1, 1);
        private TimeSpan _lastDuration;

        private HostSession(
            string pluginId,
            string assemblyPath,
            string apiVersion,
            PluginPermissionToken permissionToken,
            string authenticationToken,
            PluginRuntimeLimits limits,
            Func<int> crashCount,
            Process process,
            NamedPipeClientStream pipe)
        {
            _pluginId = pluginId;
            _assemblyPath = assemblyPath;
            _apiVersion = apiVersion;
            _permissionToken = permissionToken;
            _authenticationToken = authenticationToken;
            _limits = limits;
            _crashCount = crashCount;
            _process = process;
            _pipe = pipe;
            _reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, true);
            _writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
        }

        public int ProcessId => _process.Id;
        public bool IsAlive => !_process.HasExited && _pipe.IsConnected;

        public static async Task<HostSession> StartAsync(
            string pluginId,
            string assemblyPath,
            string apiVersion,
            string[] grantedPermissions,
            string hostPath,
            string pipeName,
            string authToken,
            string permissionSecret,
            PluginRuntimeLimits limits,
            Func<int> crashCount,
            CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo = CreateStartInfo(hostPath, pipeName, authToken);
            Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the isolated plugin host.");
            var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(limits.StartupTimeout);
            try
            {
                Task connect = pipe.ConnectAsync(deadline.Token);
                Task exited = process.WaitForExitAsync(deadline.Token);
                Task completed = await Task.WhenAny(connect, exited).ConfigureAwait(false);
                if (completed == exited) throw new IOException($"Plugin host exited before connecting (exit code {process.ExitCode}).");
                await connect.ConfigureAwait(false);
                return new HostSession(pluginId, assemblyPath, apiVersion,
                    new PluginPermissionToken(permissionSecret, grantedPermissions, DateTimeOffset.UtcNow.AddHours(1)),
                    authToken, limits, crashCount, process, pipe);
            }
            catch
            {
                pipe.Dispose();
                Kill(process);
                process.Dispose();
                throw;
            }
        }

        public Task<PluginRuntimeResult> InitializeAsync(CancellationToken cancellationToken) =>
            SendAsync(PluginProtocol.Initialize,
                new PluginInitializePayload(_pluginId, _assemblyPath, _apiVersion, _permissionToken),
                _limits.StartupTimeout, cancellationToken);

        public Task<PluginRuntimeResult> ExecuteAsync(PluginRuntimeRequest request, TimeSpan timeout, CancellationToken cancellationToken)
        {
            JsonElement payload = ParsePayload(request.PayloadJson);
            return SendAsync(PluginProtocol.Execute, new PluginExecutionRequest(request.Operation, payload), timeout, cancellationToken);
        }

        public Task<PluginRuntimeResult> StopAsync(CancellationToken cancellationToken) =>
            SendAsync(PluginProtocol.Stop, new { }, _limits.StopTimeout, cancellationToken);

        public PluginRuntimeTelemetry SampleTelemetry(string? reason = null, int? crashCount = null)
        {
            try
            {
                _process.Refresh();
                return new PluginRuntimeTelemetry(_process.Id, !_process.HasExited, _process.HasExited ? 0 : _process.WorkingSet64,
                    _process.TotalProcessorTime, _process.HasExited ? 0 : _process.Threads.Count, _lastDuration,
                    crashCount ?? _crashCount(), DateTimeOffset.UtcNow, reason);
            }
            catch (InvalidOperationException)
            {
                return new PluginRuntimeTelemetry(_process.Id, false, 0, TimeSpan.Zero, 0, _lastDuration,
                    crashCount ?? _crashCount(), DateTimeOffset.UtcNow, reason);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _reader.Dispose();
            try { await _writer.DisposeAsync().ConfigureAwait(false); }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
            try { await _pipe.DisposeAsync().ConfigureAwait(false); }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            try { Kill(_process); } catch { }
            try { _process.Dispose(); } catch { }
            try { _commandGate.Dispose(); } catch { }
        }

        private async Task<PluginRuntimeResult> SendAsync(string commandName, object payload, TimeSpan timeout, CancellationToken cancellationToken)
        {
            await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (_process.HasExited) return Error(_pluginId, "HOST_CRASHED", $"Plugin host exited with code {_process.ExitCode}.");
                var command = new PluginHostCommand(PluginProtocol.Version, Guid.NewGuid(), commandName, _authenticationToken,
                    JsonSerializer.SerializeToElement(payload, JsonOptions));
                await _writer.WriteLineAsync(JsonSerializer.Serialize(command, JsonOptions)).ConfigureAwait(false);

                using var timeoutSource = new CancellationTokenSource(timeout);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
                try
                {
                    while (true)
                    {
                        Task<string?> read = ReadFrameAsync(linked.Token);
                        Task<string> monitor = MonitorResourcesAsync(linked.Token);
                        Task exit = _process.WaitForExitAsync(linked.Token);
                        Task completed = await Task.WhenAny(read, monitor, exit).ConfigureAwait(false);
                        if (completed == read)
                        {
                            string? line = await read.ConfigureAwait(false);
                            if (line is null) return Error(_pluginId, "HOST_CRASHED", "Plugin host closed the protocol pipe.");
                            PluginHostResponse response = JsonSerializer.Deserialize<PluginHostResponse>(line, JsonOptions)
                                ?? throw new InvalidDataException("The plugin host returned an empty response.");
                            if (response.RequestId != command.RequestId || response.ProtocolVersion != PluginProtocol.Version)
                                return Error(_pluginId, "PROTOCOL_ERROR", "The plugin host returned a mismatched response.");
                            return new PluginRuntimeResult(response.Succeeded, _pluginId, response.Payload.GetRawText(), response.ErrorCode, response.Message);
                        }
                        if (completed == exit)
                        {
                            await exit.ConfigureAwait(false);
                            return Error(_pluginId, "HOST_CRASHED", $"Plugin host exited with code {_process.ExitCode}.");
                        }
                        string limit = await monitor.ConfigureAwait(false);
                        await TerminateAndWaitAsync(_process).ConfigureAwait(false);
                        return Error(_pluginId, "RESOURCE_LIMIT_EXCEEDED", limit);
                    }
                }
                catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    if (_process.HasExited)
                    {
                        await _process.WaitForExitAsync().ConfigureAwait(false);
                        return Error(_pluginId, "HOST_CRASHED", $"Plugin host exited with code {_process.ExitCode}.");
                    }
                    await TerminateAndWaitAsync(_process).ConfigureAwait(false);
                    return Error(_pluginId, "EXECUTION_TIMEOUT", "The plugin operation exceeded its deadline and the host was terminated.");
                }
                catch (IOException exception)
                {
                    return Error(_pluginId, "HOST_CRASHED", exception.Message);
                }
            }
            finally
            {
                stopwatch.Stop();
                _lastDuration = stopwatch.Elapsed;
                _commandGate.Release();
            }
        }

        private async Task<string?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();
            var buffer = new char[1];
            while (true)
            {
                int count = await _reader.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
                if (count == 0) return builder.Length == 0 ? null : throw new InvalidDataException("Incomplete response frame.");
                if (buffer[0] == '\n') return builder.ToString();
                if (buffer[0] != '\r') builder.Append(buffer[0]);
                if (builder.Length > PluginProtocol.MaxFrameCharacters) throw new InvalidDataException("Response frame exceeds the protocol limit.");
            }
        }

        private async Task<string> MonitorResourcesAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                await Task.Delay(_limits.MonitorInterval, cancellationToken).ConfigureAwait(false);
                PluginRuntimeTelemetry sample = SampleTelemetry();
                if (sample.WorkingSetBytes > _limits.MaximumWorkingSetBytes)
                    return $"Working set {sample.WorkingSetBytes} exceeded {_limits.MaximumWorkingSetBytes} bytes.";
                if (sample.TotalProcessorTime > _limits.MaximumProcessorTime)
                    return $"CPU time {sample.TotalProcessorTime} exceeded {_limits.MaximumProcessorTime}.";
                if (sample.ThreadCount > _limits.MaximumThreadCount)
                    return $"Thread count {sample.ThreadCount} exceeded {_limits.MaximumThreadCount}.";
            }
        }

        private static ProcessStartInfo CreateStartInfo(string hostPath, string pipeName, string authToken)
        {
            bool isDll = string.Equals(Path.GetExtension(hostPath), ".dll", StringComparison.OrdinalIgnoreCase);
            var start = new ProcessStartInfo
            {
                FileName = isDll ? ResolveDotNetHost(hostPath) : hostPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                WorkingDirectory = Path.GetDirectoryName(hostPath)!,
            };
            if (isDll) start.ArgumentList.Add(hostPath);
            start.ArgumentList.Add("--pipe");
            start.ArgumentList.Add(pipeName);
            start.ArgumentList.Add("--auth");
            start.ArgumentList.Add(authToken);
            start.ArgumentList.Add("--parent");
            start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return start;
        }

        private static string ResolveDotNetHost(string hostPath)
        {
            string executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
            string? explicitHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrWhiteSpace(explicitHost) && File.Exists(explicitHost))
                return Path.GetFullPath(explicitHost);

            string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrWhiteSpace(dotnetRoot))
            {
                string rootedCandidate = Path.Combine(dotnetRoot, executable);
                if (File.Exists(rootedCandidate)) return rootedCandidate;
            }

            string processCandidate = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty, executable);
            if (File.Exists(processCandidate)) return processCandidate;

            for (DirectoryInfo? directory = new(Path.GetDirectoryName(hostPath)!); directory is not null; directory = directory.Parent)
            {
                string adjacentCandidate = Path.Combine(directory.FullName, ".dotnet-sdk", executable);
                if (File.Exists(adjacentCandidate)) return adjacentCandidate;
            }

            return "dotnet";
        }

        private static JsonElement ParsePayload(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return JsonSerializer.SerializeToElement(new { });
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        private static async Task TerminateAndWaitAsync(Process process)
        {
            Kill(process);
            if (process.HasExited) return;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { }
        }

        private static void Kill(Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}

