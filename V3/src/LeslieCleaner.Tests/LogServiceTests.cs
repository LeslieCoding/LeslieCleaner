using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Logging;
using LeslieCleaner.Tests.Support;
using Serilog;

namespace LeslieCleaner.Tests;

public sealed class LogServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task LogEvent_WritesEventKindToFile()
    {
        string logFile = Path.Combine(_workspace.PathService.LogDirectory, "test-.log");
        Directory.CreateDirectory(_workspace.PathService.LogDirectory);
        Serilog.ILogger logger = new LoggerConfiguration()
            .WriteTo.File(logFile, outputTemplate: "[{EventKind}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        var service = new SerilogLogService(logger);

        service.LogEvent(LogEventKind.ScanStart, "扫描开始", new Dictionary<string, object?> { ["Module"] = "Cleaner" });
        ((IDisposable)logger).Dispose();

        string content = await File.ReadAllTextAsync(logFile);
        Assert.Contains("[ScanStart]", content);
        Assert.Contains("扫描开始", content);
    }

    [Fact]
    public async Task LogException_WritesExceptionDetails()
    {
        string logFile = Path.Combine(_workspace.PathService.LogDirectory, "ex-.log");
        Directory.CreateDirectory(_workspace.PathService.LogDirectory);
        Serilog.ILogger logger = new LoggerConfiguration()
            .WriteTo.File(logFile)
            .CreateLogger();
        var service = new SerilogLogService(logger);

        service.LogException(new InvalidOperationException("boom"), "测试上下文");
        ((IDisposable)logger).Dispose();

        string content = await File.ReadAllTextAsync(logFile);
        Assert.Contains("boom", content);
        Assert.Contains("测试上下文", content);
        Assert.Contains(nameof(InvalidOperationException), content);
    }

    [Fact]
    public void Bootstrapper_CreatesLogDirectory()
    {
        var pathService = _workspace.PathService;
        var bootstrapper = new SerilogLoggingBootstrapper(pathService);
        string expectedLogDir = pathService.LogDirectory;

        Serilog.ILogger logger = bootstrapper.Bootstrap();
        logger.Information("引导测试");

        Assert.True(Directory.Exists(expectedLogDir));
        Assert.Same(logger, Log.Logger);
        Log.CloseAndFlush();
    }
}
