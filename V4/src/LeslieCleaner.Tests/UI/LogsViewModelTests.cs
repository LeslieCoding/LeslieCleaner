using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Diagnostics;
using LeslieCleaner.Tests.Support;
using LeslieCleaner.UI.ViewModels;

namespace LeslieCleaner.Tests.UI;

/// <summary>运行日志页必须同时公开普通日志与同步审计日志。</summary>
public sealed class LogsViewModelTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public async Task LoadFiles_IncludesTextAndJsonLineAuditFiles()
    {
        Directory.CreateDirectory(_workspace.PathService.LogDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(_workspace.PathService.LogDirectory, "lesliecleaner-test.log"),
            "2026-09-01 [INF] started");
        await File.WriteAllTextAsync(
            Path.Combine(_workspace.PathService.LogDirectory, "audit-test.jsonl"),
            "{\"Level\":\"Warning\",\"RenderedMessage\":\"verified delete\"}");
        await File.WriteAllTextAsync(
            Path.Combine(_workspace.PathService.LogDirectory, "ignore.tmp"),
            "not a log");
        using var config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        var diagnostics = new DiagnosticsService(_workspace.PathService, config, _workspace.Log);
        var viewModel = new LogsViewModel(_workspace.PathService, diagnostics, _workspace.Log);

        await viewModel.LoadFilesCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Files.Count);
        Assert.Contains(viewModel.Files, file => file.Name.EndsWith(".log", StringComparison.Ordinal));
        Assert.Contains(viewModel.Files, file => file.Name.EndsWith(".jsonl", StringComparison.Ordinal));
        Assert.DoesNotContain(viewModel.Files, file => file.Name.EndsWith(".tmp", StringComparison.Ordinal));
    }

    public void Dispose() => _workspace.Dispose();
}
