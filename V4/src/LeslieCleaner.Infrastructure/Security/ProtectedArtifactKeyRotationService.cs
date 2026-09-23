using System.Security.Cryptography;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>Transactional key rotation for an explicit set of protected local encrypted artifacts.</summary>
public sealed class ProtectedArtifactKeyRotationService : IProtectedArtifactKeyRotationService
{
    private readonly IEncryptionService _encryptionService;
    private readonly ILogService _logService;
    private readonly string[] _allowedRoots;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates a key rotation service limited to application data, backup, and configuration roots.</summary>
    public ProtectedArtifactKeyRotationService(IPathService pathService,
        IEncryptionService encryptionService, ILogService logService)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _allowedRoots =
        [
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(pathService.EnsureDirectory(pathService.DataDirectory))),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(pathService.EnsureDirectory(pathService.BackupDirectory))),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(pathService.EnsureDirectory(pathService.ConfigDirectory))),
        ];
    }

    /// <inheritdoc />
    public async Task<ProtectedArtifactRotationResult> RotateAsync(
        IReadOnlyList<string> encryptedArtifactPaths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(encryptedArtifactPaths);
        if (encryptedArtifactPaths.Count == 0)
            throw new ArgumentException("At least one encrypted artifact is required.", nameof(encryptedArtifactPaths));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var originals = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var plaintext = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();
            KeyRotationResult? rotation = null;
            try
            {
                foreach (string candidate in encryptedArtifactPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string path = ValidatePath(candidate);
                    if (!originals.TryAdd(path, await File.ReadAllBytesAsync(path, cancellationToken)
                            .ConfigureAwait(false)))
                        throw new ArgumentException($"Duplicate encrypted artifact: {path}",
                            nameof(encryptedArtifactPaths));
                    plaintext[path] = _encryptionService.Decrypt(originals[path]);
                }

                cancellationToken.ThrowIfCancellationRequested();
                rotation = await _encryptionService.RotateKeyAsync(cancellationToken).ConfigureAwait(false);
                foreach ((string path, byte[] cleartext) in plaintext)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await WriteAtomicallyAsync(path, _encryptionService.Encrypt(cleartext), cancellationToken)
                        .ConfigureAwait(false);
                }
                await _encryptionService.RetireKeyAsync(rotation.PreviousKeyId, cancellationToken)
                    .ConfigureAwait(false);
                _logService.LogEvent(LogEventKind.UserAction, "Protected artifact key rotation completed",
                    new Dictionary<string, object?>
                    {
                        ["PreviousKeyId"] = rotation.PreviousKeyId,
                        ["ActiveKeyId"] = rotation.ActiveKeyId,
                        ["ArtifactCount"] = plaintext.Count,
                    });
                return new ProtectedArtifactRotationResult(true, rotation.PreviousKeyId,
                    rotation.ActiveKeyId, plaintext.Count, [], DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException)
            {
                foreach ((string path, byte[] original) in originals)
                    await WriteAtomicallyAsync(path, original, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                       or CryptographicException or ArgumentException or InvalidOperationException)
            {
                errors.Add(exception.Message);
                foreach ((string path, byte[] original) in originals)
                {
                    try
                    {
                        await WriteAtomicallyAsync(path, original, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception rollbackException) when (rollbackException is IOException
                               or UnauthorizedAccessException)
                    {
                        errors.Add($"Rollback failed for '{path}': {rollbackException.Message}");
                    }
                }
                _logService.LogException(exception, "ProtectedArtifactKeyRotationService.RotateAsync");
                return new ProtectedArtifactRotationResult(false, rotation?.PreviousKeyId,
                    rotation?.ActiveKeyId, 0, errors, DateTimeOffset.UtcNow);
            }
            finally
            {
                foreach (byte[] cleartext in plaintext.Values)
                    CryptographicOperations.ZeroMemory(cleartext);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ValidatePath(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || !Path.IsPathFullyQualified(candidate))
            throw new ArgumentException("Encrypted artifact paths must be absolute.", nameof(candidate));
        string path = Path.GetFullPath(candidate);
        string? root = _allowedRoots.FirstOrDefault(allowedRoot =>
            path.StartsWith(allowedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        if (root is null) throw new UnauthorizedAccessException("Encrypted artifact escapes protected data roots.");
        if (!File.Exists(path)) throw new FileNotFoundException("Encrypted artifact not found.", path);
        string relative = Path.GetRelativePath(root, path);
        string current = root;
        foreach (string segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Reparse-point encrypted artifacts are rejected.");
        }
        return path;
    }

    private static async Task WriteAtomicallyAsync(string path, byte[] content,
        CancellationToken cancellationToken)
    {
        string temporaryPath = path + $".{Guid.NewGuid():N}.rotate";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}