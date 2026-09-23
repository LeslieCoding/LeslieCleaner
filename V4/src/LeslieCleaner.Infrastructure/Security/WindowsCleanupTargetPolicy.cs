using System.Runtime.InteropServices;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>
/// Last-mile file deletion policy. It blocks application/security state, volume roots, reparse traversal,
/// boot resources, Windows servicing directories, and files covered by Windows Resource Protection.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsCleanupTargetPolicy : ICleanupTargetPolicy
{
    private readonly string[] _privateBoundaries;
    private readonly string[] _systemBoundaries;
    private readonly HashSet<string> _criticalFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "pagefile.sys", "hiberfil.sys", "swapfile.sys", "bootmgr", "bootnxt", "ntldr", "ntdetect.com",
    };

    /// <summary>Creates the policy from application, security storage, and Windows boundaries.</summary>
    public WindowsCleanupTargetPolicy(IPathService pathService)
        : this(pathService, AppContext.BaseDirectory)
    {
    }

    /// <summary>Creates the policy with an explicit application root for deterministic tests.</summary>
    public WindowsCleanupTargetPolicy(IPathService pathService, string applicationDirectory)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

        _privateBoundaries = new[]
            {
                applicationDirectory,
                pathService.ConfigDirectory,
                pathService.DataDirectory,
                pathService.BackupDirectory,
                pathService.QuarantineDirectory,
            }
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeBoundary)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        _systemBoundaries = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Path.Combine(windows, "SysWOW64"),
                Path.Combine(windows, "WinSxS"),
                Path.Combine(windows, "servicing"),
                Path.Combine(windows, "Boot"),
            }
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeBoundary)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public CleanupTargetDecision EvaluateFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\0') >= 0)
        {
            return Denied(null, OperationFailureKind.InvalidTarget, "文件目标为空或格式无效。");
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Denied(null, OperationFailureKind.InvalidTarget, "文件目标无法规范化。");
        }

        string? volumeRoot = Path.GetPathRoot(normalized);
        if (!string.IsNullOrWhiteSpace(volumeRoot) &&
            string.Equals(normalized, volumeRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            return Denied(normalized, OperationFailureKind.ProtectedResource, "禁止清理卷根目录。");
        }

        if (_privateBoundaries.Any(boundary => IsSameOrDescendant(normalized, boundary)))
        {
            return Denied(normalized, OperationFailureKind.ProtectedResource, "目标位于应用核心、配置、备份或隔离区边界内。");
        }

        if (_systemBoundaries.Any(boundary => IsSameOrDescendant(normalized, boundary)) ||
            _criticalFileNames.Contains(Path.GetFileName(normalized)))
        {
            return Denied(normalized, OperationFailureKind.ProtectedResource, "目标属于 Windows 核心或启动资源。");
        }

        try
        {
            if (ContainsExistingReparsePoint(normalized))
            {
                return Denied(normalized, OperationFailureKind.ProtectedResource,
                    "目标路径经过符号链接、联接或其他重解析点，已阻止越界清理。");
            }

            if (OperatingSystem.IsWindows() && File.Exists(normalized) && SfcIsFileProtected(IntPtr.Zero, normalized))
            {
                return Denied(normalized, OperationFailureKind.ProtectedResource,
                    "目标受 Windows 资源保护，禁止接管所有权或强制删除。");
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Denied(normalized, OperationFailureKind.RequiresElevation,
                "无法读取目标安全元数据，需要用户确认后以管理员模式重试检查。");
        }
        catch (IOException)
        {
            return Denied(normalized, OperationFailureKind.VerificationFailed, "无法可靠验证目标安全边界。");
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(windows) && IsSameOrDescendant(normalized, NormalizeBoundary(windows)))
            {
                return Denied(normalized, OperationFailureKind.ProtectedResource,
                    "Windows 资源保护接口不可用；系统目录目标按失败安全策略阻止。");
            }
        }

        return new CleanupTargetDecision(true, normalized, OperationFailureKind.None, "目标通过最终安全检查。");
    }

    private static bool ContainsExistingReparsePoint(string path)
    {
        string? current = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        foreach (string segment in path[current.Length..].Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                continue;
            }

            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeBoundary(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsSameOrDescendant(string candidate, string boundary) =>
        string.Equals(candidate, boundary, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static CleanupTargetDecision Denied(
        string? path, OperationFailureKind failureKind, string reason) =>
        new(false, path, failureKind, reason);

    [DllImport("sfc.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SfcIsFileProtected(IntPtr rpcHandle, string protectedFileName);
}
