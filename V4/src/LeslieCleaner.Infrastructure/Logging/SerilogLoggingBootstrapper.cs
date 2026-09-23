using LeslieCleaner.Core.Abstractions;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Text;

namespace LeslieCleaner.Infrastructure.Logging;

/// <summary>
/// Serilog 集中配置引导。普通事件进入有背压的异步滚动文件；Warning/Error 另写同步 JSONL 审计文件，
/// Serilog 自身故障写入独立 self-log。输出目录 %LocalAppData%\LeslieCleaner\Logs，保留 31 天。
/// </summary>
public sealed class SerilogLoggingBootstrapper : ILoggingBootstrapper
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{EventKind}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    private readonly IPathService _pathService;
    private readonly LoggingOptions _options;
    private readonly object _bootstrapGate = new();
    private Serilog.ILogger? _logger;

    /// <summary>以路径服务创建引导器。</summary>
    /// <param name="pathService">路径服务。</param>
    /// <param name="options">日志级别、保留与大小轮转配置。</param>
    public SerilogLoggingBootstrapper(IPathService pathService, LoggingOptions? options = null)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _options = options ?? new LoggingOptions();
        _options.Validate();
    }

    /// <inheritdoc />
    public Serilog.ILogger Bootstrap()
    {
        lock (_bootstrapGate)
        {
            if (_logger is not null)
            {
                return _logger;
            }

            string logDirectory = _pathService.EnsureDirectory(_pathService.LogDirectory);
            string logFile = Path.Combine(logDirectory, "lesliecleaner-.log");
            string auditFile = Path.Combine(logDirectory, "audit-.jsonl");
            string selfLogFile = Path.Combine(logDirectory, "serilog-self.log");

            // Sink 内部异常默认只会静默进入 SelfLog。按次打开/关闭文件，避免全局 SelfLog
            // 长期持有句柄，也确保主日志目录损坏时仍有独立诊断尝试。
            SelfLog.Enable(message => AppendSelfLogSafely(selfLogFile, message));

            _logger = new LoggerConfiguration()
                .MinimumLevel.Is(_options.MinimumLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                // Destructive operations must be observable before the command returns. The
                // previous async wrapper could leave the rolling file handle owned by a worker
                // after CloseAndFlush when another component replaced the process-wide logger.
                // Keep the human-readable stream synchronous; the warning/error audit stream was
                // already synchronous below.
                .WriteTo.File(
                    logFile,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: _options.RetainedFileCountLimit,
                    fileSizeLimitBytes: _options.FileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    outputTemplate: OutputTemplate,
                    buffered: false,
                    shared: true)
                .WriteTo.File(
                    new JsonFormatter(renderMessage: true),
                    auditFile,
                    restrictedToMinimumLevel: LogEventLevel.Warning,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: _options.RetainedFileCountLimit,
                    fileSizeLimitBytes: _options.FileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    buffered: false,
                    shared: true)
                .CreateLogger();

            Log.Logger = _logger;
            _logger.Information("File logging initialized at {LogDirectory}", logDirectory);
            return _logger;
        }
    }

    private static void AppendSelfLogSafely(string path, string message)
    {
        try
        {
            string line = $"{DateTimeOffset.UtcNow:O} {message.TrimEnd()}" + Environment.NewLine;
            File.AppendAllText(path, line, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                             System.Security.SecurityException or NotSupportedException)
        {
            // SelfLog is the final diagnostic fallback and must never recurse or terminate the application.
        }
    }
}
