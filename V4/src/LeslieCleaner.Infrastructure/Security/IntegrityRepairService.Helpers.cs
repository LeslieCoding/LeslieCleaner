using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

public sealed partial class IntegrityRepairService
{
    private async Task<IntegrityManifestDocument> ReadTrustedManifestAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_manifestPath))
            throw new FileNotFoundException("No explicitly trusted integrity manifest exists.", _manifestPath);
        IntegrityManifestEnvelope envelope;
        try
        {
            string json = await File.ReadAllTextAsync(_manifestPath, cancellationToken).ConfigureAwait(false);
            envelope = JsonSerializer.Deserialize<IntegrityManifestEnvelope>(json, JsonOptions)
                ?? throw new CryptographicException("The integrity manifest is empty or invalid.");
        }
        catch (JsonException exception)
        {
            throw new CryptographicException("The integrity manifest cannot be parsed.", exception);
        }
        if (envelope.Manifest.SchemaVersion != SchemaVersion || envelope.Manifest.Entries.Count == 0)
            throw new CryptographicException("The integrity manifest schema or target list is invalid.");
        string payloadHash = ComputeManifestHash(envelope.Manifest);
        string sealedHash;
        try
        {
            sealedHash = Encoding.UTF8.GetString(
                _encryptionService.Decrypt(Convert.FromBase64String(envelope.Seal)));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            throw new CryptographicException("The integrity manifest seal is invalid.", exception);
        }
        if (!FixedTimeHexEquals(payloadHash, sealedHash))
            throw new CryptographicException("The integrity manifest was modified after trust was established.");
        string applicationRoot = NormalizeExistingDirectory(envelope.Manifest.ApplicationRoot,
            nameof(envelope.Manifest.ApplicationRoot));
        string backupRoot = NormalizeExistingDirectory(envelope.Manifest.TrustedBackupRoot,
            nameof(envelope.Manifest.TrustedBackupRoot));
        if (IsSameOrDescendant(backupRoot, applicationRoot))
            throw new UnsafeIntegrityPathException("The trusted backup root is inside the application root.");
        EnsureNoReparsePoint(applicationRoot, applicationRoot);
        EnsureNoReparsePoint(backupRoot, backupRoot);
        if (Enum.GetValues<IntegrityArtifactKind>()
            .Except(envelope.Manifest.Entries.Select(static entry => entry.Kind)).Any())
            throw new CryptographicException(
                "The integrity manifest does not cover every required startup target category.");
        return envelope.Manifest with { ApplicationRoot = applicationRoot, TrustedBackupRoot = backupRoot };
    }

    private async Task WriteSealedManifestAsync(IntegrityManifestDocument manifest,
        CancellationToken cancellationToken)
    {
        string hash = ComputeManifestHash(manifest);
        string seal = Convert.ToBase64String(_encryptionService.Encrypt(Encoding.UTF8.GetBytes(hash)));
        var envelope = new IntegrityManifestEnvelope(manifest, seal);
        await WriteJsonAtomicallyAsync(_manifestPath, envelope, cancellationToken).ConfigureAwait(false);
    }

    private Task PersistRecordsAsync(IReadOnlyList<FileIntegrityRecord> records,
        CancellationToken cancellationToken) => WriteJsonAtomicallyAsync(_recordsPath, records, cancellationToken);

    private static async Task WriteJsonAtomicallyAsync<T>(string path, T value,
        CancellationToken cancellationToken)
    {
        string temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(value, JsonOptions),
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private void VerifyOptionalSignature(string sha256, string? signatureBase64)
    {
        if (string.IsNullOrWhiteSpace(signatureBase64)) return;
        if (string.IsNullOrWhiteSpace(_options.TrustedSignerPublicKeyPem))
            throw new CryptographicException(
                "A signature was supplied but no externally trusted signer key is configured.");
        try
        {
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportFromPem(_options.TrustedSignerPublicKeyPem);
            bool valid = verifier.VerifyHash(Convert.FromHexString(sha256),
                Convert.FromBase64String(signatureBase64));
            if (!valid) throw new CryptographicException("The trusted detached file signature is invalid.");
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("The detached signature format is invalid.", exception);
        }
    }

    private static string NormalizeExistingDirectory(string path, string parameterName)
    {
        string normalized = NormalizeDirectory(path, parameterName);
        if (!Directory.Exists(normalized))
            throw new DirectoryNotFoundException($"Directory not found: {normalized}");
        return normalized;
    }

    private static string NormalizeDirectory(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("An absolute directory path is required.", parameterName);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private (IntegrityManifestRoot RootKind, string TrustedRoot, string FullPath) NormalizeTargetPath(
        TrustedIntegrityTarget target, string applicationRoot)
    {
        if (string.IsNullOrWhiteSpace(target.FilePath) || !Path.IsPathFullyQualified(target.FilePath))
            throw new ArgumentException("Every integrity target must use an absolute path.", nameof(target));
        string fullPath = Path.GetFullPath(target.FilePath);
        (IntegrityManifestRoot Kind, string Root)[] roots = target.Kind switch
        {
            IntegrityArtifactKind.Executable or IntegrityArtifactKind.CoreLibrary =>
                [(IntegrityManifestRoot.Application, applicationRoot)],
            IntegrityArtifactKind.Configuration =>
                [(IntegrityManifestRoot.Application, applicationRoot), (IntegrityManifestRoot.Configuration, _configurationRoot)],
            IntegrityArtifactKind.Database =>
                [(IntegrityManifestRoot.Application, applicationRoot), (IntegrityManifestRoot.Data, _dataRoot)],
            IntegrityArtifactKind.PluginMetadata =>
                [(IntegrityManifestRoot.Application, applicationRoot), (IntegrityManifestRoot.ProgramData, _programDataRoot)],
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        (IntegrityManifestRoot Kind, string Root) selected = roots.FirstOrDefault(candidate =>
            IsSameOrDescendant(fullPath, candidate.Root) &&
            !string.Equals(fullPath, candidate.Root, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(selected.Root))
            throw new UnsafeIntegrityPathException($"Integrity target is outside its category trust root: {target.FilePath}");
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Integrity target does not exist.", fullPath);
        return (selected.Kind, selected.Root, fullPath);
    }

    private string ResolveManifestTargetPath(IntegrityManifestDocument manifest, IntegrityManifestEntry entry,
        out string trustedRoot)
    {
        trustedRoot = entry.RootKind switch
        {
            IntegrityManifestRoot.Application => manifest.ApplicationRoot,
            IntegrityManifestRoot.Configuration => _configurationRoot,
            IntegrityManifestRoot.Data => _dataRoot,
            IntegrityManifestRoot.ProgramData => _programDataRoot,
            _ => throw new UnsafeIntegrityPathException("Manifest target root is invalid."),
        };
        return NormalizeManifestPath(trustedRoot, entry.RelativePath);
    }
    private static string NormalizeFileUnderRoot(string filePath, string root)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath))
            throw new ArgumentException("Every integrity target must use an absolute path.", nameof(filePath));
        string fullPath = Path.GetFullPath(filePath);
        if (!IsSameOrDescendant(fullPath, root) ||
            string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            throw new UnsafeIntegrityPathException($"Integrity target escapes the application root: {filePath}");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Integrity target does not exist.", fullPath);
        return fullPath;
    }

    private static string NormalizeManifestPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathFullyQualified(relativePath))
            throw new UnsafeIntegrityPathException("Manifest entries must contain non-empty relative paths.");
        string fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!IsSameOrDescendant(fullPath, root) ||
            string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            throw new UnsafeIntegrityPathException($"Manifest path escapes its trusted root: {relativePath}");
        return fullPath;
    }

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        string candidateFull = Path.GetFullPath(candidate);
        string rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return string.Equals(candidateFull, rootFull, StringComparison.OrdinalIgnoreCase) ||
            candidateFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNoReparsePoint(string trustedRoot, string path,
        bool allowMissingLeaf = false)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trustedRoot));
        string target = Path.GetFullPath(path);
        if (!IsSameOrDescendant(target, root))
            throw new UnsafeIntegrityPathException("Path is outside its trusted boundary.");
        string relative = Path.GetRelativePath(root, target);
        string current = root;
        foreach (string segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                if (allowMissingLeaf && string.Equals(current, target, StringComparison.OrdinalIgnoreCase)) return;
                continue;
            }
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new UnsafeIntegrityPathException(
                    $"Reparse points are not trusted integrity paths: {current}");
        }
    }

    private static async Task CopyFileAsync(string source, string destination,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 81920;
        await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await input.CopyToAsync(output, bufferSize, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ComputeFileHashAsync(string filePath,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(
            await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static string ComputeManifestHash(IntegrityManifestDocument document) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, JsonOptions))));

    private static bool FixedTimeHexEquals(string expected, string actual)
    {
        try
        {
            byte[] left = Convert.FromHexString(expected);
            byte[] right = Convert.FromHexString(actual);
            return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsIntegrityException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or CryptographicException
            or JsonException or ArgumentException;

    private void EnterEmergency(string reason, IReadOnlyList<string> conditions) =>
        _emergencyGate.EnterEmergencyReadOnly(reason, conditions);

    private async Task AuditAsync(SecurityAuditEventType eventType, SecurityAuditLevel level,
        string result, string description, string recommendation, CancellationToken cancellationToken)
    {
        try
        {
            await _auditStore.AppendAsync(new SecurityAuditEvent(DateTimeOffset.UtcNow, eventType, level,
                nameof(IntegrityRepairService), result, description, recommendation), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logService.LogException(exception, "IntegrityRepairService.SecurityAudit");
        }
        _logService.LogEvent(level == SecurityAuditLevel.Information
                ? LogEventKind.UserAction : LogEventKind.Exception,
            description, new Dictionary<string, object?>
            {
                ["EventType"] = eventType.ToString(), ["Level"] = level.ToString(),
                ["Result"] = result, ["Recommendation"] = recommendation,
            });
    }

    private static StartupIntegrityReport CreateUnavailableReport() =>
        new(false, [], 1, 0, DateTimeOffset.UtcNow);

    private enum IntegrityManifestRoot { Application, Configuration, Data, ProgramData }

    private sealed record IntegrityManifestEntry(Guid Id, IntegrityManifestRoot RootKind, string RelativePath, IntegrityArtifactKind Kind,
        string Sha256, long Size, string BackupRelativePath, string? SignatureBase64);

    private sealed record IntegrityManifestDocument(int SchemaVersion, string ApplicationRoot,
        string TrustedBackupRoot, string BaselineId, DateTimeOffset CreatedUtc, string AuthorizationReason,
        IReadOnlyList<IntegrityManifestEntry> Entries);

    private sealed record IntegrityManifestEnvelope(IntegrityManifestDocument Manifest, string Seal);

    private sealed class UnsafeIntegrityPathException(string message) : IOException(message);
}