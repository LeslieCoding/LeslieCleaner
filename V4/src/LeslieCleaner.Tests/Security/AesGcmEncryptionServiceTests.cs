using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Security;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class AesGcmEncryptionServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void EncryptDecrypt_RoundTripsAcrossServiceInstances()
    {
        var first = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        byte[] encrypted = first.Encrypt(Encoding.UTF8.GetBytes("sensitive backup metadata"));
        var second = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);

        Assert.Equal("sensitive backup metadata", Encoding.UTF8.GetString(second.Decrypt(encrypted)));
        string keyRing = File.ReadAllText(Path.Combine(_workspace.PathService.DataDirectory, "encryption-keyring.json"));
        Assert.DoesNotContain("sensitive backup metadata", keyRing, StringComparison.Ordinal);
        Assert.Contains("\"ProtectionScope\":\"LocalMachine\"", keyRing, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingCurrentUserKeyRing_IsMigratedWithoutChangingKeyId()
    {
        string dataDirectory = Directory.CreateDirectory(_workspace.PathService.DataDirectory).FullName;
        string keyRingPath = Path.Combine(dataDirectory, "encryption-keyring.json");
        string keyId = Guid.NewGuid().ToString("D");
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] entropy = "LeslieCleaner.V3.EncryptionKeyRing"u8.ToArray();
        try
        {
            string protectedKey = Convert.ToBase64String(ProtectedData.Protect(key, entropy,
                DataProtectionScope.CurrentUser));
            File.WriteAllText(keyRingPath, JsonSerializer.Serialize(new
            {
                ActiveKeyId = keyId,
                Keys = new[] { new { Id = keyId, ProtectedKey = protectedKey, CreatedUtc = DateTimeOffset.UtcNow } },
            }));

            var service = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
            byte[] encrypted = service.Encrypt(Encoding.UTF8.GetBytes("migrate me"));

            Assert.Equal("migrate me", Encoding.UTF8.GetString(service.Decrypt(encrypted)));
            using JsonDocument migrated = JsonDocument.Parse(File.ReadAllText(keyRingPath));
            JsonElement entry = migrated.RootElement.GetProperty("Keys")[0];
            Assert.Equal(keyId, entry.GetProperty("Id").GetString());
            Assert.Equal("LocalMachine", entry.GetProperty("ProtectionScope").GetString());
            byte[] machineProtectedKey = Convert.FromBase64String(entry.GetProperty("ProtectedKey").GetString()!);
            byte[] recovered = ProtectedData.Unprotect(machineProtectedKey, entropy, DataProtectionScope.LocalMachine);
            try
            {
                Assert.Equal(key, recovered);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(recovered);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public void Decrypt_TamperedPayload_Throws()
    {
        var service = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        byte[] encrypted = service.Encrypt([1, 2, 3, 4]);
        encrypted[^1] ^= 0x80;

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(encrypted));
    }

    [Fact]
    public async Task RotateAndRetire_ReencryptedPayloadRemainsReadable()
    {
        var service = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        byte[] oldPayload = service.Encrypt(Encoding.UTF8.GetBytes("rotate me"));
        KeyRotationResult rotation = await service.RotateKeyAsync();
        byte[] reencryptedPayload = service.Encrypt(service.Decrypt(oldPayload));

        await service.RetireKeyAsync(rotation.PreviousKeyId);

        Assert.Equal("rotate me", Encoding.UTF8.GetString(service.Decrypt(reencryptedPayload)));
        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(oldPayload));
    }
}
