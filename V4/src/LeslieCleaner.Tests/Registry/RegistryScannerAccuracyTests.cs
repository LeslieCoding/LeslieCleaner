using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Registry;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Tests.Support;
using Microsoft.Win32;

namespace LeslieCleaner.Tests.Registry;

/// <summary>
/// 注册表扫描准确性回归：可修复结果必须指向真实物理配置单元，不能返回 HKCR 合并视图，
/// 否则删除后的回读可能读到另一个后备键并把成功误判为失败。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class RegistryScannerAccuracyTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly string _extension = $".lesliecleaner-{Guid.NewGuid():N}";
    private readonly string _clsid = $"{{{Guid.NewGuid():D}}}";

    public RegistryScannerAccuracyTests()
    {
        using RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using (RegistryKey extension = hive.CreateSubKey($@"SOFTWARE\Classes\{_extension}", writable: true))
        {
            extension.SetValue(null, $"LeslieCleaner.Missing.{Guid.NewGuid():N}", RegistryValueKind.String);
        }

        using RegistryKey inproc = hive.CreateSubKey(
            $@"SOFTWARE\Classes\CLSID\{_clsid}\InprocServer32", writable: true);
        inproc.SetValue(null, $@"C:\DefinitelyMissing\{Guid.NewGuid():N}.dll", RegistryValueKind.String);
    }

    public void Dispose()
    {
        using RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        hive.DeleteSubKeyTree($@"SOFTWARE\Classes\{_extension}", throwOnMissingSubKey: false);
        hive.DeleteSubKeyTree($@"SOFTWARE\Classes\CLSID\{_clsid}", throwOnMissingSubKey: false);
        _workspace.Dispose();
    }

    [Fact]
    public async Task Scan_ClassFindingsUsePhysicalHiveAndRecordRegistryView()
    {
        var scanner = new RegistryScanner(_workspace.Log);

        IReadOnlyList<RegistryFinding> findings = await scanner.ScanAsync();

        RegistryFinding extension = Assert.Single(findings, finding =>
            finding.KeyPath.Equals($@"HKCU\SOFTWARE\Classes\{_extension}", StringComparison.OrdinalIgnoreCase));
        RegistryFinding com = Assert.Single(findings, finding =>
            finding.KeyPath.Equals(
                $@"HKCU\SOFTWARE\Classes\CLSID\{_clsid}\InprocServer32",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(RegistryView.Registry64.ToString(), extension.Evidence.RegistryView);
        Assert.Equal(RegistryView.Registry64.ToString(), com.Evidence.RegistryView);
        Assert.DoesNotContain(findings, finding =>
            finding.KeyPath.StartsWith("HKCR\\", StringComparison.OrdinalIgnoreCase) ||
            finding.KeyPath.StartsWith("HKCR32\\", StringComparison.OrdinalIgnoreCase));

        var duplicateTargets = findings
            .GroupBy(finding => string.Concat(finding.KeyPath.Trim().TrimEnd('\\'), '\u001F',
                    finding.ValueName ?? "<key>"),
                StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .ToArray();
        Assert.Empty(duplicateTargets);
    }

    [Fact]
    public async Task ScanToBackupDeleteReadback_UsesPhysicalTargetThroughEnabledSecurityGate()
    {
        var scanner = new RegistryScanner(_workspace.Log);
        RegistryFinding finding = Assert.Single(
            await scanner.ScanAsync(),
            item => item.KeyPath.Equals(
                $@"HKCU\SOFTWARE\Classes\{_extension}",
                StringComparison.OrdinalIgnoreCase));

        var operationGate = new SecurityOperationGateV4();
        operationGate.Apply(new SecurityAssessment(
            ApplicationIntegrityValid: true,
            DatabaseIntegrityValid: true,
            AuditIntegrityValid: true,
            TrustedPlugins: 0,
            RejectedPlugins: 0,
            Score: 100,
            Summary: "verified test baseline",
            EvaluatedUtc: DateTimeOffset.UtcNow));
        var backupService = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        var engine = new RegistryEngine(backupService, _workspace.Log, operationGate);

        BackupRecord backup = await backupService.BackupRegistryKeyAsync(finding.KeyPath);
        await engine.DeleteKeyAsync(finding.KeyPath, backup.Id);

        Assert.False(await engine.KeyExistsAsync(finding.KeyPath));
        Assert.True(_workspace.Log.HasEvent(LogEventKind.DeleteStart));
        Assert.True(_workspace.Log.HasEvent(LogEventKind.DeleteFinish));
    }

    [Fact]
    public async Task Fingerprint_ChangesWhenPhysicalClassesValueChanges()
    {
        var provider = new WindowsRegistryStateFingerprintProvider();
        string before = await provider.ComputeAsync();

        using (RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
        using (RegistryKey extension = hive.OpenSubKey(
                   $@"SOFTWARE\Classes\{_extension}", writable: true)!)
        {
            extension.SetValue(null, $"LeslieCleaner.Changed.{Guid.NewGuid():N}", RegistryValueKind.String);
        }

        string after = await provider.ComputeAsync();

        Assert.NotEqual(before, after);
    }
}
