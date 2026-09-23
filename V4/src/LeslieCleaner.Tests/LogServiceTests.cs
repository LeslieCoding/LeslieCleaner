using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Logging;
using LeslieCleaner.Tests.Support;
using Serilog;
using System.Text.Json;

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

        string content = await ReadSharedTextAsync(logFile);
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
        Assert.Same(logger, bootstrapper.Bootstrap());
        Log.CloseAndFlush();
    }

    [Fact]
    public async Task Bootstrapper_PersistsStructuredOperationProperties()
    {
        var bootstrapper = new SerilogLoggingBootstrapper(_workspace.PathService);
        Serilog.ILogger logger = bootstrapper.Bootstrap();
        var service = new SerilogLogService(logger);

        service.LogEvent(LogEventKind.DeleteStart, "registry delete", new Dictionary<string, object?>
        {
            ["KeyPath"] = @"HKCU\Software\LeslieCleaner.Tests",
            ["RegistryView"] = "Registry64",
            ["Elevated"] = false,
        });
        Log.CloseAndFlush();

        string logFile = Assert.Single(Directory.EnumerateFiles(
            _workspace.PathService.LogDirectory, "lesliecleaner-*.log"));
        string content = await File.ReadAllTextAsync(logFile);
        Assert.Contains("registry delete", content);
        Assert.Contains("KeyPath", content);
        Assert.Contains("HKCU", content);
        Assert.Contains("LeslieCleaner.Tests", content);
        Assert.Contains("Registry64", content);
    }

    private static async Task<string> ReadSharedTextAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task Bootstrapper_PersistsDestructiveAuditSynchronouslyAsJsonLines()
    {
        var bootstrapper = new SerilogLoggingBootstrapper(_workspace.PathService);
        Serilog.ILogger logger = bootstrapper.Bootstrap();
        var service = new SerilogLogService(logger);

        service.LogEvent(LogEventKind.DeleteFinish, "verified registry delete", new Dictionary<string, object?>
        {
            ["KeyPath"] = @"HKCU\Software\LeslieCleaner.Tests\Deleted",
            ["BackupId"] = Guid.Parse("7bf1a4da-5f67-4d98-8e67-ccb07cb00ff8"),
            ["ReadBackVerified"] = true,
        });

        string auditFile = Assert.Single(Directory.EnumerateFiles(
            _workspace.PathService.LogDirectory, "audit-*.jsonl"));
        await using var stream = new FileStream(
            auditFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        string line = Assert.Single((await reader.ReadToEndAsync())
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement properties = document.RootElement.GetProperty("Properties");
        Assert.Equal("DeleteFinish", properties.GetProperty("EventKind").GetString());
        Assert.True(properties.GetProperty("ReadBackVerified").GetBoolean());
        Assert.Contains("verified registry delete", document.RootElement.GetProperty("RenderedMessage").GetString());

        Log.CloseAndFlush();
    }

    [Fact]
    public async Task LogEvent_ReservedPropertiesCannotSpoofEventKind()
    {
        string logFile = Path.Combine(_workspace.PathService.LogDirectory, "reserved-.log");
        Directory.CreateDirectory(_workspace.PathService.LogDirectory);
        Serilog.ILogger logger = new LoggerConfiguration()
            .WriteTo.File(logFile, outputTemplate: "[{EventKind}] [{DataEventKind}] {Message:lj}")
            .CreateLogger();
        var service = new SerilogLogService(logger);

        service.LogEvent(LogEventKind.DeleteStart, "delete", new Dictionary<string, object?>
        {
            ["EventKind"] = "FakeSuccess",
        });
        ((IDisposable)logger).Dispose();

        string content = await File.ReadAllTextAsync(logFile);
        Assert.Contains("[DeleteStart] [FakeSuccess] delete", content);
    }
}
