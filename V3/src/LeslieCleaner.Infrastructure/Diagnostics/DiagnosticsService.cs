using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Diagnostics;

/// <summary>
/// 诊断报告服务：一键生成 zip（近 7 天日志 + error-summary.json 聚合统计 +
/// system-info.json + 脱敏配置快照）。供"导出诊断报告"与更新诊断共用错误汇总。
/// </summary>
public sealed class DiagnosticsService
{
    /// <summary>日志覆盖天数（报告内含最近 7 天日志文件）。</summary>
    public const int LogDaysIncluded = 7;

    /// <summary>错误汇总覆盖天数。</summary>
    public const int SummaryDays = 30;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] SensitiveKeyFragments =
        ["secret", "password", "token", "credential", "apikey", "path", "url", "user"];

    private readonly IPathService _pathService;
    private readonly IConfigService _configService;
    private readonly ILogService _logService;

    /// <summary>以路径、配置与日志服务创建诊断服务。</summary>
    /// <param name="pathService">路径服务。</param>
    /// <param name="configService">配置服务。</param>
    /// <param name="logService">日志服务。</param>
    public DiagnosticsService(IPathService pathService, IConfigService configService, ILogService logService)
    {
        _pathService = pathService;
        _configService = configService;
        _logService = logService;
    }

    /// <summary>
    /// 生成错误汇总（近 30 天，不含隐私内容）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<ErrorSummary> BuildErrorSummaryAsync(CancellationToken cancellationToken = default) =>
        LogSummaryBuilder.BuildAsync(_pathService.LogDirectory, SummaryDays, cancellationToken: cancellationToken);

    /// <summary>
    /// 生成诊断报告 zip 到指定目录。
    /// </summary>
    /// <param name="outputDirectory">输出目录（不存在自动创建）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>zip 文件完整路径。</returns>
    public async Task<string> GenerateReportAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        string zipPath = Path.Combine(outputDirectory,
            $"LeslieCleaner-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

        _logService.LogEvent(LogEventKind.UserAction, "诊断报告生成开始",
            new Dictionary<string, object?> { ["Output"] = zipPath });

        ErrorSummary summary = await BuildErrorSummaryAsync(cancellationToken).ConfigureAwait(false);

        await using (FileStream zipStream = new(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            // 近 7 天日志文件
            DateTime cutoff = DateTime.UtcNow.AddDays(-LogDaysIncluded);
            if (Directory.Exists(_pathService.LogDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(_pathService.LogDirectory, "*.log"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (File.GetLastWriteTimeUtc(file) >= cutoff)
                    {
                        ZipArchiveEntry entry = archive.CreateEntry($"logs/{Path.GetFileName(file)}");
                        await using Stream entryStream = entry.Open();
                        await using FileStream source = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, FileOptions.Asynchronous);
                        await source.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            await WriteEntryAsync(archive, "error-summary.json", LogSummaryBuilder.ToJson(summary), cancellationToken).ConfigureAwait(false);
            await WriteEntryAsync(archive, "system-info.json", BuildSystemInfoJson(), cancellationToken).ConfigureAwait(false);
            await WriteEntryAsync(archive, "config-snapshot.json", BuildSanitizedConfigJson(), cancellationToken).ConfigureAwait(false);
        }

        _logService.LogEvent(LogEventKind.UserAction, "诊断报告生成完成", new Dictionary<string, object?>
        {
            ["Output"] = zipPath,
            ["Warnings"] = summary.TotalWarnings,
            ["Errors"] = summary.TotalErrors,
        });
        return zipPath;
    }

    /// <summary>
    /// 生成脱敏配置快照 JSON（敏感键值替换为 "***"）。
    /// </summary>
    public string BuildSanitizedConfigJson()
    {
        string configFile = Path.Combine(_pathService.ConfigDirectory, "settings.json");
        if (!File.Exists(configFile))
        {
            return "{}";
        }

        try
        {
            JsonNode? root = JsonNode.Parse(File.ReadAllText(configFile));
            if (root is null)
            {
                return "{}";
            }

            SanitizeNode(root, parentKey: string.Empty);
            return root.ToJsonString(JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "DiagnosticsService.BuildSanitizedConfigJson");
            return "{}";
        }
    }

    private static void SanitizeNode(JsonNode node, string parentKey)
    {
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> pair in obj.ToArray())
            {
                string keyPath = string.IsNullOrEmpty(parentKey) ? pair.Key : $"{parentKey}:{pair.Key}";
                if (IsSensitiveKey(keyPath) && pair.Value is JsonValue)
                {
                    obj[pair.Key] = "***";
                }
                else if (pair.Value is not null)
                {
                    SanitizeNode(pair.Value, keyPath);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                if (item is not null)
                {
                    SanitizeNode(item, parentKey);
                }
            }
        }
    }

    private static bool IsSensitiveKey(string keyPath)
    {
        string lowered = keyPath.ToLowerInvariant();
        return SensitiveKeyFragments.Any(fragment => lowered.Contains(fragment, StringComparison.Ordinal));
    }

    private string BuildSystemInfoJson()
    {
        var systemDrive = new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System))!);
        var info = new
        {
            OsVersion = Environment.OSVersion.ToString(),
            Os64Bit = Environment.Is64BitOperatingSystem,
            DotNetVersion = Environment.Version.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            WorkingSetMB = Math.Round(Environment.WorkingSet / 1048576.0, 1),
            SystemDriveFreeMB = systemDrive.AvailableFreeSpace / 1048576,
            SystemDriveTotalMB = systemDrive.TotalSize / 1048576,
            AppVersion = typeof(DiagnosticsService).Assembly.GetName().Version?.ToString() ?? "1.0.2",
            GeneratedUtc = DateTimeOffset.UtcNow,
        };
        return JsonSerializer.Serialize(info, JsonOptions);
    }

    private static async Task WriteEntryAsync(
        ZipArchive archive, string name, string content, CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name);
        await using Stream stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
    }
}
