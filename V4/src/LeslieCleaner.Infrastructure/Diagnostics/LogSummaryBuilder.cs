using System.Text.Json;
using System.Text.RegularExpressions;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Diagnostics;

/// <summary>
/// 事件种类计数。
/// </summary>
/// <param name="EventKind">事件种类（ScanStart/DeleteFinish/Exception 等）。</param>
/// <param name="Count">次数。</param>
public sealed record EventKindCount(string EventKind, int Count);

/// <summary>
/// 异常类型计数。
/// </summary>
/// <param name="TypeName">异常类型名（不含消息与堆栈，无隐私内容）。</param>
/// <param name="Count">次数。</param>
/// <param name="LastSeenUtc">最近发生时间。</param>
public sealed record ExceptionTypeCount(string TypeName, int Count, DateTimeOffset LastSeenUtc);

/// <summary>
/// 错误汇总（近 N 天，仅结构化元数据，不含路径/用户名等隐私）。
/// </summary>
/// <param name="GeneratedUtc">生成时间。</param>
/// <param name="DaysCovered">覆盖天数。</param>
/// <param name="TotalWarnings">Warning 总数。</param>
/// <param name="TotalErrors">Error 总数。</param>
/// <param name="ByEventKind">按事件种类计数。</param>
/// <param name="TopExceptions">Top 异常类型（按次数降序）。</param>
public sealed record ErrorSummary(
    DateTimeOffset GeneratedUtc,
    int DaysCovered,
    int TotalWarnings,
    int TotalErrors,
    IReadOnlyList<EventKindCount> ByEventKind,
    IReadOnlyList<ExceptionTypeCount> TopExceptions);

/// <summary>
/// 日志文件解析与错误汇总（Serilog 文本格式：
/// "时间戳 [级别] [事件种类] 消息" + 异常行）。供诊断报告与更新诊断共用。
/// </summary>
public static class LogSummaryBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Regex LogLineRegex = new(@"^\s*(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d+(?:\s*[+-]\d{2}:\d{2})?)\s+\[(?<level>[^\]]+)\]\s+\[(?<kind>[^\]]+)\]\s+(?<message>.*)$", RegexOptions.Compiled);

    /// <summary>
    /// 解析指定目录下近 <paramref name="days"/> 天的日志，生成错误汇总。
    /// </summary>
    /// <param name="logDirectory">日志目录。</param>
    /// <param name="days">覆盖天数。</param>
    /// <param name="topExceptions">异常类型保留条数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static async Task<ErrorSummary> BuildAsync(
        string logDirectory,
        int days = 30,
        int topExceptions = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        int warnings = 0;
        int errors = 0;
        var byEventKind = new Dictionary<string, int>(StringComparer.Ordinal);
        var exceptions = new Dictionary<string, (int Count, DateTimeOffset LastSeen)>(StringComparer.Ordinal);

        if (Directory.Exists(logDirectory))
        {
            foreach (string file in Directory.EnumerateFiles(logDirectory, "*.log"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.GetLastWriteTimeUtc(file) < cutoff.UtcDateTime)
                {
                    continue;
                }

                await ParseFileAsync(file, cutoff, warnings: () => warnings++,
                    errors: () => errors++, byEventKind, exceptions, cancellationToken).ConfigureAwait(false);
            }
        }

        return new ErrorSummary(
            DateTimeOffset.UtcNow,
            days,
            warnings,
            errors,
            [.. byEventKind.OrderByDescending(static p => p.Value)
                .Select(static p => new EventKindCount(p.Key, p.Value))],
            [.. exceptions.OrderByDescending(static p => p.Value.Count)
                .Take(topExceptions)
                .Select(static p => new ExceptionTypeCount(p.Key, p.Value.Count, p.Value.LastSeen))]);
    }

    /// <summary>
    /// 将汇总序列化为 JSON。
    /// </summary>
    /// <param name="summary">错误汇总。</param>
    public static string ToJson(ErrorSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static async Task ParseFileAsync(
        string file,
        DateTimeOffset cutoff,
        Action warnings,
        Action errors,
        Dictionary<string, int> byEventKind,
        Dictionary<string, (int Count, DateTimeOffset LastSeen)> exceptions,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        string? line;
        DateTimeOffset? currentTimestamp = null;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            var match = LogLineRegex.Match(line);
            if (match.Success)
            {
                string tsStr = match.Groups["timestamp"].Value;
                if (DateTimeOffset.TryParse(tsStr, out DateTimeOffset parsed))
                {
                    currentTimestamp = parsed;
                }

                string level = match.Groups["level"].Value;
                string kind = match.Groups["kind"].Value;

                if (level == "WRN")
                {
                    warnings();
                }
                else if (level == "ERR")
                {
                    errors();
                }

                byEventKind[kind] = byEventKind.TryGetValue(kind, out int count) ? count + 1 : 1;
            }
            else if (line.StartsWith("System.", StringComparison.Ordinal) ||
                     line.StartsWith("Microsoft.", StringComparison.Ordinal))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex > 8)
                {
                    string typeName = line[..colonIndex];
                    if (currentTimestamp is null || currentTimestamp >= cutoff)
                    {
                        DateTimeOffset seen = currentTimestamp ?? File.GetLastWriteTimeUtc(file);
                        exceptions[typeName] = exceptions.TryGetValue(typeName, out (int Count, DateTimeOffset LastSeen) existing)
                            ? (existing.Count + 1, existing.LastSeen > seen ? existing.LastSeen : seen)
                            : (1, seen);
                    }
                }
            }
        }
    }
}
