using System.IO;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.AI;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>AI、插件、更新与安全状态统一平台中心；所有外部工作均可取消并呈现失败恢复。</summary>
public sealed partial class PlatformCenterViewModel : ObservableObject, ICancelablePageActivatable
{
    private readonly IPluginManager _pluginManager;
    private readonly IUpdateManager _updateManager;
    private readonly ISecurityManager _securityManager;
    private readonly ISecurityManagerV4 _securityManagerV4;
    private readonly IEmergencySecurityGate _emergencyGate;
    private readonly IIntegrityRepairService _integrityRepairService;
    private readonly IEncryptionService _encryptionService;
    private readonly IPathService _pathService;
    private readonly AiAssistantService _aiAssistant;
    private readonly SystemContextSnapshotProvider _contextProvider;
    private readonly DialogService _dialogService;
    private CancellationTokenSource? _aiCts;

    [ObservableProperty] private string _pluginStatus = "尚未检查插件目录。";
    [ObservableProperty] private string _updateStatus = "尚未检查更新。";
    [ObservableProperty] private string _securityStatus = "正在等待首次安全评估。";
    [ObservableProperty] private string _integrityStatus = "完整性清单尚未验证。新安装必须由用户明确建立可信基线，绝不自动信任当前文件。";
    [ObservableProperty] private string _aiStatus = "离线规则助手可用；不会读取文件内容或自动执行操作。";
    [ObservableProperty] private string _question = "为什么我的电脑最近变慢？";
    [ObservableProperty] private string _aiAnswer = "输入问题后选择“分析问题”。任何系统更改仍需您确认。";
    [ObservableProperty] private string _aiSafety = "建议模式：仅解释和推荐，不执行清理、注册表或启动项修改。";
    [ObservableProperty] private string? _lastError;
    [ObservableProperty] private bool _isAiBusy;
    [ObservableProperty] private bool _isPluginBusy;
    [ObservableProperty] private bool _isUpdateBusy;
    [ObservableProperty] private bool _isSecurityBusy;
    [ObservableProperty] private bool _isIntegrityBusy;
    [ObservableProperty] private int _securityScore;
    [ObservableProperty] private bool _isEmergencyReadOnly = true;
    [ObservableProperty] private DateTimeOffset? _lastUpdateCheck;

    /// <summary>已发现并通过描述解析的插件。</summary>
    public ObservableCollection<PluginDescriptor> Plugins { get; } = [];

    /// <summary>创建统一平台中心并绑定真实平台服务。</summary>
    public PlatformCenterViewModel(
        IPluginManager pluginManager,
        IUpdateManager updateManager,
        ISecurityManager securityManager,
        ISecurityManagerV4 securityManagerV4,
        IEmergencySecurityGate emergencyGate,
        IIntegrityRepairService integrityRepairService,
        IEncryptionService encryptionService,
        IPathService pathService,
        AiAssistantService aiAssistant,
        SystemContextSnapshotProvider contextProvider,
        DialogService dialogService)
    {
        _pluginManager = pluginManager;
        _updateManager = updateManager;
        _securityManager = securityManager;
        _securityManagerV4 = securityManagerV4;
        _emergencyGate = emergencyGate;
        _integrityRepairService = integrityRepairService;
        _encryptionService = encryptionService;
        _pathService = pathService;
        _aiAssistant = aiAssistant;
        _contextProvider = contextProvider;
        _dialogService = dialogService;
    }

    /// <inheritdoc />
    public Task OnActivatedAsync() => OnActivatedAsync(CancellationToken.None);

