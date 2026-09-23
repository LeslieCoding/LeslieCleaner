using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>Append-only, SHA-256 chained store for unified security audit events.</summary>
public sealed class SecurityAuditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates an audit store in the protected application data directory.</summary>
    public SecurityAuditStore(IPathService pathService)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        _path = Path.Combine(pathService.EnsureDirectory(pathService.DataDirectory), "security-events-v4.jsonl");
    }

    /// <summary>Appends one structured event to the tamper-evident chain.</summary>
    public async Task AppendAsync(SecurityAuditEvent securityEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string previousHash = string.Empty;
            if (File.Exists(_path))
            {
                string? last = (await File.ReadAllLinesAsync(_path, cancellationToken).ConfigureAwait(false))
                    .LastOrDefault(static line => !string.IsNullOrWhiteSpace(line));
                if (last is not null) previousHash = JsonSerializer.Deserialize<Entry>(last, JsonOptions)?.Hash ?? string.Empty;
            }

            string payload = JsonSerializer.Serialize(securityEvent);
            string hash = Hash(payload, previousHash);
            var entry = new Entry(securityEvent, previousHash, hash);
            await File.AppendAllTextAsync(_path, JsonSerializer.Serialize(entry) + Environment.NewLine, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Reads and verifies the entire chain, failing closed if it was modified.</summary>
    public async Task<IReadOnlyList<SecurityAuditEvent>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path)) return [];
            string previousHash = string.Empty;
            var events = new List<SecurityAuditEvent>();
            foreach (string line in await File.ReadAllLinesAsync(_path, cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                Entry entry = JsonSerializer.Deserialize<Entry>(line, JsonOptions)
                    ?? throw new CryptographicException("Security audit event could not be parsed.");
                string expected = Hash(JsonSerializer.Serialize(entry.Event), previousHash);
                if (!string.Equals(entry.PreviousHash, previousHash, StringComparison.Ordinal) ||
                    !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(entry.Hash), Encoding.ASCII.GetBytes(expected)))
                    throw new CryptographicException("Security audit event chain integrity verification failed.");
                events.Add(entry.Event);
                previousHash = entry.Hash;
            }

            return events;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Hash(string payload, string previousHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload + "|" + previousHash)));

    private sealed record Entry(SecurityAuditEvent Event, string PreviousHash, string Hash);
}
