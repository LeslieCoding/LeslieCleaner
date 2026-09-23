using LeslieCleaner.Infrastructure.Logging;
using LeslieCleaner.Tests.Support;
using Serilog;

namespace LeslieCleaner.Tests.Diagnostics;

/// <summary>全局异常处理验证：未处理/未观察异常真实写入 Serilog 文件。</summary>
public sealed class GlobalExceptionTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Handle_WritesExceptionWithFullContext()
    {
        string logFile = Path.Combine(_workspace.PathService.LogDirectory, "glo-.log");
        Directory.CreateDirectory(_workspace.PathService.LogDirectory);
        Serilog.ILogger logger = new LoggerConfiguration()
            .WriteTo.File(logFile, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{EventKind}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        var service = new SerilogLogService(logger);
        GlobalExceptionHandler.Handle(new InvalidOperationException("故意触发的验证异常"), "测试上下文", service);
        ((IDisposable)logger).Dispose();

        string content = File.ReadAllText(logFile);
        Assert.Contains("[ERR]", content);
        Assert.Contains("[Exception]", content);
        Assert.Contains("测试上下文", content);
        Assert.Contains("故意触发的验证异常", content);
        Assert.Contains(nameof(InvalidOperationException), content);
        // 格式：时间戳/级别/事件种类齐备
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} \[ERR\] \[Exception\]", content);
    }

    [Fact]
    public async Task Attach_UnobservedTaskException_IsLoggedToFile()
    {
        string logFile = Path.Combine(_workspace.PathService.LogDirectory, "unobs-.log");
        Directory.CreateDirectory(_workspace.PathService.LogDirectory);
        Serilog.ILogger logger = new LoggerConfiguration()
            .WriteTo.File(logFile, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{EventKind}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        var service = new SerilogLogService(logger);
        GlobalExceptionHandler.Attach(service);

        // 制造未观察任务异常并触发终结器
        _ = Task.Run(() => throw new InvalidOperationException("未观察任务异常验证"));
        await Task.Delay(200);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        await Task.Delay(300);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        ((IDisposable)logger).Dispose();

        string content = File.ReadAllText(logFile);
        Assert.Contains("TaskScheduler.UnobservedTaskException", content);
        Assert.Contains("未观察任务异常验证", content);
    }

    [Fact]
    public async Task Observe_FaultedFireAndForget_IsLogged()
    {
        var service = _workspace.Log;
        GlobalExceptionHandler.Observe(
            Task.FromException(new InvalidOperationException("即发即弃异常")),
            "测试观察", service);

        // ContinueWith 在线程池异步执行：轮询等待记录落地
        for (int i = 0; i < 40 && !_workspace.Log.Exceptions.Any(e => e.Message.Contains("即发即弃异常")); i++)
        {
            await Task.Delay(50);
        }

        Assert.Contains(_workspace.Log.Exceptions, e => e.Message.Contains("即发即弃异常"));
    }
}
