using LeslieCleaner.Core.Abstractions;
using Serilog;
using Serilog.Events;

namespace LeslieCleaner.Infrastructure.Logging;

/// <summary>
/// Serilog 集中配置引导。按天滚动文件 + 异步 Sink，
/// 输出目录 %LocalAppData%\LeslieCleaner\Logs，保留 31 天。
/// </summary>
public sealed class SerilogLoggingBootstrapper : ILoggingBootstrapper
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{EventKind}] {Message:lj}{NewLine}{Exception}";

    private readonly IPathService _pathService;

    /// <summary>以路径服务创建引导器。</summary>
    /// <param name="pathService">路径服务。</param>
    public SerilogLoggingBootstrapper(IPathService pathService) => _pathService = pathService;

    /// <inheritdoc />
    public Serilog.ILogger Bootstrap()
    {
        string logDirectory = _pathService.EnsureDirectory(_pathService.LogDirectory);
        string logFile = Path.Combine(logDirectory, "lesliecleaner-.log");

        Serilog.ILogger logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Async(a => a.File(
                logFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate: OutputTemplate,
                buffered: false))
            .CreateLogger();

        Log.Logger = logger;
        return logger;
    }
}
