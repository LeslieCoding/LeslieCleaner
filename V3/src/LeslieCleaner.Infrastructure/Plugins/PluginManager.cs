using System.Security.Cryptography;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Plugins;

/// <summary>
/// 本地插件发现器。插件必须位于 ProgramData 根目录下的独立子目录，并在加载前通过清单和 SHA-256 校验。
/// 本版本不反射加载第三方程序集，避免将“列出插件”变成代码执行入口。
/// </summary>
public sealed class PluginManager : IPluginManager
{
    private const string ManifestFileName = "plugin.json";
    private const string EnabledKeyPrefix = "plugins:enabled:";
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private readonly IPathService _pathService;
    private readonly IConfigService _configService;
    private readonly ILogService _logService;

    /// <summary>使用应用路径、配置和结构化日志服务创建插件管理器。</summary>
    public PluginManager(IPathService pathService, IConfigService configService, ILogService logService)
    {
        _pathService = pathService;
        _configService = configService;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PluginDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        string pluginRoot = _pathService.EnsureDirectory(Path.Combine(_pathService.ProgramDataRoot, "Plugins"));
        var plugins = new List<PluginDescriptor>();
        foreach (string pluginDirectory in Directory.EnumerateDirectories(pluginRoot, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileAttributes attributes = File.GetAttributes(pluginDirectory);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                _logService.LogEvent(LogEventKind.UserAction, "跳过插件重解析点目录",
                    new Dictionary<string, object?> { ["Directory"] = pluginDirectory });
                continue;
            }

            string manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            plugins.Add(await ValidateManifestAsync(pluginRoot, manifestPath, cancellationToken).ConfigureAwait(false));
        }

        return plugins.OrderBy(static plugin => plugin.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <inheritdoc />
    public Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default)
    {
        ValidatePluginId(pluginId);
        return _configService.SetValueAsync(EnabledKeyPrefix + pluginId, enabled, cancellationToken);
    }

    private async Task<PluginDescriptor> ValidateManifestAsync(
        string pluginRoot,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        PluginManifest? manifest;
        try
        {
            if ((File.GetAttributes(manifestPath) & FileAttributes.ReparsePoint) != 0)
            {
                return Invalid(manifestPath, PluginTrustStatus.UnsafePath, "插件清单不能是重解析点。");
            }

            await using FileStream stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<PluginManifest>(stream, ManifestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _logService.LogException(exception, "PluginManager.ReadManifest",
                new Dictionary<string, object?> { ["ManifestPath"] = manifestPath });
            return Invalid(manifestPath, PluginTrustStatus.InvalidManifest, "无法读取或解析 plugin.json。");
        }

        if (manifest is null || !IsValidManifest(manifest))
        {
            return Invalid(manifestPath, PluginTrustStatus.InvalidManifest, "插件清单缺少必填字段或插件标识不安全。");
        }

        string pluginDirectoryPath = Path.GetFullPath(Path.GetDirectoryName(manifestPath)!)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string assemblyPath;
        try
        {
            assemblyPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.EntryAssembly));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CreateDescriptor(manifest, manifestPath, string.Empty, PluginTrustStatus.UnsafePath, exception.Message);
        }

        if (!assemblyPath.StartsWith(pluginDirectoryPath, StringComparison.OrdinalIgnoreCase))
        {
            return CreateDescriptor(manifest, manifestPath, assemblyPath, PluginTrustStatus.UnsafePath,
                "插件程序集路径超出插件自身目录。");
        }

        if (!File.Exists(assemblyPath))
        {
            return CreateDescriptor(manifest, manifestPath, assemblyPath, PluginTrustStatus.MissingAssembly, "插件程序集不存在。");
        }

        if ((File.GetAttributes(assemblyPath) & FileAttributes.ReparsePoint) != 0)
        {
            return CreateDescriptor(manifest, manifestPath, assemblyPath, PluginTrustStatus.UnsafePath,
                "插件程序集不能是重解析点。");
        }

        string actualHash;
        await using (FileStream assembly = File.OpenRead(assemblyPath))
        {
            actualHash = Convert.ToHexString(await SHA256.HashDataAsync(assembly, cancellationToken).ConfigureAwait(false));
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(manifest.Sha256), Convert.FromHexString(actualHash)))
        {
            return CreateDescriptor(manifest, manifestPath, assemblyPath, PluginTrustStatus.HashMismatch, "插件程序集哈希不匹配。");
        }

        bool enabled = _configService.GetValue(EnabledKeyPrefix + manifest.Id, false);
        return CreateDescriptor(manifest, manifestPath, assemblyPath,
            enabled ? PluginTrustStatus.Ready : PluginTrustStatus.Disabled,
            enabled ? "已验证，可由受信任宿主启用。" : "已验证，默认禁用。");
    }

    private static bool IsValidManifest(PluginManifest manifest) =>
        !string.IsNullOrWhiteSpace(manifest.Name) &&
        !string.IsNullOrWhiteSpace(manifest.Version) &&
        !string.IsNullOrWhiteSpace(manifest.EntryAssembly) &&
        IsSafePluginId(manifest.Id) &&
        manifest.Sha256.Length == 64 &&
        manifest.Sha256.All(Uri.IsHexDigit);

    private static bool IsSafePluginId(string? pluginId) =>
        !string.IsNullOrWhiteSpace(pluginId) && pluginId.Length <= 80 &&
        pluginId.All(static character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

    private static void ValidatePluginId(string pluginId)
    {
        if (!IsSafePluginId(pluginId))
        {
            throw new ArgumentException("插件标识只能包含 ASCII 字母、数字、点、连字符或下划线。", nameof(pluginId));
        }
    }

    private static PluginDescriptor Invalid(string manifestPath, PluginTrustStatus status, string detail) =>
        new("unknown", "未知插件", "unknown", string.Empty, manifestPath, string.Empty, status, detail);

    private static PluginDescriptor CreateDescriptor(
        PluginManifest manifest,
        string manifestPath,
        string assemblyPath,
        PluginTrustStatus status,
        string detail) =>
        new(manifest.Id, manifest.Name, manifest.Version, manifest.Description ?? string.Empty,
            manifestPath, assemblyPath, status, detail);

    private sealed record PluginManifest(string Id, string Name, string Version, string EntryAssembly, string Sha256, string? Description);
}
