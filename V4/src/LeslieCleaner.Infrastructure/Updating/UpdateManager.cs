using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Updating;

/// <summary>将文件哈希校验更新服务适配为 UI 可消费的 Core 契约。</summary>
public sealed class UpdateManager : IUpdateManager
{
    private readonly UpdateService _updateService;

    /// <summary>使用底层更新服务创建更新管理器。</summary>
    public UpdateManager(UpdateService updateService) => _updateService = updateService;

    /// <inheritdoc />
    public async Task<UpdateStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        UpdateCheckResult result = await _updateService.CheckForUpdateAsync(
            AppContext.BaseDirectory, cancellationToken).ConfigureAwait(false);
        string message = result.UpdateAvailable
            ? $"发现版本 {result.Manifest!.Version}，{result.FilesToDownload.Count} 个文件待下载并校验。"
            : "当前已是最新版本，或尚未配置更新源。";
        return new UpdateStatus(result.CurrentVersion, result.Manifest?.Version,
            result.FilesToDownload.Count, message, DateTimeOffset.UtcNow);
    }
}
