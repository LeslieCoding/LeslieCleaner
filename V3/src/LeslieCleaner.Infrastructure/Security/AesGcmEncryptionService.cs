using System.Security.Cryptography;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>以 DPAPI 保护 AES-256 密钥环的本地数据加密服务。</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class AesGcmEncryptionService : IEncryptionService
{
    private static readonly byte[] Magic = "LCENC1"u8.ToArray();
    private static readonly byte[] Entropy = "LeslieCleaner.V3.EncryptionKeyRing"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _keyRingPath;
    private readonly ILogService _logService;
    private readonly object _sync = new();
    private KeyRing? _keyRing;

    /// <summary>以应用数据路径和日志服务创建加密服务。</summary>
    public AesGcmEncryptionService(IPathService pathService, ILogService logService)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        _logService = logService;
        _keyRingPath = Path.Combine(pathService.EnsureDirectory(pathService.DataDirectory), "encryption-keyring.json");
    }

    /// <inheritdoc />
    public byte[] Encrypt(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        lock (_sync)
        {
            KeyRing keyRing = LoadKeyRing();
            KeyEntry activeKey = GetActiveKey(keyRing);
            byte[] nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];
            byte[] ciphertext = new byte[data.Length];
            byte[] key = UnprotectKey(activeKey.ProtectedKey);
            try
            {
                using var aes = new AesGcm(key, tag.Length);
                aes.Encrypt(nonce, data, ciphertext, tag);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }

            byte[] keyId = Guid.Parse(activeKey.Id).ToByteArray();
            byte[] payload = new byte[Magic.Length + keyId.Length + nonce.Length + tag.Length + ciphertext.Length];
            int offset = 0;
            Buffer.BlockCopy(Magic, 0, payload, offset, Magic.Length);
            offset += Magic.Length;
            Buffer.BlockCopy(keyId, 0, payload, offset, keyId.Length);
            offset += keyId.Length;
            Buffer.BlockCopy(nonce, 0, payload, offset, nonce.Length);
            offset += nonce.Length;
            Buffer.BlockCopy(tag, 0, payload, offset, tag.Length);
            offset += tag.Length;
            Buffer.BlockCopy(ciphertext, 0, payload, offset, ciphertext.Length);
            return payload;
        }
    }

    /// <inheritdoc />
    public byte[] Decrypt(byte[] encryptedData)
    {
        ArgumentNullException.ThrowIfNull(encryptedData);
        lock (_sync)
        {
            int minimumLength = Magic.Length + 16 + AesGcm.NonceByteSizes.MaxSize + AesGcm.TagByteSizes.MaxSize;
            if (encryptedData.Length < minimumLength || !encryptedData.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            {
                throw new CryptographicException("密文格式无效。");
            }

            int offset = Magic.Length;
            string keyId = new Guid(encryptedData.AsSpan(offset, 16)).ToString("D");
            offset += 16;
            byte[] nonce = encryptedData[offset..(offset + AesGcm.NonceByteSizes.MaxSize)];
            offset += nonce.Length;
            byte[] tag = encryptedData[offset..(offset + AesGcm.TagByteSizes.MaxSize)];
            offset += tag.Length;
            byte[] ciphertext = encryptedData[offset..];
            KeyEntry key = LoadKeyRing().Keys.SingleOrDefault(entry => entry.Id.Equals(keyId, StringComparison.OrdinalIgnoreCase))
                ?? throw new CryptographicException("密文引用的密钥已不可用。");
            byte[] plaintext = new byte[ciphertext.Length];
            byte[] keyMaterial = UnprotectKey(key.ProtectedKey);
            try
            {
                using var aes = new AesGcm(keyMaterial, tag.Length);
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(keyMaterial);
            }

            return plaintext;
        }
    }

    /// <inheritdoc />
    public Task<KeyRotationResult> RotateKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            KeyRing keyRing = LoadKeyRing();
            string previousKeyId = keyRing.ActiveKeyId;
            var entry = CreateKeyEntry();
            keyRing = keyRing with { ActiveKeyId = entry.Id, Keys = [.. keyRing.Keys, entry] };
            SaveKeyRing(keyRing);
            _keyRing = keyRing;
            var result = new KeyRotationResult(previousKeyId, entry.Id, DateTimeOffset.UtcNow);
            _logService.LogEvent(LogEventKind.UserAction, "加密密钥已轮换", new Dictionary<string, object?>
            {
                ["PreviousKeyId"] = previousKeyId,
                ["ActiveKeyId"] = entry.Id,
            });
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task RetireKeyAsync(string keyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            KeyRing keyRing = LoadKeyRing();
            if (keyId.Equals(keyRing.ActiveKeyId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("不能淘汰当前活动密钥。");
            }

            int removed = keyRing.Keys.Count(entry => entry.Id.Equals(keyId, StringComparison.OrdinalIgnoreCase));
            if (removed != 1)
            {
                throw new KeyNotFoundException("要淘汰的密钥不存在。");
            }

            keyRing = keyRing with { Keys = [.. keyRing.Keys.Where(entry => !entry.Id.Equals(keyId, StringComparison.OrdinalIgnoreCase))] };
            SaveKeyRing(keyRing);
            _keyRing = keyRing;
            _logService.LogEvent(LogEventKind.UserAction, "加密密钥已淘汰", new Dictionary<string, object?> { ["KeyId"] = keyId });
            return Task.CompletedTask;
        }
    }

    private KeyRing LoadKeyRing()
    {
        if (_keyRing is not null)
        {
            return _keyRing;
        }

        if (!File.Exists(_keyRingPath))
        {
            KeyEntry initial = CreateKeyEntry();
            _keyRing = new KeyRing(initial.Id, [initial]);
            SaveKeyRing(_keyRing);
            return _keyRing;
        }

        _keyRing = JsonSerializer.Deserialize<KeyRing>(File.ReadAllText(_keyRingPath), JsonOptions)
            ?? throw new CryptographicException("加密密钥环无法解析。");
        _ = GetActiveKey(_keyRing);
        return _keyRing;
    }

    private void SaveKeyRing(KeyRing keyRing)
    {
        string temporaryPath = _keyRingPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(keyRing));
        File.Move(temporaryPath, _keyRingPath, overwrite: true);
    }

    private static KeyEntry CreateKeyEntry()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        try
        {
            return new KeyEntry(Guid.NewGuid().ToString("D"), Convert.ToBase64String(
                ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser)), DateTimeOffset.UtcNow);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static KeyEntry GetActiveKey(KeyRing keyRing) =>
        keyRing.Keys.SingleOrDefault(entry => entry.Id.Equals(keyRing.ActiveKeyId, StringComparison.OrdinalIgnoreCase))
        ?? throw new CryptographicException("密钥环缺少活动密钥。");

    private static byte[] UnprotectKey(string protectedKey) =>
        ProtectedData.Unprotect(Convert.FromBase64String(protectedKey), Entropy, DataProtectionScope.CurrentUser);

    private sealed record KeyRing(string ActiveKeyId, IReadOnlyList<KeyEntry> Keys);
    private sealed record KeyEntry(string Id, string ProtectedKey, DateTimeOffset CreatedUtc);
}
