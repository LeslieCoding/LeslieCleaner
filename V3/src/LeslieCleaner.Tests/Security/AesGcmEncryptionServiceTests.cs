using System.Security.Cryptography;
using System.Text;
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