    /// <inheritdoc />
    public async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        LastError = null;
        await Task.WhenAll(
            RefreshPluginsAsync(cancellationToken),
            RefreshSecurityAsync(cancellationToken),
            RefreshIntegrityAsync(cancellationToken));
    }

    [RelayCommand]
    private async Task AskAiAsync()
    {
        if (string.IsNullOrWhiteSpace(Question))
        {
            LastError = "请先输入要分析的问题。";
            return;
        }

        _aiCts?.Cancel();
        _aiCts?.Dispose();
        _aiCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        IsAiBusy = true;
        LastError = null;
        AiStatus = "正在采集匿名 CPU、内存与磁盘汇总并分析…";
        try
        {
            AiAggregateContext context = await _contextProvider.CaptureAsync(_aiCts.Token);
            AiAssistantResponse response = await _aiAssistant.AskAsync(
                new AiQuestion(Question.Trim(), context), _aiCts.Token);
            AiAnswer = response.Answer;
            AiSafety = $"风险说明：{response.Safety.RiskExplanation}  备份建议：{response.Safety.BackupSuggestion}  确认要求：{response.Safety.ConfirmationRequirement}";
            AiStatus = response.UsedOfflineFallback
                ? "在线服务不可用，已安全降级到本机离线规则。"
                : $"由 {response.Provider} 完成，用时 {response.Duration.TotalMilliseconds:F0}ms。";
        }
        catch (OperationCanceledException)
        {
            AiStatus = "分析已取消；没有执行任何系统更改。";
        }
        catch (Exception ex)
        {
            LastError = $"AI 分析失败：{ex.Message}";
            AiStatus = "分析失败，可检查设置后重试；系统未被修改。";
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    [RelayCommand]
    private void CancelAi() => _aiCts?.Cancel();

    [RelayCommand]
    private async Task RefreshPluginsAsync(CancellationToken cancellationToken = default)
    {
        IsPluginBusy = true;
        LastError = null;
        try
        {
            IReadOnlyList<PluginDescriptor> plugins = await _pluginManager.DiscoverAsync(cancellationToken);
            Plugins.Clear();
            foreach (PluginDescriptor plugin in plugins) Plugins.Add(plugin);
            int ready = plugins.Count(static p => p.TrustStatus == PluginTrustStatus.Ready);
            int disabled = plugins.Count(static p => p.TrustStatus == PluginTrustStatus.Disabled);
            int rejected = plugins.Count - ready - disabled;
            PluginStatus = $"共 {plugins.Count} 个：启用 {ready}，停用 {disabled}，因安全校验拒绝 {rejected}。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            PluginStatus = "插件检查已取消。";
        }
        catch (Exception ex)
        {
            LastError = $"插件检查失败：{ex.Message}";
            PluginStatus = "无法读取插件状态，请重试。";
        }
        finally { IsPluginBusy = false; }
    }

    [RelayCommand]
    private async Task TogglePluginAsync(PluginDescriptor? plugin)
    {
        if (plugin is null || plugin.TrustStatus is not (PluginTrustStatus.Ready or PluginTrustStatus.Disabled))
        {
            LastError = "该插件未通过完整性或权限校验，不能启用。";
            return;
        }
        bool enabling = plugin.TrustStatus == PluginTrustStatus.Disabled;
        if (enabling)
        {
            string permissions = plugin.Permissions is { Count: > 0 } ? string.Join("、", plugin.Permissions) : "无额外权限";
            bool confirmed = await _dialogService.ConfirmAsync(
                "确认启用插件",
                $"插件：{plugin.Name}\n发布者：{plugin.Author}\n申请权限：{permissions}\n\n插件将在隔离进程运行。仅在信任发布者和权限用途时继续。",
                "确认启用");
            if (!confirmed)
            {
                PluginStatus = "已取消启用，插件状态未改变。";
                return;
            }
        }
        try
        {
            await _pluginManager.SetEnabledAsync(plugin.Id, enabling);
            await RefreshPluginsAsync();
        }
        catch (Exception ex) { LastError = $"插件状态修改失败：{ex.Message}"; }
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync(CancellationToken cancellationToken = default)
    {
        IsUpdateBusy = true;
        LastError = null;
        try
        {
            UpdateStatus status = await _updateManager.CheckAsync(cancellationToken);
            LastUpdateCheck = status.CheckedUtc;
            UpdateStatus = status.AvailableVersion is null
                ? $"当前版本 {status.CurrentVersion} 已是最新。{status.Message}"
                : $"当前 {status.CurrentVersion}，可更新到 {status.AvailableVersion}；需下载 {status.FilesToDownload} 个文件。{status.Message}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            UpdateStatus = "更新检查已取消。";
        }
        catch (Exception ex)
        {
            LastError = $"更新检查失败：{ex.Message}";
            UpdateStatus = "无法连接更新源；可稍后重试，不影响当前版本使用。";
        }
        finally { IsUpdateBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshSecurityAsync(CancellationToken cancellationToken = default)
    {
        IsSecurityBusy = true;
        LastError = null;
        try
        {
            SecurityAssessment assessment = await _securityManager.AssessAsync(cancellationToken);
            ApplyAssessment(assessment);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SecurityStatus = "安全评估已取消。";
        }
        catch (Exception ex)
        {
            IsEmergencyReadOnly = true;
            LastError = $"安全评估失败：{ex.Message}";
            SecurityStatus = "无法确认完整性，界面按只读保护状态展示；请重试评估。";
        }
        finally { IsSecurityBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshIntegrityAsync(CancellationToken cancellationToken = default)
    {
        IsIntegrityBusy = true;
        try
        {
            StartupIntegrityReport report = await _integrityRepairService.VerifyStartupAsync(cancellationToken);
            IntegrityStatus = report.ManifestTrusted
                ? report.IsValid
                    ? $"可信基线有效，五类启动目标共 {report.Records.Count} 项全部通过。"
                    : $"可信基线有效但发现失败：高风险 {report.HighRiskFailureCount}，低风险 {report.LowRiskFailureCount}。应优先从可信备份修复，不要直接重建。"
                : "未找到可信或可验证的基线。应用保持只读；仅在确认当前文件来自可信安装包后，由用户明确建立基线。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException)
        {
            IntegrityStatus = $"完整性清单不可用：{ex.Message}。应用保持只读。";
        }
        finally { IsIntegrityBusy = false; }
    }

    [RelayCommand]
    private async Task EstablishTrustedBaselineAsync()
    {
        bool confirmed = await _dialogService.ConfirmAsync(
            "建立或重建可信基线",
            "这会把当前已发布目录中的五类目标设为可信：主程序、核心库、运行配置、数据库组件、插件元数据。\n\n只有在这些文件刚由可信安装包安装或已核验 SHA-256/签名时才能继续。若当前文件可能被篡改，请取消并先重新安装。此操作不会静默执行，也不会信任用户文档。",
            "我已核验来源，继续");
        if (!confirmed)
        {
            IntegrityStatus = "已取消建立基线；只读保护保持不变。";
            return;
        }
        bool finalConfirmed = await _dialogService.ConfirmAsync(
            "最终确认",
            "重建会替换现有完整性信任记录，但不会删除可信备份之外的文件。确认当前五类文件来自可信来源并继续？",
            "明确建立可信基线");
        if (!finalConfirmed)
        {
            IntegrityStatus = "已取消建立基线；没有改变信任状态。";
            return;
        }

        IsIntegrityBusy = true;
        LastError = null;
        try
        {
            string applicationRoot = Path.GetFullPath(AppContext.BaseDirectory);
            IReadOnlyList<TrustedIntegrityTarget> targets = IntegrityTargetPlanner.Build(applicationRoot);
            string backupRoot = _pathService.EnsureDirectory(Path.Combine(_pathService.BackupDirectory, "SecurityIntegrity"));
            var request = new TrustedBaselineRequest(applicationRoot, backupRoot, targets,
                ExplicitlyAuthorized: true,
                AuthorizationReason: "User explicitly confirmed trusted installer/source in Platform Center.",
                ReplaceExisting: true);
            TrustedBaselineResult result = await _integrityRepairService.EstablishTrustedBaselineAsync(request);
            IntegrityStatus = result.Succeeded
                ? $"可信基线已密封：保护 {result.ProtectedFileCount} 个文件。正在重新验证并评估只读状态。"
                : $"可信基线未建立：{result.Message}";
            if (result.Succeeded)
            {
                await RefreshIntegrityAsync();
                SecurityAssessment assessment = await _securityManager.AssessAsync();
                ApplyAssessment(assessment);
                if (_emergencyGate.EmergencyState.IsReadOnly)
                {
                    IntegrityStatus += $" 仍处于只读：{_emergencyGate.EmergencyState.Reason}";
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException or InvalidOperationException)
        {
            LastError = $"可信基线建立失败：{ex.Message}";
            IntegrityStatus = "基线未改变，应用继续只读。请从可信安装包重新安装后再试。";
        }
        finally { IsIntegrityBusy = false; }
    }

    [RelayCommand]
    private async Task RepairFromTrustedBackupAsync()
    {
        bool confirmed = await _dialogService.ConfirmAsync(
            "从可信备份修复",
            "将只恢复完整性校验失败的受保护文件，并在恢复后重新校验。数据库、审计链、权限策略、更新信任、插件安全和加密自检也必须全部通过，系统才会退出只读模式。是否继续？",
            "开始可信修复");
        if (!confirmed) return;

        IsIntegrityBusy = true;
        LastError = null;
        try
        {
            SecurityAssessment assessment = await _securityManager.AssessAsync();
            IntegrityRecoveryPrerequisites prerequisites = await BuildRecoveryPrerequisitesAsync(assessment);
            IntegrityRepairResult result = await _integrityRepairService.RepairAsync(prerequisites,
                includeLowRiskConfiguration: true);
            IntegrityStatus = result.Succeeded
                ? $"修复并复核完成：恢复 {result.RestoredFileCount} 项，已退出只读模式。"
                : $"修复未完全通过：恢复 {result.RestoredFileCount} 项，剩余高风险 {result.Verification.HighRiskFailureCount}、低风险 {result.Verification.LowRiskFailureCount}。{string.Join("；", result.Errors)}";
            ApplyAssessment(await _securityManager.AssessAsync());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException)
        {
            LastError = $"可信修复失败：{ex.Message}";
            IntegrityStatus = "修复失败，未解除只读保护。请核对可信备份和每项恢复前置条件后重试。";
        }
        finally { IsIntegrityBusy = false; }
    }

    private async Task<IntegrityRecoveryPrerequisites> BuildRecoveryPrerequisitesAsync(SecurityAssessment assessment)
    {
        SecurityPermissionDecision permissionProbe = await _securityManagerV4.RequestPermissionAsync(
            new SecurityPermissionRequest("platform-recovery-probe", SecurityPermissionCategory.File,
                SecurityPermissionAction.Delete, Environment.SystemDirectory,
                "Verify that protected system paths remain default-denied before integrity recovery."));
        bool permissionSafe = !permissionProbe.Allowed;

        UpdateStatus update = await _updateManager.CheckAsync();
        bool updateTrusted = update.AvailableVersion is not null ||
            !update.Message.Contains("尚未配置更新源", StringComparison.Ordinal);

        byte[] plain = RandomNumberGenerator.GetBytes(32);
        byte[] decrypted = _encryptionService.Decrypt(_encryptionService.Encrypt(plain));
        bool encryptionHealthy = CryptographicOperations.FixedTimeEquals(plain, decrypted);
        return new IntegrityRecoveryPrerequisites(
            assessment.DatabaseIntegrityValid,
            assessment.AuditIntegrityValid,
            permissionSafe,
            updateTrusted,
            assessment.RejectedPlugins == 0,
            encryptionHealthy);
    }

    private void ApplyAssessment(SecurityAssessment assessment)
    {
        SecurityScore = assessment.Score;
        IsEmergencyReadOnly = _emergencyGate.EmergencyState.IsReadOnly;
        string mode = IsEmergencyReadOnly
            ? $"安全只读模式：{_emergencyGate.EmergencyState.Reason}"
            : "敏感操作保护正常";
        SecurityStatus = $"评分 {assessment.Score}/100；应用/数据库/审计完整性：{Pass(assessment.ApplicationIntegrityValid)}/{Pass(assessment.DatabaseIntegrityValid)}/{Pass(assessment.AuditIntegrityValid)}；拒绝插件 {assessment.RejectedPlugins}。{mode}。{assessment.Summary}";
    }

    private static string Pass(bool value) => value ? "通过" : "未通过";
}