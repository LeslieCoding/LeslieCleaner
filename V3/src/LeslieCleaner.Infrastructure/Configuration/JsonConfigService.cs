using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Configuration;

/// <summary>
/// JSON 配置服务实现。特性：System.Text.Json 存储、FileSystemWatcher 热更新（300ms 防抖）、
/// 变更通知事件、ReaderWriterLockSlim 线程安全读写、默认值自举、自身写入回环抑制。
/// 变更事件在线程池线程触发，订阅方如需更新 UI 请自行封送。
/// </summary>
public sealed class JsonConfigService : IConfigService, IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string EncryptedConfigFormat = "lesliecleaner.config.aes-gcm.v1";

    private readonly string _configFilePath;
    private readonly ILogService _logService;
    private readonly IEncryptionService? _encryptionService;
    private readonly ReaderWriterLockSlim _gate = new(LockRecursionPolicy.NoRecursion);
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _debounceTimer;

    private JsonObject _root;
    private string _lastWrittenHash = string.Empty;
    private int _disposed;

    /// <inheritdoc />
    public event EventHandler<ConfigChangedEventArgs>? Changed;

    /// <summary>
    /// 创建配置服务并加载（或自举）配置文件。
    /// </summary>
    /// <param name="configFilePath">配置文件完整路径；目录不存在时自动创建。</param>
    /// <param name="logService">日志服务。</param>
    /// <param name="encryptionService">Optional authenticated encryption for persisted configuration data.</param>
    public JsonConfigService(
        string configFilePath,
        ILogService logService,
        IEncryptionService? encryptionService = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFilePath);
        _configFilePath = configFilePath;
        _logService = logService;
        _encryptionService = encryptionService;
        _root = LoadFromDisk() ?? new JsonObject();

        string directory = Path.GetDirectoryName(_configFilePath)!;
        Directory.CreateDirectory(directory);
        _watcher = new FileSystemWatcher(directory, Path.GetFileName(_configFilePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileChanged;
        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public T GetValue<T>(string key, T defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _gate.EnterReadLock();
        try
        {
            if (TryGetNode(key, out JsonNode? node) && node is not null)
            {
                try
                {
                    // Deserialize 兼容标量/数组/对象（JsonNode.GetValue<T> 仅支持 JsonValue）
                    T? value = node.Deserialize<T>(JsonOptions);
                    return value is null ? defaultValue : value;
                }
                catch (Exception ex) when (ex is InvalidOperationException or FormatException or JsonException or NotSupportedException)
                {
                    _logService.LogException(ex, "JsonConfigService.GetValue",
                        new Dictionary<string, object?> { ["Key"] = key });
                    return defaultValue;
                }
            }
        }
        finally
        {
            _gate.ExitReadLock();
        }

        // 默认值自举：键不存在时写入默认值
        SetValueInternal(key, defaultValue, fireEvent: false);
        return defaultValue;
    }

    /// <inheritdoc />
    public Task SetValueAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        SetValueInternal(key, value, fireEvent: true);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        JsonObject? loaded = await Task.Run(LoadFromDisk, cancellationToken).ConfigureAwait(false);
        if (loaded is null)
        {
            return;
        }

        string newHash = ComputeHash(loaded.ToJsonString(JsonOptions));
        _gate.EnterWriteLock();
        List<string> changedKeys;
        try
        {
            if (newHash == _lastWrittenHash)
            {
                // 自身写入引发的回环事件，忽略
                return;
            }

            Dictionary<string, string> before = Flatten(_root);
            Dictionary<string, string> after = Flatten(loaded);
            changedKeys = DiffKeys(before, after);
            _root = loaded;
            _lastWrittenHash = newHash;
        }
        finally
        {
            _gate.ExitWriteLock();
        }

        if (changedKeys.Count > 0)
        {
            RaiseChanged(new ConfigChangedEventArgs(changedKeys, isFullReload: false));
        }
    }

    /// <summary>释放文件监听与计时器资源。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFileChanged;
        _watcher.Created -= OnFileChanged;
        _watcher.Renamed -= OnFileChanged;
        _watcher.Dispose();
        _debounceTimer.Dispose();
        _gate.Dispose();
    }

    private void SetValueInternal<T>(string key, T value, bool fireEvent)
    {
        JsonNode? serialized;
        try
        {
            serialized = JsonSerializer.SerializeToNode(value, JsonOptions);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            throw new ConfigurationException($"配置值无法序列化，键: {key}", ex);
        }

        _gate.EnterWriteLock();
        try
        {
            SetNode(key, serialized);
            Persist();
        }
        finally
        {
            _gate.ExitWriteLock();
        }

        if (fireEvent)
        {
            RaiseChanged(new ConfigChangedEventArgs([key], isFullReload: false));
        }
    }

    private void Persist()
    {
        string json = _root.ToJsonString(JsonOptions);
        _lastWrittenHash = ComputeHash(json);
        string persisted = EncryptForStorage(json);

        string directory = Path.GetDirectoryName(_configFilePath)!;
        Directory.CreateDirectory(directory);

        // 先写临时文件再原子替换，避免监听方读到半截文件
        string tempPath = _configFilePath + ".tmp";
        File.WriteAllText(tempPath, persisted, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tempPath, _configFilePath, overwrite: true);
    }

    private JsonObject? LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                return null;
            }

            string persisted = File.ReadAllText(_configFilePath);
            string json = DecryptFromStorage(persisted);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new JsonObject();
            }

            _lastWrittenHash = ComputeHash(json);
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or CryptographicException or FormatException)
        {
            _logService.LogException(ex, "JsonConfigService.LoadFromDisk",
                new Dictionary<string, object?> { ["Path"] = _configFilePath });
            return null;
        }
    }

    private bool TryGetNode(string key, out JsonNode? node)
    {
        JsonNode? current = _root;
        foreach (string segment in key.Split(':'))
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment, out current))
            {
                node = null;
                return false;
            }
        }

        node = current;
        return true;
    }

    private void SetNode(string key, JsonNode? value)
    {
        string[] segments = key.Split(':');
        JsonObject current = _root;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (current[segments[i]] is not JsonObject next)
            {
                next = new JsonObject();
                current[segments[i]] = next;
            }

            current = next;
        }

        current[segments[^1]] = value;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            _debounceTimer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Dispose 竞态：服务已释放，事件可安全丢弃
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            ReloadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (ObjectDisposedException)
        {
            // Dispose 竞态：读写锁已释放，直接退出
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logService.LogException(ex, "JsonConfigService.HotReload");
        }
    }

    private void RaiseChanged(ConfigChangedEventArgs args)
    {
        try
        {
            Changed?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            // 订阅方异常不得影响配置服务本身
            _logService.LogException(ex, "JsonConfigService.ChangedSubscriber");
        }
    }

    private static Dictionary<string, string> Flatten(JsonObject root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        FlattenInto(root, prefix: string.Empty, result);
        return result;
    }

    private static void FlattenInto(JsonObject obj, string prefix, Dictionary<string, string> result)
    {
        foreach (KeyValuePair<string, JsonNode?> pair in obj)
        {
            string path = prefix.Length == 0 ? pair.Key : $"{prefix}:{pair.Key}";
            if (pair.Value is JsonObject child)
            {
                FlattenInto(child, path, result);
            }
            else
            {
                result[path] = pair.Value?.ToJsonString() ?? "null";
            }
        }
    }

    private static List<string> DiffKeys(Dictionary<string, string> before, Dictionary<string, string> after)
    {
        var keys = new List<string>();
        foreach (KeyValuePair<string, string> pair in after)
        {
            if (!before.TryGetValue(pair.Key, out string? oldValue) || oldValue != pair.Value)
            {
                keys.Add(pair.Key);
            }
        }

        foreach (string key in before.Keys)
        {
            if (!after.ContainsKey(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private static string ComputeHash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private string EncryptForStorage(string json)
    {
        if (_encryptionService is null)
        {
            return json;
        }

        byte[] plaintext = Encoding.UTF8.GetBytes(json);
        try
        {
            byte[] encrypted = _encryptionService.Encrypt(plaintext);
            return JsonSerializer.Serialize(new EncryptedConfigEnvelope(
                EncryptedConfigFormat,
                Convert.ToBase64String(encrypted)), JsonOptions);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private string DecryptFromStorage(string persisted)
    {
        JsonObject? envelope = JsonNode.Parse(persisted) as JsonObject;
        string? format = envelope?["Format"]?.GetValue<string>();
        if (!EncryptedConfigFormat.Equals(format, StringComparison.Ordinal))
        {
            return persisted;
        }

        if (_encryptionService is null)
        {
            throw new CryptographicException("Encrypted configuration requires the local encryption service.");
        }

        string payload = envelope?["Payload"]?.GetValue<string>()
            ?? throw new CryptographicException("Encrypted configuration payload is missing.");
        byte[] decrypted = _encryptionService.Decrypt(Convert.FromBase64String(payload));
        try
        {
            return Encoding.UTF8.GetString(decrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decrypted);
        }
    }

    private sealed record EncryptedConfigEnvelope(string Format, string Payload);
}
