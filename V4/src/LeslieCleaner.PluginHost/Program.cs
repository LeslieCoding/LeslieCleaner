using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using LeslieCleaner.PluginSdk;

return await PluginHostProgram.RunAsync(args).ConfigureAwait(false);

internal static class PluginHostProgram
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(string[] args)
    {
        HostArguments? options = HostArguments.Parse(args);
        if (options is null)
        {
            Console.Error.WriteLine("Required arguments: --pipe <name> --auth <token> --parent <pid>");
            return 64;
        }

        using var shutdown = new CancellationTokenSource();
        _ = MonitorParentAsync(options.ParentProcessId, shutdown);
        try
        {
            await using var pipe = new NamedPipeServerStream(
                options.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(shutdown.Token).ConfigureAwait(false);
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
            var session = new PluginSession();

            while (!shutdown.IsCancellationRequested && pipe.IsConnected)
            {
                string? line = await ReadFrameAsync(reader, shutdown.Token).ConfigureAwait(false);
                if (line is null) break;
                PluginHostResponse response;
                PluginHostCommand? command = null;
                try
                {
                    command = JsonSerializer.Deserialize<PluginHostCommand>(line, JsonOptions)
                        ?? throw new JsonException("Command frame is empty.");
                    response = await session.HandleAsync(command, options.AuthenticationToken, shutdown.Token).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentException)
                {
                    response = new PluginHostResponse(
                        PluginProtocol.Version,
                        command?.RequestId ?? Guid.Empty,
                        false,
                        JsonSerializer.SerializeToElement(new { }),
                        "INVALID_COMMAND",
                        exception.Message);
                }

                await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions)).ConfigureAwait(false);
                if (string.Equals(command?.Command, PluginProtocol.Stop, StringComparison.Ordinal)) break;
            }

            await session.DisposeAsync().ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 70;
        }
        finally
        {
            shutdown.Cancel();
        }
    }

    private static async Task<string?> ReadFrameAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[1];
        while (true)
        {
            int count = await reader.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (count == 0) return builder.Length == 0 ? null : throw new InvalidDataException("Incomplete command frame.");
            if (buffer[0] == '\n') return builder.ToString();
            if (buffer[0] != '\r') builder.Append(buffer[0]);
            if (builder.Length > PluginProtocol.MaxFrameCharacters) throw new InvalidDataException("Command frame exceeds the protocol limit.");
        }
    }

    private static async Task MonitorParentAsync(int processId, CancellationTokenSource shutdown)
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(processId);
            await process.WaitForExitAsync(shutdown.Token).ConfigureAwait(false);
            shutdown.Cancel();
        }
        catch (ArgumentException)
        {
            shutdown.Cancel();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed record HostArguments(string PipeName, string AuthenticationToken, int ParentProcessId)
    {
        public static HostArguments? Parse(string[] args)
        {
            string? pipe = Value(args, "--pipe");
            string? auth = Value(args, "--auth");
            return pipe is not null && auth is not null &&
                int.TryParse(Value(args, "--parent"), out int parent) && parent > 0
                    ? new HostArguments(pipe, auth, parent)
                    : null;
        }

        private static string? Value(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }

    private sealed class PluginSession : IAsyncDisposable
    {
        private PluginLoadContext? _loadContext;
        private IPlugin? _plugin;
        private PluginContext? _context;

        public async ValueTask<PluginHostResponse> HandleAsync(
            PluginHostCommand command,
            string expectedAuthenticationToken,
            CancellationToken cancellationToken)
        {
            if (!string.Equals(command.ProtocolVersion, PluginProtocol.Version, StringComparison.Ordinal))
                return Error(command, "PROTOCOL_MISMATCH", "The host protocol version is incompatible.");
            if (!FixedTimeEquals(command.AuthenticationToken, expectedAuthenticationToken))
                return Error(command, "AUTHENTICATION_FAILED", "The command authentication token is invalid.");

            try
            {
                return command.Command switch
                {
                    PluginProtocol.Initialize => await InitializeAsync(command, cancellationToken).ConfigureAwait(false),
                    PluginProtocol.Execute => await ExecuteAsync(command, cancellationToken).ConfigureAwait(false),
                    PluginProtocol.Stop => await StopAsync(command, cancellationToken).ConfigureAwait(false),
                    _ => Error(command, "UNKNOWN_COMMAND", "The requested host command is unknown."),
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Error(command, "PLUGIN_FAILURE", exception.GetBaseException().Message);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_plugin is not null)
            {
                try { await _plugin.StopAsync(CancellationToken.None).ConfigureAwait(false); }
                catch { }
            }
            _plugin = null;
            _context = null;
            _loadContext?.Unload();
            _loadContext = null;
        }

        private async ValueTask<PluginHostResponse> InitializeAsync(PluginHostCommand command, CancellationToken cancellationToken)
        {
            if (_plugin is not null) return Error(command, "ALREADY_INITIALIZED", "The plugin is already initialized.");
            PluginInitializePayload payload = command.Payload.Deserialize<PluginInitializePayload>(JsonOptions)
                ?? throw new InvalidDataException("Initialization payload is missing.");
            if (!string.Equals(payload.ApiVersion, PluginApi.Version, StringComparison.Ordinal))
                return Error(command, "SDK_VERSION_MISMATCH", "The plugin SDK version is incompatible.");
            if (payload.PermissionToken.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                return Error(command, "PERMISSION_TOKEN_EXPIRED", "The permission token has expired.");

            string assemblyPath = Path.GetFullPath(payload.AssemblyPath);
            if (!File.Exists(assemblyPath)) return Error(command, "ASSEMBLY_NOT_FOUND", "The plugin entry assembly does not exist.");
            _loadContext = new PluginLoadContext(assemblyPath);
            Assembly assembly = _loadContext.LoadFromAssemblyPath(assemblyPath);
            Type[] pluginTypes = assembly.GetTypes().Where(static type =>
                !type.IsAbstract && !type.IsInterface && typeof(IPlugin).IsAssignableFrom(type)).ToArray();
            if (pluginTypes.Length != 1)
                return Error(command, "PLUGIN_ENTRY_INVALID", "The plugin assembly must contain exactly one public IPlugin implementation.");
            _plugin = Activator.CreateInstance(pluginTypes[0]) as IPlugin
                ?? throw new InvalidDataException("The plugin must have a public parameterless constructor.");
            _context = new PluginContext(payload.PermissionToken);
            await _plugin.InitializeAsync(_context, cancellationToken).ConfigureAwait(false);
            return Success(command, new { pluginId = payload.PluginId, processId = Environment.ProcessId });
        }

        private async ValueTask<PluginHostResponse> ExecuteAsync(PluginHostCommand command, CancellationToken cancellationToken)
        {
            if (_plugin is null) return Error(command, "NOT_INITIALIZED", "The plugin is not initialized.");
            PluginExecutionRequest request = command.Payload.Deserialize<PluginExecutionRequest>(JsonOptions)
                ?? throw new InvalidDataException("Execution payload is missing.");
            PluginExecutionResponse result = await _plugin.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return new PluginHostResponse(
                PluginProtocol.Version,
                command.RequestId,
                result.Succeeded,
                result.Payload,
                result.ErrorCode,
                result.Message);
        }

        private async ValueTask<PluginHostResponse> StopAsync(PluginHostCommand command, CancellationToken cancellationToken)
        {
            if (_plugin is not null) await _plugin.StopAsync(cancellationToken).ConfigureAwait(false);
            _plugin = null;
            _context = null;
            _loadContext?.Unload();
            _loadContext = null;
            return Success(command, new { stopped = true });
        }

        private static PluginHostResponse Success(PluginHostCommand command, object payload) =>
            new(PluginProtocol.Version, command.RequestId, true, JsonSerializer.SerializeToElement(payload, JsonOptions));

        private static PluginHostResponse Error(PluginHostCommand command, string code, string message) =>
            new(PluginProtocol.Version, command.RequestId, false, JsonSerializer.SerializeToElement(new { }), code, message);

        private static bool FixedTimeEquals(string left, string right)
        {
            byte[] first = Encoding.UTF8.GetBytes(left);
            byte[] second = Encoding.UTF8.GetBytes(right);
            return first.Length == second.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(first, second);
        }
    }

    private sealed class PluginContext(PluginPermissionToken permissionToken) : IPluginContext
    {
        private readonly HashSet<string> _permissions = new(permissionToken.Permissions, StringComparer.OrdinalIgnoreCase);

        public PluginPermissionToken PermissionToken { get; } = permissionToken;

        public ValueTask<PluginCapabilityResponse> RequestCapabilityAsync(
            PluginCapabilityRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PermissionToken.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
                !_permissions.Contains(request.Permission))
            {
                return ValueTask.FromResult(new PluginCapabilityResponse(
                    false,
                    JsonSerializer.SerializeToElement(new { }),
                    "PERMISSION_DENIED",
                    $"Permission '{request.Permission}' was not granted."));
            }

            return ValueTask.FromResult(new PluginCapabilityResponse(
                false,
                JsonSerializer.SerializeToElement(new { }),
                "CAPABILITY_NOT_SUPPORTED",
                "The permission is granted, but this host version does not expose that privileged operation."));
        }
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string assemblyPath) : base(isCollectible: true) =>
            _resolver = new AssemblyDependencyResolver(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.Equals(assemblyName.Name, typeof(IPlugin).Assembly.GetName().Name, StringComparison.Ordinal))
                return typeof(IPlugin).Assembly;
            string? path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
