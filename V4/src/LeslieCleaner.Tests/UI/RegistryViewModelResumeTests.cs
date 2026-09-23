using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Tests.Support;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.UI.Services;
using LeslieCleaner.UI.ViewModels;

namespace LeslieCleaner.Tests.UI;

/// <summary>提权恢复只能基于一次成功的新扫描，绝不能复用旧结果。</summary>
public sealed class RegistryViewModelResumeTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public async Task Resume_ScanFailureClearsStaleSelectionAndReportsNoOperation()
    {
        var store = new RegistryElevationResumeStore(_workspace.PathService);
        var target = new RegistryResumeTarget(@"HKLM\Software\Vendor", "OldValue");
        string token = await store.SaveAsync([target]);
        RegistryViewModel viewModel = CreateViewModel(new ThrowingScanner(), store);
        viewModel.Findings.Add(new RegistryFindingRow(Finding(target.KeyPath, target.ValueName)));
        Assert.True(viewModel.Findings[0].IsChecked);

        await viewModel.ResumeAfterElevationAsync(token);

        Assert.False(viewModel.Findings[0].IsChecked);
        Assert.Contains("重新扫描失败", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("未执行任何操作", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resume_SuccessClearsDefaultSelectionAndSelectsOnlyStillPresentTarget()
    {
        var store = new RegistryElevationResumeStore(_workspace.PathService);
        var target = new RegistryResumeTarget(@"HKLM\Software\Vendor", "RetryValue");
        string token = await store.SaveAsync([target]);
        RegistryFinding retry = Finding(target.KeyPath, target.ValueName);
        RegistryFinding unrelated = Finding(@"HKCU\Software\Other", "DefaultSelected");
        RegistryViewModel viewModel = CreateViewModel(new FakeRegistryScanner([retry, unrelated]), store);

        await viewModel.ResumeAfterElevationAsync(token);

        Assert.True(Assert.Single(viewModel.Findings,
            row => row.Finding.ValueName == "RetryValue").IsChecked);
        Assert.False(Assert.Single(viewModel.Findings,
            row => row.Finding.ValueName == "DefaultSelected").IsChecked);
        Assert.Contains("恢复勾选 1/1", viewModel.StatusText, StringComparison.Ordinal);
    }

    public void Dispose() => _workspace.Dispose();

    private RegistryViewModel CreateViewModel(
        IRegistryScanner scanner, RegistryElevationResumeStore store) => new(
        scanner,
        new NoOpCleanEngine(),
        new RegistryBackupService(_workspace.PathService, _workspace.Log),
        new DialogService(),
        _workspace.Log,
        store);

    private static RegistryFinding Finding(string keyPath, string? valueName) => new(
        keyPath, valueName, RegistryIssueKind.InvalidPathValue, RegistryRiskLevel.Safe,
        "test finding", @"C:\missing.exe");

    private sealed class ThrowingScanner : IRegistryScanner
    {
        public Task<IReadOnlyList<RegistryFinding>> ScanAsync(
            IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new UnauthorizedAccessException("simulated registry scan denial");
    }

    private sealed class NoOpCleanEngine : ICleanEngine
    {
        public Task<CleanResult> CleanAsync(
            CleanRequest request, IProgress<CleanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Cleanup must not run during selection recovery.");

        public Task<bool> RollbackAsync(
            string operationId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Rollback must not run during selection recovery.");
    }
}
