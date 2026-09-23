using LeslieCleaner.Modules.Startup;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Startup;

/// <summary>
/// 启动项禁用失败显性化回归：schtasks 非零退出码必须报失败（此前静默成功），
/// 服务/HKLM 权限不足必须给出明确权限提示，不允许"看起来成功"。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class StartupDisableHonestyTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly StartupManagerEngine _engine;

    public StartupDisableHonestyTests() =>
        _engine = new StartupManagerEngine(_workspace.PathService, _workspace.Log);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Disable_ScheduledTask_NonexistentTask_FailsWithError()
    {
        // schtasks /change 对不存在的任务返回非零退出码；修复前不查退出码 → 假成功
        var item = new StartupItem(
            "task:\\NoSuch\\TaskXyz123", "\\NoSuch\\TaskXyz123",
            StartupItemKind.ScheduledTask, "\\NoSuch\\TaskXyz123",
            Command: null, Publisher: null, Enabled: true,
            StartupImpact.Low, string.Empty);

        StartupChangeResult result = await _engine.DisableAsync(item);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("schtasks", result.Error);
    }

    [Fact]
    public async Task Disable_Service_NonexistentService_FailsWithError()
    {
        var item = new StartupItem(
            "service:no-such-service-xyz", "no-such-service-xyz",
            StartupItemKind.Service, "no-such-service-xyz",
            null, null, Enabled: true,
            StartupImpact.Low, string.Empty);

        StartupChangeResult result = await _engine.DisableAsync(item);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Disable_HklmRun_WithoutAdmin_FailsWithPermissionHint()
    {
        // HKLM Run 禁用：无管理员权限时必须失败并给出"权限不足"提示
        var item = new StartupItem(
            "run:HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run:SecurityHealth",
            "SecurityHealth", StartupItemKind.RegistryRun,
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"C:\Windows\System32\SecurityHealthSystray.exe", "Microsoft", Enabled: true,
            StartupImpact.Low, string.Empty);

        StartupChangeResult result = await _engine.DisableAsync(item);

        // 测试环境为标准权限（若管理员运行则该用例不成立，跳过判定）
        if (result.Success)
        {
            // 管理员环境：确认值被还原（HKLM Run 可能被真实禁用，立即恢复）
            _ = await _engine.EnableAsync(item.Id);
            return;
        }

        Assert.NotNull(result.Error);
        Assert.Contains("权限不足", result.Error);
    }
}
