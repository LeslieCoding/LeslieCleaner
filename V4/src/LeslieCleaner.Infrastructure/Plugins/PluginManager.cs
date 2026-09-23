using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Plugins;

/// <summary>
/// Validates and manages plugin package files. It deliberately never loads or executes third-party assemblies;
/// an out-of-process runtime host is required before enabled plugins can execute.
/// </summary>
public sealed class PluginManager : IPluginManager
{
    private const string ManifestFileName = "plugin.json";
    private const int SupportedApiMajor = 1;
    private static readonly HashSet<string> AllowedPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "File.Read", "File.Write", "File.Delete", "Registry.Read", "Registry.Write", "Registry.Delete",
        "Network.Access", "Network.RemoteApi", "Network.Upload", "Service.Read", "Service.Modify",
        "Process.Read", "Process.Terminate", "System.Configure",
    };
    private static readonly HashSet<string> DangerousPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "File.Delete", "Registry.Write", "Registry.Delete", "Service.Modify", "Process.Terminate", "System.Configure",
    };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IPathService _paths;
    private readonly IPluginStateStore _stateStore;
    private readonly ILogService _log;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    /// <summary>Creates a package manager.</summary>
    public PluginManager(IPathService paths, IPluginStateStore stateStore, ILogService log)
    {
        _paths = paths;
        _stateStore = stateStore;
        _log = log;
    }

    /// <summary>Performs the documented operation.</summary>
    public async Task<IReadOnlyList<PluginDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        string root = PluginRoot();
        IReadOnlyList<PluginStateRecord> states = await _stateStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<PluginDescriptor>();
        foreach (string directory in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsReparsePoint(directory) || string.Equals(Path.GetFileName(directory), ".staging", StringComparison.OrdinalIgnoreCase)) continue;
            string manifestPath = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(manifestPath)) continue;
            PluginStateRecord? state = states.FirstOrDefault(item => string.Equals(item.InstallPath, directory, StringComparison.OrdinalIgnoreCase));
            results.Add(await ValidateDirectoryAsync(directory, state?.ApprovedPermissions, state?.Enabled ?? false,
                requirePermissionApproval: false, cancellationToken).ConfigureAwait(false));
        }

        return results.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Performs the documented operation.</summary>
    public Task<PluginOperationResult> InstallAsync(PluginInstallRequest request, CancellationToken cancellationToken = default) =>
        InstallOrUpdateAsync(request, update: false, cancellationToken);

    /// <summary>Performs the documented operation.</summary>
    public Task<PluginOperationResult> UpdateAsync(PluginInstallRequest request, CancellationToken cancellationToken = default) =>
        InstallOrUpdateAsync(request, update: true, cancellationToken);

    /// <summary>Performs the documented operation.</summary>
    public async Task<PluginOperationResult> RemoveAsync(PluginRemovalRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePluginId(request.PluginId);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<PluginDescriptor> plugins = await DiscoverAsync(cancellationToken).ConfigureAwait(false);
            PluginDescriptor? target = plugins.FirstOrDefault(item => string.Equals(item.Id, request.PluginId, StringComparison.OrdinalIgnoreCase));
            if (target is null) return await AuditedResultAsync("Remove", request.PluginId, false, "Plugin is not installed.", "PLUGIN_NOT_FOUND").ConfigureAwait(false);
            PluginDescriptor? dependent = plugins.FirstOrDefault(item => item.Dependencies?.Any(dependency =>
                string.Equals(dependency.Id, request.PluginId, StringComparison.OrdinalIgnoreCase)) == true);
            if (dependent is not null)
            {
                return await AuditedResultAsync("Remove", request.PluginId, false,
                    $"Plugin '{dependent.Id}' depends on this plugin.", "PLUGIN_HAS_DEPENDENTS").ConfigureAwait(false);
            }

            string removalArea = _paths.EnsureDirectory(Path.Combine(PluginBackupRoot(), "Removal"));
            string staged = Path.Combine(removalArea, $"{request.PluginId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}");
            Directory.Move(Path.GetDirectoryName(target.ManifestPath)!, staged);
            try
            {
                await _stateStore.RemoveAsync(request.PluginId, cancellationToken).ConfigureAwait(false);
                if (request.DeleteUserData)
                {
                    string data = SafeChildPath(Path.Combine(_paths.ProgramDataRoot, "PluginData"), request.PluginId);
                    if (Directory.Exists(data)) Directory.Delete(data, true);
                }
                Directory.Delete(staged, true);
                return await AuditedResultAsync("Remove", request.PluginId, true, "Plugin removed safely.").ConfigureAwait(false);
            }
            catch
            {
                Directory.Move(staged, SafeChildPath(PluginRoot(), request.PluginId));
                throw;
            }
        }
        finally { _operationGate.Release(); }
    }

    /// <summary>Performs the documented operation.</summary>
    public async Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default)
    {
        ValidatePluginId(pluginId);
        PluginDescriptor? descriptor = (await DiscoverAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item => string.Equals(item.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (descriptor is null || descriptor.TrustStatus is not (PluginTrustStatus.Disabled or PluginTrustStatus.Ready))
            throw new InvalidOperationException("Only an installed and validated plugin can change enabled state.");
        PluginStateRecord state = (await _stateStore.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .Single(item => string.Equals(item.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
        await _stateStore.UpsertAsync(state with { Enabled = enabled, UpdatedAt = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync("SetEnabled", pluginId, true, enabled ? "Plugin enabled for a future isolated host." : "Plugin disabled.", null, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Performs the documented operation.</summary>
    public Task<IReadOnlyList<PluginAuditRecord>> GetAuditAsync(CancellationToken cancellationToken = default) =>
        _stateStore.GetAuditAsync(cancellationToken);

    private async Task<PluginOperationResult> InstallOrUpdateAsync(PluginInstallRequest request, bool update, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PackagePath);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? staging = null;
        string? backup = null;
        string? pluginId = null;
        try
        {
            staging = await StagePackageAsync(request.PackagePath, cancellationToken).ConfigureAwait(false);
            PluginDescriptor candidate = await ValidateDirectoryAsync(staging, request.ApprovedPermissions, false, true, cancellationToken).ConfigureAwait(false);
            pluginId = candidate.Id;
            if (candidate.TrustStatus != PluginTrustStatus.Disabled)
                return await AuditedResultAsync(update ? "Update" : "Install", pluginId, false, candidate.Detail ?? "Package validation failed.", candidate.TrustStatus.ToString()).ConfigureAwait(false);
            if ((candidate.Permissions ?? []).Any(DangerousPermissions.Contains) && !request.DangerousPermissionsConfirmed)
                return await AuditedResultAsync(update ? "Update" : "Install", pluginId, false, "Dangerous permissions require explicit confirmation.", "DANGEROUS_PERMISSION_CONFIRMATION_REQUIRED").ConfigureAwait(false);

            string destination = SafeChildPath(PluginRoot(), candidate.Id);
            bool exists = Directory.Exists(destination);
            if (update != exists)
                return await AuditedResultAsync(update ? "Update" : "Install", pluginId, false,
                    update ? "Plugin is not installed." : "Plugin is already installed.", update ? "PLUGIN_NOT_FOUND" : "PLUGIN_ALREADY_INSTALLED").ConfigureAwait(false);
            PluginStateRecord? previous = (await _stateStore.GetAllAsync(cancellationToken).ConfigureAwait(false))
                .FirstOrDefault(item => string.Equals(item.PluginId, candidate.Id, StringComparison.OrdinalIgnoreCase));
            if (update)
            {
                if (previous is null || !TryVersion(candidate.Version, out Version? next) || !TryVersion(previous.Version, out Version? current) || next <= current)
                    return await AuditedResultAsync("Update", pluginId, false, "Update version must be greater than the installed version.", "VERSION_NOT_NEWER").ConfigureAwait(false);
                backup = Path.Combine(PluginBackupRoot(), $"{candidate.Id}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}");
                CopyDirectory(destination, backup);
            }

            string replacement = destination + ".replacement." + Guid.NewGuid().ToString("N");
            Directory.Move(staging, replacement);
            staging = null;
            if (exists) Directory.Delete(destination, true);
            Directory.Move(replacement, destination);
            PluginDescriptor installed = await ValidateDirectoryAsync(destination, request.ApprovedPermissions, previous?.Enabled ?? false, true, cancellationToken).ConfigureAwait(false);
            if (installed.TrustStatus is not (PluginTrustStatus.Disabled or PluginTrustStatus.Ready)) throw new InvalidDataException(installed.Detail);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            await _stateStore.UpsertAsync(new PluginStateRecord(installed.Id, installed.Name, installed.Version,
                previous?.Enabled ?? false, destination, (request.ApprovedPermissions ?? []).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                previous?.InstalledAt ?? now, now), cancellationToken).ConfigureAwait(false);
            await WriteAuditAsync(update ? "Update" : "Install", pluginId, true, update ? "Plugin updated safely." : "Plugin installed disabled by default.", backup, false, cancellationToken).ConfigureAwait(false);
            return new PluginOperationResult(true, pluginId, update ? "Plugin updated safely." : "Plugin installed disabled by default.", Plugin: installed, BackupPath: backup);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or CryptographicException)
        {
            bool rolledBack = false;
            if (pluginId is not null && backup is not null && Directory.Exists(backup))
            {
                string destination = SafeChildPath(PluginRoot(), pluginId);
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                CopyDirectory(backup, destination);
                rolledBack = true;
            }
            _log.LogException(exception, "PluginManager.PackageOperation", new Dictionary<string, object?> { ["PluginId"] = pluginId, ["RolledBack"] = rolledBack });
            await WriteAuditAsync(update ? "Update" : "Install", pluginId, false, exception.Message, backup, rolledBack, CancellationToken.None).ConfigureAwait(false);
            return new PluginOperationResult(false, pluginId, "Plugin package operation failed.", "PLUGIN_OPERATION_FAILED", BackupPath: backup, RolledBack: rolledBack);
        }
        finally
        {
            if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, true);
            _operationGate.Release();
        }
    }

    private async Task<PluginDescriptor> ValidateDirectoryAsync(string directory, IReadOnlyCollection<string>? approvedPermissions,
        bool enabled, bool requirePermissionApproval, CancellationToken cancellationToken)
    {
        string manifestPath = Path.Combine(directory, ManifestFileName);
        PluginManifest? manifest;
        try
        {
            if (IsReparsePoint(directory) || !File.Exists(manifestPath) || IsReparsePoint(manifestPath)) return Invalid(manifestPath, PluginTrustStatus.UnsafePath, "Manifest path is unsafe or missing.");
            await using FileStream stream = new(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            manifest = await JsonSerializer.DeserializeAsync<PluginManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Invalid(manifestPath, PluginTrustStatus.InvalidManifest, exception.Message);
        }
        if (!IsValidManifest(manifest)) return Invalid(manifestPath, PluginTrustStatus.InvalidManifest, "Manifest fields or semantic versions are invalid.");
        string assembly = SafeChildPath(directory, manifest!.EntryAssembly);
        if (!File.Exists(assembly)) return Create(manifest, manifestPath, assembly, PluginTrustStatus.MissingAssembly, "Entry assembly is missing.");
        if (IsReparsePoint(assembly)) return Create(manifest, manifestPath, assembly, PluginTrustStatus.UnsafePath, "Entry assembly cannot be a reparse point.");
        if (!TryVersion(manifest.ApiVersion, out Version? api) || api is null || api.Major != SupportedApiMajor) return Create(manifest, manifestPath, assembly, PluginTrustStatus.IncompatibleApi, "Plugin API major version is incompatible.");
        string[] permissions = (manifest.Permissions ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (permissions.Any(permission => !AllowedPermissions.Contains(permission))) return Create(manifest, manifestPath, assembly, PluginTrustStatus.PermissionDenied, "Manifest requests an unknown permission.");
        if (requirePermissionApproval && permissions.Except(approvedPermissions ?? [], StringComparer.OrdinalIgnoreCase).Any()) return Create(manifest, manifestPath, assembly, PluginTrustStatus.PermissionDenied, "All requested permissions must be explicitly approved.");
        IReadOnlyList<PluginStateRecord> states = await _stateStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (PluginDependency dependency in manifest.Dependencies ?? [])
        {
            PluginStateRecord? installed = states.FirstOrDefault(item => string.Equals(item.PluginId, dependency.Id, StringComparison.OrdinalIgnoreCase));
            if (installed is null || !TryVersion(installed.Version, out Version? actual) || !TryVersion(dependency.MinimumVersion, out Version? minimum) || actual < minimum)
                return Create(manifest, manifestPath, assembly, PluginTrustStatus.MissingDependency, $"Dependency '{dependency.Id}' is missing or too old.");
        }
        await using FileStream binary = new(assembly, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(binary, cancellationToken).ConfigureAwait(false));
        byte[] expected = Convert.FromHexString(manifest.Sha256);
        if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(hash))) return Create(manifest, manifestPath, assembly, PluginTrustStatus.HashMismatch, "Entry assembly SHA-256 does not match.");
        return Create(manifest, manifestPath, assembly, enabled ? PluginTrustStatus.Ready : PluginTrustStatus.Disabled,
            enabled ? "Validated and enabled for a future isolated host; not loaded in this process." : "Validated and disabled; third-party code was not executed.");
    }

    private Task<string> StagePackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        string full = Path.GetFullPath(packagePath);
        string staging = Path.Combine(_paths.EnsureDirectory(Path.Combine(PluginRoot(), ".staging")), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        if (Directory.Exists(full)) CopyDirectory(full, staging);
        else if (File.Exists(full) && string.Equals(Path.GetExtension(full), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            using ZipArchive archive = ZipFile.OpenRead(full);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string output = SafeChildPath(staging, entry.FullName);
                if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(output);
                else { Directory.CreateDirectory(Path.GetDirectoryName(output)!); entry.ExtractToFile(output, false); }
            }
        }
        else throw new FileNotFoundException("Plugin package must be an existing directory or ZIP archive.", full);
        return Task.FromResult(staging);
    }

    private async Task<PluginOperationResult> AuditedResultAsync(string operation, string? pluginId, bool success, string message, string? error = null)
    {
        await WriteAuditAsync(operation, pluginId, success, message, null, false, CancellationToken.None).ConfigureAwait(false);
        return new PluginOperationResult(success, pluginId, message, error);
    }

    private async Task WriteAuditAsync(string operation, string? pluginId, bool success, string message, string? backup, bool rolledBack, CancellationToken cancellationToken)
    {
        var record = new PluginAuditRecord(Guid.NewGuid(), operation, pluginId, success, message, DateTimeOffset.UtcNow, backup, rolledBack);
        await _stateStore.AppendAuditAsync(record, cancellationToken).ConfigureAwait(false);
        _log.LogEvent(LogEventKind.UserAction, $"Plugin {operation}: {message}", new Dictionary<string, object?> { ["OperationId"] = record.OperationId, ["PluginId"] = pluginId, ["Success"] = success, ["RolledBack"] = rolledBack });
    }

    private string PluginRoot() => _paths.EnsureDirectory(Path.Combine(_paths.ProgramDataRoot, "Plugins"));
    private string PluginBackupRoot() => _paths.EnsureDirectory(Path.Combine(_paths.BackupDirectory, "Plugins"));
    private static bool TryVersion(string value, out Version? version) => Version.TryParse(value, out version);
    private static bool IsValidManifest(PluginManifest? manifest) => manifest is not null && IsSafePluginId(manifest.Id) &&
        !string.IsNullOrWhiteSpace(manifest.Name) && !string.IsNullOrWhiteSpace(manifest.Author) &&
        !string.IsNullOrWhiteSpace(manifest.Type) && TryVersion(manifest.Version, out _) && TryVersion(manifest.ApiVersion, out _) &&
        !string.IsNullOrWhiteSpace(manifest.EntryAssembly) && manifest.Sha256.Length == 64 && manifest.Sha256.All(Uri.IsHexDigit) &&
        (manifest.Dependencies ?? []).All(item => IsSafePluginId(item.Id) && TryVersion(item.MinimumVersion, out _));
    private static bool IsSafePluginId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 80 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
    private static void ValidatePluginId(string value) { if (!IsSafePluginId(value)) throw new ArgumentException("Plugin identifier contains unsafe characters.", nameof(value)); }
    private static bool IsReparsePoint(string path) => File.Exists(path) || Directory.Exists(path) ? (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 : false;
    private static string SafeChildPath(string root, string relative)
    {
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string child = Path.GetFullPath(Path.Combine(root, relative));
        if (!child.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Plugin package path escapes its root.");
        return child;
    }
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (IsReparsePoint(file)) throw new InvalidDataException("Plugin packages cannot contain reparse points.");
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
    private static PluginDescriptor Invalid(string manifest, PluginTrustStatus status, string detail) => new("unknown", "Unknown plugin", "unknown", string.Empty, manifest, string.Empty, status, detail);
    private static PluginDescriptor Create(PluginManifest manifest, string manifestPath, string assemblyPath, PluginTrustStatus status, string detail) =>
        new(manifest.Id, manifest.Name, manifest.Version, manifest.Description ?? string.Empty, manifestPath, assemblyPath, status, detail, manifest.Author, manifest.Type, manifest.ApiVersion, manifest.Permissions ?? [], manifest.Dependencies ?? []);
    private sealed record PluginManifest(string Id, string Name, string Version, string Author, string Type, string ApiVersion, string EntryAssembly, string Sha256, string? Description, string[]? Permissions, PluginDependency[]? Dependencies);
}


