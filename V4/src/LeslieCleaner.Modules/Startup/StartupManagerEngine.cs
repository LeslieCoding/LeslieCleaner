using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Startup.Classification;
using Microsoft.Win32;

namespace LeslieCleaner.Modules.Startup;

/// <summary>
/// 启动项管理引擎。覆盖：注册表 Run/RunOnce（HKLM 64/32 位 + HKCU）、Startup 文件夹
/// （用户+公共）、计划任务（schtasks 枚举/禁用/启用）、服务与驱动自启动
/// （HKLM\SYSTEM\CurrentControlSet\Services 的 Start 值，按 Type 区分服务/驱动）。
/// 禁用一律备份式：原值/原文件/原 Start 值先落备份库再移除，可完整恢复，禁止直接删。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class StartupManagerEngine : IStartupEntryOperations
{
    private const string RunSubPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnceSubPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string ServicesSubPath = @"SYSTEM\CurrentControlSet\Services";
    private const int ServiceStartAuto = 2;
    private const int ServiceStartDisabled = 4;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IPathService _pathService;
    private readonly ILogService _logService;
    private readonly ISecurityOperationGate? _securityOperationGate;

    /// <summary>以路径与日志服务创建引擎。</summary>
    /// <param name="pathService">路径服务。</param>
    /// <param name="logService">日志服务。</param>
    /// <param name="securityOperationGate">启动安全检查失败时阻止启动项禁用的运行时闸门。</param>
    public StartupManagerEngine(
        IPathService pathService,
        ILogService logService,
        ISecurityOperationGate? securityOperationGate = null)
    {
        _pathService = pathService;
        _logService = logService;
        _securityOperationGate = securityOperationGate;
        _signatureVerifier = new SignatureVerifier(logService);
    }

    private readonly SignatureVerifier _signatureVerifier;

    /// <summary>
    /// 枚举全部启动项并聚合完整详情（v2）：签名验证、来源分类、影响评估、
    /// AI 建议、SHA256、修改时间、保护级别。全部真实数据，估计值已标注。
    /// </summary>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="includeFileVerification">Whether to verify executable signatures and calculate file hashes.</param>
    public async Task<IReadOnlyList<StartupItemDetail>> EnumerateDetailedAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default,
        bool includeFileVerification = true)
    {
        IReadOnlyList<StartupItem> items = await EnumerateAsync(progress, cancellationToken).ConfigureAwait(false);
        var details = new List<StartupItemDetail>(items.Count);
        int processed = 0;

        foreach (StartupItem item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? filePath = ResolveExecutablePath(item);
            long fileSize = 0;
            DateTimeOffset? modifiedUtc = null;
            string? sha256 = null;
            SignatureInfo signature = new(false, null, SignatureVerificationState.NotChecked);

            if (filePath is not null && File.Exists(filePath))
            {
                var info = new FileInfo(filePath);
                fileSize = info.Length;
                modifiedUtc = new DateTimeOffset(info.LastWriteTimeUtc);
                if (includeFileVerification)
                {
                    signature = await _signatureVerifier.VerifyAsync(filePath, cancellationToken).ConfigureAwait(false);
                    if (fileSize is > 0 and <= 50 * 1024 * 1024)
                    {
                        sha256 = await ComputeSha256Async(filePath, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            StartupClassification classification;
            if (item.Kind == StartupItemKind.ScheduledTask &&
                item.Location.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase))
            {
                // 计划任务命名空间因素：\Microsoft\* 命名空间属微软官方任务
                classification = new StartupClassification(
                    StartupSourceKind.MicrosoftWindows,
                    [$"位于 Microsoft 计划任务命名空间（{item.Location}）"],
                    0.95);
            }
            else
            {
                classification = StartupClassifier.Classify(
                    item.Name,
                    item.Publisher,
                    signature.SignerSubject,
                    signature.IsValid,
                    filePath);
            }
            StartupImpactEstimate impact = StartupImpactAssessor.Assess(
                Path.GetFileName(filePath), fileSize, classification.Kind, signature.IsValid);

            details.Add(new StartupItemDetail(
                item,
                filePath,
                fileSize,
                signature,
                classification,
                impact,
                StartupAdvisor.Advise(item.Name, classification, signature.IsValid),
                sha256,
                InstallDate: null,
                modifiedUtc,
                StartupClassifier.GetProtection(classification.Kind)));

            processed++;
            if ((processed & 0x1F) == 0)
            {
                progress?.Report(new ScanProgress("详情聚合", processed, items.Count, item.Name));
            }
        }

        return details;
    }

    /// <summary>
    /// 从启动项命令行解析可执行文件路径（无命令行或不可解析时返回 <see langword="null"/>）。
    /// </summary>
    /// <param name="item">启动项。</param>
    public static string? ResolveExecutablePath(StartupItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        string? command = item.Command;
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(command.Trim().Trim('"'));
        int exeIndex = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            return expanded[..(exeIndex + 4)];
        }

        return File.Exists(expanded) ? expanded : null;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// 枚举全部启动项（含已禁用记录），带评分与建议。
    /// </summary>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<IReadOnlyList<StartupItem>> EnumerateAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logService.LogEvent(LogEventKind.ScanStart, "启动项枚举开始");
        var items = new List<StartupItem>();

        await Task.Run(() =>
        {
            items.AddRange(EnumerateRegistryRunItems(cancellationToken));
            progress?.Report(new ScanProgress("注册表启动项", 1, 4, null));

            items.AddRange(EnumerateStartupFolderItems(cancellationToken));
            progress?.Report(new ScanProgress("启动文件夹", 2, 4, null));

            items.AddRange(EnumerateScheduledTasks(cancellationToken));
            progress?.Report(new ScanProgress("计划任务", 3, 4, null));

            items.AddRange(EnumerateServicesAndDrivers(cancellationToken));
            progress?.Report(new ScanProgress("服务与驱动", 4, 4, null));
        }, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<DisabledStartupRecord> disabled = await LoadBackupStoreAsync(cancellationToken).ConfigureAwait(false);
        foreach (DisabledStartupRecord record in disabled)
        {
            // 已禁用项以 Enabled=false 呈现（注册表/文件夹类已被移除，无法从系统侧枚举到）
            var restored = new StartupItem(
                record.ItemId, record.Name, record.Kind, record.Location, record.Command,
                record.Publisher, Enabled: false,
                StartupImpactScorer.Score(record.Name, record.Command, record.Publisher, record.Kind),
                string.Empty);
            items.Add(restored with { Suggestion = StartupImpactScorer.BuildSuggestion(restored) });
        }

        _logService.LogEvent(LogEventKind.ScanFinish, "启动项枚举完成",
            new Dictionary<string, object?> { ["Items"] = items.Count });
        return items;
    }

    /// <summary>
    /// 备份式禁用启动项。
    /// </summary>
    /// <param name="item">待禁用项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<StartupChangeResult> DisableAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool backupRecorded = false;
        try
        {
            _securityOperationGate?.EnsureSensitiveOperationAllowed("启动项禁用");
            // 先持久化恢复信息，再改变系统状态；进程中断也不会丢失唯一恢复路径。
            await AddBackupRecordAsync(item, cancellationToken).ConfigureAwait(false);
            backupRecorded = true;
            switch (item.Kind)
            {
                case StartupItemKind.RegistryRun:
                    await DisableRegistryRunAsync(item, cancellationToken).ConfigureAwait(false);
                    break;
                case StartupItemKind.StartupFolder:
                    await DisableStartupFolderAsync(item, cancellationToken).ConfigureAwait(false);
                    break;
                case StartupItemKind.ScheduledTask:
                    await RunSchtasksAsync($"/change /tn \"{item.Location}\" /disable", cancellationToken).ConfigureAwait(false);
                    break;
                case StartupItemKind.Service or StartupItemKind.Driver:
                    await DisableServiceAsync(item, cancellationToken).ConfigureAwait(false);
                    break;
            }

            _logService.LogEvent(LogEventKind.UserAction, "启动项已禁用",
                new Dictionary<string, object?> { ["ItemId"] = item.Id, ["Name"] = item.Name });
            return new StartupChangeResult(item.Id, true, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or Core.Diagnostics.LeslieCleanerException)
        {
            if (backupRecorded)
            {
                try
                {
                    await RemoveBackupRecordAsync(item.Id, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception removeException) when (removeException is IOException or UnauthorizedAccessException)
                {
                    _logService.LogException(removeException, "StartupManagerEngine.RemoveFailedBackup",
                        new Dictionary<string, object?> { ["ItemId"] = item.Id });
                }
            }

            _logService.LogException(ex, "StartupManagerEngine.DisableAsync",
                new Dictionary<string, object?> { ["ItemId"] = item.Id });
            string error = ex is UnauthorizedAccessException or System.Security.SecurityException
                ? $"权限不足（禁用{item.Kind switch
                {
                    StartupItemKind.RegistryRun when item.Location.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) => " HKLM 注册表启动项",
                    StartupItemKind.Service or StartupItemKind.Driver => "服务/驱动",
                    StartupItemKind.ScheduledTask => "系统计划任务",
                    _ => "该项",
                }}需要管理员权限）: {ex.Message}"
                : ex.Message;
            return new StartupChangeResult(item.Id, false, error,
                RequiresElevation: IsElevationFailure(ex));
        }
    }

    /// <summary>
    /// 从备份恢复启用启动项。
    /// </summary>
    /// <param name="itemId">启动项标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<StartupChangeResult> EnableAsync(string itemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        List<DisabledStartupRecord> store = [.. await LoadBackupStoreAsync(cancellationToken).ConfigureAwait(false)];
        DisabledStartupRecord? record = store.FirstOrDefault(r => r.ItemId == itemId);
        if (record is null)
        {
            return new StartupChangeResult(itemId, false, "无禁用备份记录");
        }

        try
        {
            switch (record.Kind)
            {
                case StartupItemKind.RegistryRun:
                    await EnableRegistryRunAsync(record, cancellationToken).ConfigureAwait(false);
                    break;
                case StartupItemKind.StartupFolder:
                    EnableStartupFolder(record);
                    break;
                case StartupItemKind.ScheduledTask:
                    await RunSchtasksAsync($"/change /tn \"{record.Location}\" /enable", cancellationToken).ConfigureAwait(false);
                    break;
                case StartupItemKind.Service or StartupItemKind.Driver:
                    await EnableServiceAsync(record, cancellationToken).ConfigureAwait(false);
                    break;
            }

            store.RemoveAll(r => r.ItemId == itemId);
            await SaveBackupStoreAsync(store, cancellationToken).ConfigureAwait(false);

            _logService.LogEvent(LogEventKind.UserAction, "启动项已恢复启用",
                new Dictionary<string, object?> { ["ItemId"] = itemId });
            return new StartupChangeResult(itemId, true, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or Core.Diagnostics.LeslieCleanerException)
        {
            _logService.LogException(ex, "StartupManagerEngine.EnableAsync",
                new Dictionary<string, object?> { ["ItemId"] = itemId });
            string error = ex is UnauthorizedAccessException or System.Security.SecurityException
                ? $"权限不足（恢复该项需要管理员权限）: {ex.Message}"
                : ex.Message;
            return new StartupChangeResult(itemId, false, error,
                RequiresElevation: IsElevationFailure(ex));
        }
    }

    private IEnumerable<StartupItem> EnumerateRegistryRunItems(CancellationToken cancellationToken)
    {
        (RegistryHive Hive, RegistryView View, string RootName)[] scopes =
        [
            (RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"HKLM\WOW6432Node"),
            (RegistryHive.CurrentUser, RegistryView.Registry64, "HKCU"),
        ];

        foreach ((RegistryHive hive, RegistryView view, string rootName) in scopes)
        {
            foreach (string subPath in new[] { RunSubPath, RunOnceSubPath })
            {
                cancellationToken.ThrowIfCancellationRequested();
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? runKey = baseKey.OpenSubKey(subPath, writable: false);
                if (runKey is null)
                {
                    continue;
                }

                string location = $@"{rootName}\{subPath}";
                foreach (string valueName in runKey.GetValueNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string? command = runKey.GetValue(valueName) as string;
                    string? publisher = TryGetCompany(command);
                    yield return BuildItem(
                        $"run:{location}:{valueName}", valueName, StartupItemKind.RegistryRun,
                        location, command, publisher, enabled: true);
                }
            }
        }
    }

    private IEnumerable<StartupItem> EnumerateStartupFolderItems(CancellationToken cancellationToken)
    {
        (string Directory, string Scope)[] folders =
        [
            (Environment.GetFolderPath(Environment.SpecialFolder.Startup), "用户"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "公共"),
        ];

        foreach ((string directory, string scope) in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = Path.GetFileName(file);
                yield return BuildItem(
                    $"folder:{file}", name, StartupItemKind.StartupFolder, $"{scope}启动文件夹",
                    file, TryGetCompany(file), enabled: true);
            }
        }
    }

    private IEnumerable<StartupItem> EnumerateScheduledTasks(CancellationToken cancellationToken)
    {
        string output;
        try
        {
            output = RunSchtasksForOutput("/query /fo csv /nh", cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or Core.Diagnostics.LeslieCleanerException or System.ComponentModel.Win32Exception)
        {
            _logService.LogException(ex, "StartupManagerEngine.EnumerateScheduledTasks");
            yield break;
        }

        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] fields = ParseCsvLine(line);
            if (fields.Length < 3)
            {
                continue;
            }

            string taskName = fields[0].Trim('"');
            string status = fields[2].Trim('"');
            yield return BuildItem(
                $"task:{taskName}", taskName, StartupItemKind.ScheduledTask, taskName,
                command: null, publisher: null, enabled: !status.Equals("Disabled", StringComparison.OrdinalIgnoreCase));
        }
    }

    private IEnumerable<StartupItem> EnumerateServicesAndDrivers(CancellationToken cancellationToken)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? services = baseKey.OpenSubKey(ServicesSubPath, writable: false);
        if (services is null)
        {
            yield break;
        }

        foreach (string serviceName in services.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartupItem? item = null;
            try
            {
                using RegistryKey? service = services.OpenSubKey(serviceName, writable: false);
                if (service is not null)
                {
                    int? start = service.GetValue("Start") as int?;
                    if (start == ServiceStartAuto)
                    {
                        int type = service.GetValue("Type") as int? ?? 0;
                        bool isDriver = type is 1 or 2 or 8;
                        string displayName = service.GetValue("DisplayName") as string ?? serviceName;
                        string? imagePath = service.GetValue("ImagePath") as string;
                        StartupItemKind kind = isDriver ? StartupItemKind.Driver : StartupItemKind.Service;
                        item = BuildItem(
                            $"{(isDriver ? "driver" : "service")}:{serviceName}", displayName, kind,
                            serviceName, imagePath, TryGetCompany(imagePath), enabled: true);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                _logService.LogException(ex, "StartupManagerEngine.EnumerateServices",
                    new Dictionary<string, object?> { ["Service"] = serviceName });
            }

            if (item is not null)
            {
                yield return item;
            }
        }
    }

    private async Task DisableRegistryRunAsync(StartupItem item, CancellationToken cancellationToken)
    {
        (RegistryKey hive, string subPath, string valueName) = ParseRunLocation(item.Location, item.Id);
        using (hive)
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using RegistryKey? key = hive.OpenSubKey(subPath, writable: true)
                ?? throw new Core.Diagnostics.LeslieCleanerException($"注册表启动项位置不存在: {item.Location}");
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnableRegistryRunAsync(DisabledStartupRecord record, CancellationToken cancellationToken)
    {
        (RegistryKey hive, string subPath, string valueName) = ParseRunLocation(record.Location, record.ItemId);
        using (hive)
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using RegistryKey key = hive.CreateSubKey(subPath, writable: true);
                key.SetValue(valueName, record.Command ?? string.Empty,
                    record.OriginalRegistryValueKind ?? RegistryValueKind.String);
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task DisableStartupFolderAsync(StartupItem item, CancellationToken cancellationToken)
    {
        string sourcePath = item.Command ?? throw new Core.Diagnostics.LeslieCleanerException($"启动文件夹项缺少文件路径: {item.Id}");
        string backupDirectory = GetStartupFolderBackupDirectory();
        Directory.CreateDirectory(backupDirectory);
        string destination = GetStartupFolderBackupPath(item.Id, sourcePath);
        File.Move(sourcePath, destination, overwrite: true);
        return Task.CompletedTask;
    }

    private void EnableStartupFolder(DisabledStartupRecord record)
    {
        string backupFile = GetStartupFolderBackupPath(record.ItemId, record.Command ?? string.Empty);
        if (!File.Exists(backupFile))
        {
            throw new Core.Diagnostics.LeslieCleanerException($"启动文件夹备份缺失: {backupFile}");
        }

        File.Move(backupFile, record.Command!, overwrite: false);
    }

    private Task DisableServiceAsync(StartupItem item, CancellationToken cancellationToken) =>
        SetServiceStartValueAsync(item.Location, ServiceStartDisabled, cancellationToken);

    private async Task EnableServiceAsync(DisabledStartupRecord record, CancellationToken cancellationToken)
    {
        int originalStart = record.OriginalStartValue ?? ServiceStartAuto;
        await SetServiceStartValueAsync(record.Location, originalStart, cancellationToken).ConfigureAwait(false);
    }

    private async Task SetServiceStartValueAsync(string serviceName, int startValue, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? service = baseKey.OpenSubKey($@"{ServicesSubPath}\{serviceName}", writable: true)
                ?? throw new Core.Diagnostics.LeslieCleanerException($"服务不存在: {serviceName}");
            service.SetValue("Start", startValue, RegistryValueKind.DWord);
        }, cancellationToken).ConfigureAwait(false);
    }

    private StartupItem BuildItem(
        string id, string name, StartupItemKind kind, string location,
        string? command, string? publisher, bool enabled)
    {
        StartupImpact impact = StartupImpactScorer.Score(name, command, publisher, kind);
        var item = new StartupItem(id, name, kind, location, command, publisher, enabled, impact, string.Empty);
        return item with { Suggestion = StartupImpactScorer.BuildSuggestion(item) };
    }

    private static (RegistryKey Hive, string SubPath, string ValueName) ParseRunLocation(string location, string itemId)
    {
        string valueName = itemId[(itemId.LastIndexOf(':') + 1)..];
        if (location.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
        {
            return (RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64),
                location["HKCU\\".Length..], valueName);
        }

        if (location.StartsWith(@"HKLM\WOW6432Node\", StringComparison.OrdinalIgnoreCase))
        {
            return (RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32),
                location[@"HKLM\WOW6432Node\".Length..], valueName);
        }

        if (location.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase))
        {
            return (RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
                location["HKLM\\".Length..], valueName);
        }

        throw new Core.Diagnostics.LeslieCleanerException($"无法解析启动项位置: {location}");
    }

    private static string[] ParseCsvLine(string line)
    {
        // schtasks CSV：字段以引号包围、逗号分隔
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }

    private static string RunSchtasksForOutput(string arguments, CancellationToken cancellationToken)
    {
        string schtasks = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = schtasks,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.Default,
            StandardErrorEncoding = System.Text.Encoding.Default,
        }) ?? throw new Core.Diagnostics.LeslieCleanerException("无法启动 schtasks.exe");

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit((int)TimeSpan.FromSeconds(60).TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new Core.Diagnostics.LeslieCleanerException($"schtasks.exe 超时: {arguments}");
        }

        // 失败必须显性化：schtasks 出错（如权限不足）时退出码非零且报错走 stderr，
        // 此前不查退出码导致"显示成功但真实世界没变"（静默失败）
        if (process.ExitCode != 0)
        {
            string detail = string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim();
            throw new Core.Diagnostics.LeslieCleanerException(
                $"schtasks 执行失败（退出码 {process.ExitCode}）: {detail}");
        }

        return output;
    }

    private async Task RunSchtasksAsync(string arguments, CancellationToken cancellationToken)
    {
        string output = await Task.Run(() => RunSchtasksForOutput(arguments, cancellationToken), cancellationToken).ConfigureAwait(false);
        _logService.LogEvent(LogEventKind.UserAction, "schtasks 执行", new Dictionary<string, object?>
        {
            ["Arguments"] = arguments,
            ["Output"] = output.Trim(),
        });
    }

    private string GetStartupFolderBackupDirectory() =>
        Path.Combine(_pathService.BackupDirectory, "StartupFolder");

    private string GetStartupFolderBackupPath(string itemId, string sourcePath)
    {
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(itemId)))[..16];
        return Path.Combine(GetStartupFolderBackupDirectory(), $"{hash}-{Path.GetFileName(sourcePath)}");
    }

    private string GetBackupStorePath() =>
        Path.Combine(_pathService.BackupDirectory, "Startup", "disabled-items.json");

    private async Task<IReadOnlyList<DisabledStartupRecord>> LoadBackupStoreAsync(CancellationToken cancellationToken)
    {
        string path = GetBackupStorePath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync<List<DisabledStartupRecord>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logService.LogException(ex, "StartupManagerEngine.LoadBackupStore");
            return [];
        }
    }

    private async Task AddBackupRecordAsync(StartupItem item, CancellationToken cancellationToken)
    {
        List<DisabledStartupRecord> store = [.. await LoadBackupStoreAsync(cancellationToken).ConfigureAwait(false)];
        store.RemoveAll(r => r.ItemId == item.Id);
        (string? originalCommand, RegistryValueKind? originalRegistryValueKind) = GetRegistryValueSnapshot(item);
        store.Add(new DisabledStartupRecord(
            item.Id, item.Name, item.Kind, item.Location, originalCommand ?? item.Command, item.Publisher,
            item.Kind is StartupItemKind.Service or StartupItemKind.Driver ? ServiceStartAuto : null,
            DateTimeOffset.UtcNow,
            originalRegistryValueKind));
        await SaveBackupStoreAsync(store, cancellationToken).ConfigureAwait(false);
    }

    private static (string? Command, RegistryValueKind? Kind) GetRegistryValueSnapshot(StartupItem item)
    {
        if (item.Kind != StartupItemKind.RegistryRun)
        {
            return (null, null);
        }

        try
        {
            (RegistryKey hive, string subPath, string valueName) = ParseRunLocation(item.Location, item.Id);
            using (hive)
            using (RegistryKey? key = hive.OpenSubKey(subPath, writable: false))
            {
                return key is null
                    ? (null, null)
                    : (key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string,
                        key.GetValueKind(valueName));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (null, null);
        }
    }

    private async Task RemoveBackupRecordAsync(string itemId, CancellationToken cancellationToken)
    {
        List<DisabledStartupRecord> store = [.. await LoadBackupStoreAsync(cancellationToken).ConfigureAwait(false)];
        if (store.RemoveAll(record => record.ItemId == itemId) > 0)
        {
            await SaveBackupStoreAsync(store, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SaveBackupStoreAsync(List<DisabledStartupRecord> store, CancellationToken cancellationToken)
    {
        string path = GetBackupStorePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsElevationFailure(Exception exception) =>
        exception is UnauthorizedAccessException or System.Security.SecurityException ||
        exception.Message.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("access is denied", StringComparison.OrdinalIgnoreCase);

    private static string? TryGetCompany(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        try
        {
            string path = command.Trim('"');
            int exeIndex = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex < 0)
            {
                return null;
            }

            path = path[..(exeIndex + 4)];
            if (!File.Exists(path))
            {
                return null;
            }

            string? company = FileVersionInfo.GetVersionInfo(path).CompanyName;
            return string.IsNullOrWhiteSpace(company) ? null : company;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private sealed record DisabledStartupRecord(
        string ItemId,
        string Name,
        StartupItemKind Kind,
        string Location,
        string? Command,
        string? Publisher,
        int? OriginalStartValue,
        DateTimeOffset DisabledUtc,
        RegistryValueKind? OriginalRegistryValueKind = null);
}
