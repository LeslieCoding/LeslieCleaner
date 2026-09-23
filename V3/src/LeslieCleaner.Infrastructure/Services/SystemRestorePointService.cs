using System.Management;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Services;

/// <summary>
/// 系统还原点服务实现。经 WMI SystemRestore 类（root\default）创建还原点，
/// 供危险操作前自动调用。需要管理员权限；系统保护未启用时安全降级返回
/// <see langword="false"/>，不阻断主流程。仅支持 Windows。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SystemRestorePointService : ISystemRestorePointService
{
    private const uint BeginSystemChange = 100;
    private const uint ApplicationInstall = 0;

    private readonly ILogService _logService;

    /// <summary>以日志服务创建还原点服务。</summary>
    /// <param name="logService">日志服务。</param>
    public SystemRestorePointService(ILogService logService) => _logService = logService;

    /// <inheritdoc />
    public Task<bool> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        cancellationToken.ThrowIfCancellationRequested();

        // WMI ManagementClass API 为同步模型，放入线程池执行避免阻塞调用方
        return Task.Run(() => CreateRestorePointCore(description), cancellationToken);
    }

    private bool CreateRestorePointCore(string description)
    {
        try
        {
            var scope = new ManagementScope(@"\\.\root\default");
            scope.Connect();

            using var systemRestore = new ManagementClass(scope, new ManagementPath("SystemRestore"), null);
            using ManagementBaseObject inParams = systemRestore.GetMethodParameters("CreateRestorePoint");
            inParams["Description"] = description;
            inParams["EventType"] = BeginSystemChange;
            inParams["RestorePointType"] = ApplicationInstall;

            using ManagementBaseObject outParams = systemRestore.InvokeMethod("CreateRestorePoint", inParams, null);
            uint returnValue = (uint)(outParams["ReturnValue"] ?? 1u);

            _logService.LogEvent(LogEventKind.UserAction, "创建系统还原点", new Dictionary<string, object?>
            {
                ["Description"] = description,
                ["Success"] = returnValue == 0,
            });

            return returnValue == 0;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            // 系统保护关闭 / 权限不足 / 组策略禁用：记录后降级，不阻断危险操作主流程（由调用方决定是否继续）
            _logService.LogException(ex, "SystemRestorePointService.CreateRestorePointAsync",
                new Dictionary<string, object?> { ["Description"] = description });
            return false;
        }
    }
}
