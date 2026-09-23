using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>Configuration for trusted detached signatures used by integrity manifests.</summary>
public sealed record IntegrityRepairOptions(string? TrustedSignerPublicKeyPem = null);

/// <summary>Fail-closed startup integrity verification and trusted-backup repair service.</summary>
public sealed partial class IntegrityRepairService : IIntegrityRepairService
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly ILogService _logService;
    private readonly SecurityAuditStore _auditStore;
    private readonly IEmergencySecurityGate _emergencyGate;
    private readonly ISecurityOperationGate _operationGate;
    private readonly IEncryptionService _encryptionService;
    private readonly IntegrityRepairOptions _options;
    private readonly string _manifestPath;
    private readonly string _configurationRoot;
    private readonly string _dataRoot;
    private readonly string _programDataRoot;
    private readonly string _recordsPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the integrity service using protected application data for its sealed manifest and records.</summary>
    public IntegrityRepairService(IPathService pathService, ILogService logService, SecurityAuditStore auditStore,
        IEmergencySecurityGate emergencyGate, ISecurityOperationGate operationGate,
        IEncryptionService encryptionService, IntegrityRepairOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _emergencyGate = emergencyGate ?? throw new ArgumentNullException(nameof(emergencyGate));
        _operationGate = operationGate ?? throw new ArgumentNullException(nameof(operationGate));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _options = options ?? new IntegrityRepairOptions();
        _configurationRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            pathService.EnsureDirectory(pathService.ConfigDirectory)));
        _dataRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            pathService.EnsureDirectory(pathService.DataDirectory)));
        _programDataRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            pathService.EnsureDirectory(pathService.ProgramDataRoot)));
        string dataDirectory = _dataRoot;
        _manifestPath = Path.Combine(dataDirectory, "security-integrity-manifest-v4.json");
        _recordsPath = Path.Combine(dataDirectory, "file-integrity-records-v4.json");
    }

    /// <inheritdoc />
    public async Task<TrustedBaselineResult> EstablishTrustedBaselineAsync(TrustedBaselineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.ExplicitlyAuthorized)
            throw new UnauthorizedAccessException("A trusted baseline requires explicit installer or user authorization.");
        if (string.IsNullOrWhiteSpace(request.AuthorizationReason))
            throw new ArgumentException("A non-empty authorization reason is required.", nameof(request));
        if (request.Targets is null || request.Targets.Count == 0)
            throw new ArgumentException("At least one protected file is required.", nameof(request));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_manifestPath) && !request.ReplaceExisting)
                return new TrustedBaselineResult(false,
                    "A trusted baseline already exists; explicit replacement authorization is required.", 0,
                    DateTimeOffset.UtcNow);

            string applicationRoot = NormalizeExistingDirectory(request.ApplicationRoot,
                nameof(request.ApplicationRoot));
            string backupRoot = NormalizeDirectory(request.TrustedBackupRoot,
                nameof(request.TrustedBackupRoot));
            if (IsSameOrDescendant(backupRoot, applicationRoot))
                throw new ArgumentException("The trusted backup root must be outside the application root.",
                    nameof(request));
            Directory.CreateDirectory(backupRoot);
            EnsureNoReparsePoint(applicationRoot, applicationRoot);
            EnsureNoReparsePoint(backupRoot, backupRoot);

            IntegrityArtifactKind[] missingKinds = Enum.GetValues<IntegrityArtifactKind>()
                .Except(request.Targets.Select(static target => target.Kind))
                .ToArray();
            if (missingKinds.Length > 0)
                throw new ArgumentException(
                    $"The trusted baseline must cover every startup target category. Missing: {string.Join(", ", missingKinds)}.",
                    nameof(request));

            var normalizedTargets = new List<(TrustedIntegrityTarget Target, IntegrityManifestRoot RootKind,
                string TrustedRoot, string FullPath, string RelativePath)>();
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TrustedIntegrityTarget target in request.Targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (IntegrityManifestRoot rootKind, string trustedRoot, string fullPath) =
                    NormalizeTargetPath(target, applicationRoot);
                EnsureNoReparsePoint(trustedRoot, fullPath);
                if (!uniquePaths.Add(fullPath))
                    throw new ArgumentException($"Duplicate integrity target: {fullPath}", nameof(request));
                normalizedTargets.Add((target, rootKind, trustedRoot, fullPath,
                    Path.GetRelativePath(trustedRoot, fullPath)));
            }

            string baselineId = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
            string baselineDirectory = Path.Combine(backupRoot, "integrity-baselines", baselineId);
            Directory.CreateDirectory(baselineDirectory);
            EnsureNoReparsePoint(backupRoot, baselineDirectory);
            var entries = new List<IntegrityManifestEntry>(normalizedTargets.Count);

            foreach ((TrustedIntegrityTarget target, IntegrityManifestRoot rootKind, string _,
                         string fullPath, string relativePath) in normalizedTargets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(fullPath);
                string sha256 = await ComputeFileHashAsync(fullPath, cancellationToken).ConfigureAwait(false);
                VerifyOptionalSignature(sha256, target.SignatureBase64);
                string pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relativePath)))[..16];
                string backupFileName = $"{entries.Count:D4}-{pathHash}{Path.GetExtension(fullPath)}";
                string backupPath = Path.Combine(baselineDirectory, backupFileName);
                await CopyFileAsync(fullPath, backupPath, cancellationToken).ConfigureAwait(false);
                string backupHash = await ComputeFileHashAsync(backupPath, cancellationToken).ConfigureAwait(false);
                if (!FixedTimeHexEquals(sha256, backupHash))
                    throw new CryptographicException($"Trusted backup verification failed for '{relativePath}'.");

                entries.Add(new IntegrityManifestEntry(Guid.NewGuid(), rootKind, relativePath, target.Kind, sha256,
                    info.Length, Path.GetRelativePath(backupRoot, backupPath), target.SignatureBase64));
            }

            var document = new IntegrityManifestDocument(SchemaVersion, applicationRoot, backupRoot, baselineId,
                DateTimeOffset.UtcNow, request.AuthorizationReason.Trim(), entries);
            await WriteSealedManifestAsync(document, cancellationToken).ConfigureAwait(false);
            await AuditAsync(SecurityAuditEventType.IntegrityBaselineEstablished, SecurityAuditLevel.Information,
                "Trusted", $"Explicit trusted baseline established for {entries.Count} files.",
                "Re-establish the baseline only after verifying a trusted installer or authorized repair source.",
                cancellationToken).ConfigureAwait(false);
            return new TrustedBaselineResult(true, "Trusted baseline established and sealed.", entries.Count,
                DateTimeOffset.UtcNow);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<StartupIntegrityReport> VerifyStartupAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await VerifyStartupCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileIntegrityRecord>> GetRecordsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_recordsPath)) return [];
        try
        {
            string json = await File.ReadAllTextAsync(_recordsPath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<IReadOnlyList<FileIntegrityRecord>>(json, JsonOptions) ?? [];
        }
        catch (JsonException exception)
        {
            throw new CryptographicException("FileIntegrityRecords is corrupt.", exception);
        }
    }

    /// <inheritdoc />
    public SecurityHealthScore CalculateHealthScore(SecurityHealthFactors factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        int integrity = factors.IntegrityValid ? 30 : 0;
        int permission = factors.PermissionSafe ? 20 : 0;
        int update = factors.UpdateTrusted ? 20 : 0;
        int plugin = factors.PluginSecurityValid ? 15 : 0;
        int encryption = factors.EncryptionHealthy ? 15 : 0;
        int score = integrity + permission + update + plugin + encryption;
        SecurityRiskLevel risk = score switch
        {
            100 => SecurityRiskLevel.Safe,
            >= 80 => SecurityRiskLevel.Notice,
            >= 50 => SecurityRiskLevel.Warning,
            _ => SecurityRiskLevel.Critical,
        };
        return new SecurityHealthScore(score, integrity, permission, update, plugin, encryption, risk);
    }
}