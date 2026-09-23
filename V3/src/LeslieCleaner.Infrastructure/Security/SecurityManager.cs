using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>应用文件完整性基线、插件信任汇总和本地审计哈希链实现。</summary>
public sealed class SecurityManager : ISecurityManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IPluginManager _pluginManager;
    private readonly IPathService _pathService;
    private readonly ILogService _logService;
    private readonly IEncryptionService? _encryptionService;
    private readonly ISecurityOperationGate? _operationGate;
    private readonly IDatabase? _database;
    private readonly string _applicationPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>使用插件管理、路径和日志服务创建安全管理器。</summary>
    public SecurityManager(
        IPluginManager pluginManager,
        IPathService pathService,
        ILogService logService,
        IEncryptionService? encryptionService = null,
        ISecurityOperationGate? operationGate = null,
        IDatabase? database = null,
        string? applicationPath = null)
    {
        _pluginManager = pluginManager;
        _pathService = pathService;
        _logService = logService;
        _encryptionService = encryptionService;
        _operationGate = operationGate;
        _database = database;
        _applicationPath = applicationPath ?? Environment.ProcessPath ?? throw new InvalidOperationException("无法确定应用程序路径。");
    }

    /// <inheritdoc />
    public async Task<SecurityAssessment> AssessAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            bool applicationIntegrity = await VerifyApplicationIntegrityAsync(cancellationToken).ConfigureAwait(false);
            bool databaseIntegrity = await VerifyDatabaseIntegrityAsync(cancellationToken).ConfigureAwait(false);
            bool auditIntegrity;
            try
            {
                await UpgradeLegacyAuditSealsAsync(cancellationToken).ConfigureAwait(false);
                auditIntegrity = await VerifyAuditAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (CryptographicException exception)
            {
                _logService.LogException(exception, "SecurityManager.UpgradeLegacyAuditSealsAsync");
                auditIntegrity = false;
            }
            IReadOnlyList<PluginDescriptor> plugins = await _pluginManager.DiscoverAsync(cancellationToken).ConfigureAwait(false);
            int trusted = plugins.Count(static plugin => plugin.TrustStatus is PluginTrustStatus.Ready or PluginTrustStatus.Disabled);
            int rejected = plugins.Count - trusted;
            int score = Math.Max(0, 100 - (applicationIntegrity ? 0 : 40) - (databaseIntegrity ? 0 : 20) -
                (auditIntegrity ? 0 : 30) - rejected * 10);
            string summary = applicationIntegrity && databaseIntegrity && auditIntegrity && rejected == 0
                ? "应用、数据库完整性、审计链和插件信任状态正常。"
                : $"检测到安全风险：应用完整性={applicationIntegrity}，数据库完整性={databaseIntegrity}，审计链={auditIntegrity}，不受信任插件={rejected}。";
            await AppendAuditAsync("SecurityAssessment", summary, cancellationToken).ConfigureAwait(false);
            var assessment = new SecurityAssessment(applicationIntegrity, databaseIntegrity, auditIntegrity, trusted, rejected, score, summary, DateTimeOffset.UtcNow);
            _operationGate?.Apply(assessment);
            return assessment;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAuditAsync(CancellationToken cancellationToken = default)
    {
        string auditPath = GetAuditPath();
        if (!File.Exists(auditPath))
        {
            return true;
        }

        string previousHash = string.Empty;
        foreach (string line in await File.ReadAllLinesAsync(auditPath, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            AuditEntry? entry = JsonSerializer.Deserialize<AuditEntry>(line, JsonOptions);
            string expectedHash = entry is null ? string.Empty : ComputeHash(entry.TimestampUtc, entry.EventType, entry.Message, previousHash);
            if (entry is null || entry.PreviousHash != previousHash || entry.Hash != expectedHash ||
                !VerifySeal(entry.Seal, expectedHash))
            {
                return false;
            }

            previousHash = entry.Hash;
        }

        return true;
    }

    private async Task<bool> VerifyApplicationIntegrityAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_applicationPath))
        {
            _logService.LogEvent(LogEventKind.Exception, "安全基线目标不存在", new Dictionary<string, object?> { ["ApplicationPath"] = _applicationPath });
            return false;
        }

        string currentHash = await ComputeFileHashAsync(_applicationPath, cancellationToken).ConfigureAwait(false);
        string baselinePath = Path.Combine(_pathService.EnsureDirectory(_pathService.DataDirectory), "security-integrity-baseline.json");
        IntegrityBaselineDocument baselineDocument = await ReadBaselineDocumentAsync(baselinePath, cancellationToken)
            .ConfigureAwait(false);
        IntegrityBaseline? baseline = baselineDocument.Installations.FirstOrDefault(candidate =>
            candidate.ApplicationPath.Equals(_applicationPath, StringComparison.OrdinalIgnoreCase));
        if (baseline is null)
        {
            baseline = new IntegrityBaseline(_applicationPath, currentHash, DateTimeOffset.UtcNow);
            baselineDocument = baselineDocument with
            {
                Installations = [.. baselineDocument.Installations, baseline],
            };
            await WriteBaselineDocumentAsync(baselinePath, baselineDocument, cancellationToken).ConfigureAwait(false);
            return true;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(baseline.Sha256), Convert.FromHexString(currentHash));
        }
        catch (FormatException)
        {
            _logService.LogEvent(LogEventKind.Exception, "完整性基线哈希格式无效", new Dictionary<string, object?>
            {
                ["ApplicationPath"] = _applicationPath,
            });
            return false;
        }
    }

    private async Task<bool> VerifyDatabaseIntegrityAsync(CancellationToken cancellationToken)
    {
        if (_database is null)
        {
            return true;
        }

        try
        {
            return await _database.VerifyIntegrityAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logService.LogException(exception, "SecurityManager.VerifyDatabaseIntegrityAsync");
            return false;
        }
    }

    private async Task AppendAuditAsync(string eventType, string message, CancellationToken cancellationToken)
    {
        string auditPath = GetAuditPath();
        string previousHash = string.Empty;
        if (File.Exists(auditPath))
        {
            string? lastLine = (await File.ReadAllLinesAsync(auditPath, cancellationToken).ConfigureAwait(false))
                .LastOrDefault(static line => !string.IsNullOrWhiteSpace(line));
            if (lastLine is not null)
            {
                previousHash = JsonSerializer.Deserialize<AuditEntry>(lastLine, JsonOptions)?.Hash ?? string.Empty;
            }
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string hash = ComputeHash(now, eventType, message, previousHash);
        var entry = new AuditEntry(now, eventType, message, previousHash, hash, CreateSeal(hash));
        await File.AppendAllTextAsync(auditPath, JsonSerializer.Serialize(entry) + Environment.NewLine, cancellationToken).ConfigureAwait(false);
    }

    private async Task UpgradeLegacyAuditSealsAsync(CancellationToken cancellationToken)
    {
        if (_encryptionService is null)
        {
            return;
        }

        string auditPath = GetAuditPath();
        if (!File.Exists(auditPath))
        {
            return;
        }

        AuditEntry[] entries = (await File.ReadAllLinesAsync(auditPath, cancellationToken).ConfigureAwait(false))
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<AuditEntry>(line, JsonOptions)
                ?? throw new CryptographicException("安全审计记录无法解析。"))
            .ToArray();
        if (entries.All(static entry => entry.Seal is not null))
        {
            return;
        }

        string previousHash = string.Empty;
        foreach (AuditEntry entry in entries)
        {
            string expectedHash = ComputeHash(entry.TimestampUtc, entry.EventType, entry.Message, previousHash);
            if (entry.PreviousHash != previousHash || entry.Hash != expectedHash)
            {
                throw new CryptographicException("无法迁移已损坏的安全审计链。");
            }

            previousHash = entry.Hash;
        }

        string temporaryPath = auditPath + ".tmp";
        string content = string.Join(Environment.NewLine, entries.Select(entry => JsonSerializer.Serialize(
            entry with { Seal = CreateSeal(entry.Hash) }))) + Environment.NewLine;
        await File.WriteAllTextAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, auditPath, overwrite: true);
    }

    private string? CreateSeal(string hash) => _encryptionService is null
        ? null
        : Convert.ToBase64String(_encryptionService.Encrypt(Encoding.UTF8.GetBytes(hash)));

    private bool VerifySeal(string? seal, string expectedHash)
    {
        if (_encryptionService is null)
        {
            return seal is null;
        }

        if (string.IsNullOrWhiteSpace(seal))
        {
            return false;
        }

        try
        {
            string sealedHash = Encoding.UTF8.GetString(_encryptionService.Decrypt(Convert.FromBase64String(seal)));
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(sealedHash), Encoding.UTF8.GetBytes(expectedHash));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return false;
        }
    }

    private string GetAuditPath() => Path.Combine(_pathService.EnsureDirectory(_pathService.DataDirectory), "security-audit.jsonl");

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(filePath);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static string ComputeHash(DateTimeOffset timestampUtc, string eventType, string message, string previousHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{timestampUtc:O}|{eventType}|{message}|{previousHash}")));

    private static async Task<IntegrityBaselineDocument> ReadBaselineDocumentAsync(string baselinePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(baselinePath))
        {
            return new IntegrityBaselineDocument([]);
        }

        string content = await File.ReadAllTextAsync(baselinePath, cancellationToken).ConfigureAwait(false);
        IntegrityBaselineDocument? document = JsonSerializer.Deserialize<IntegrityBaselineDocument>(content, JsonOptions);
        if (document?.Installations is not null)
        {
            return document;
        }

        IntegrityBaseline? legacyBaseline = JsonSerializer.Deserialize<IntegrityBaseline>(content, JsonOptions);
        return legacyBaseline is null ? new IntegrityBaselineDocument([]) : new IntegrityBaselineDocument([legacyBaseline]);
    }

    private static async Task WriteBaselineDocumentAsync(
        string baselinePath,
        IntegrityBaselineDocument document,
        CancellationToken cancellationToken)
    {
        string temporaryPath = baselinePath + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(document), cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, baselinePath, overwrite: true);
    }

    private sealed record IntegrityBaseline(string ApplicationPath, string Sha256, DateTimeOffset CreatedUtc);
    private sealed record IntegrityBaselineDocument(IReadOnlyList<IntegrityBaseline> Installations);
    private sealed record AuditEntry(
        DateTimeOffset TimestampUtc,
        string EventType,
        string Message,
        string PreviousHash,
        string Hash,
        string? Seal = null);
}
