using System.IO;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.UI.Services;

/// <summary>从已发布应用目录选择完整性基线的五类最小启动目标。</summary>
public static class IntegrityTargetPlanner
{
    /// <summary>返回启动器、核心库、运行配置、数据库引擎与插件契约五类目标。</summary>
    public static IReadOnlyList<TrustedIntegrityTarget> Build(string applicationRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationRoot);
        string root = Path.GetFullPath(applicationRoot);
        var targets = new[]
        {
            Target(root, "LeslieCleaner.App.exe", IntegrityArtifactKind.Executable),
            Target(root, "LeslieCleaner.Core.dll", IntegrityArtifactKind.CoreLibrary),
            Target(root, "LeslieCleaner.App.runtimeconfig.json", IntegrityArtifactKind.Configuration),
            Target(root, "LeslieCleaner.Data.dll", IntegrityArtifactKind.Database),
            Target(root, "LeslieCleaner.PluginSdk.dll", IntegrityArtifactKind.PluginMetadata),
        };
        IntegrityArtifactKind[] kinds = targets.Select(static target => target.Kind).Distinct().ToArray();
        if (kinds.Length != Enum.GetValues<IntegrityArtifactKind>().Length)
        {
            throw new InvalidOperationException("Integrity baseline target selection does not cover all required categories.");
        }
        return targets;
    }

    private static TrustedIntegrityTarget Target(string root, string fileName, IntegrityArtifactKind kind)
    {
        string path = Path.GetFullPath(Path.Combine(root, fileName));
        string boundary = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            throw new FileNotFoundException($"可信基线目标不存在：{fileName}", path);
        }
        return new TrustedIntegrityTarget(path, kind);
    }
}