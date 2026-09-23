using System.Security.Cryptography;
using System.Text;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Security;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class ProtectedArtifactKeyRotationServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Rotate_ReencryptsEveryArtifactBeforeRetiringOldKey()
    {
        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        var service = new ProtectedArtifactKeyRotationService(
            _workspace.PathService, encryption, _workspace.Log);
        string first = CreateEncryptedArtifact(encryption, "one.enc", "first secret");
        string second = CreateEncryptedArtifact(encryption, "two.enc", "second secret");
        byte[] oldPayload = await File.ReadAllBytesAsync(first);

        var result = await service.RotateAsync([first, second]);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.ReEncryptedArtifactCount);
        Assert.Equal("first secret", Encoding.UTF8.GetString(
            encryption.Decrypt(await File.ReadAllBytesAsync(first))));
        Assert.Equal("second secret", Encoding.UTF8.GetString(
            encryption.Decrypt(await File.ReadAllBytesAsync(second))));
        Assert.ThrowsAny<CryptographicException>(() => encryption.Decrypt(oldPayload));
    }

    [Fact]
    public async Task Rotate_InvalidArtifactFailsBeforeRotationAndPreservesEveryOriginal()
    {
        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        var service = new ProtectedArtifactKeyRotationService(
            _workspace.PathService, encryption, _workspace.Log);
        string valid = CreateEncryptedArtifact(encryption, "valid.enc", "valid secret");
        string invalid = Path.Combine(_workspace.PathService.DataDirectory, "invalid.enc");
        await File.WriteAllTextAsync(invalid, "not encrypted");
        byte[] originalValid = await File.ReadAllBytesAsync(valid);
        byte[] originalInvalid = await File.ReadAllBytesAsync(invalid);

        var result = await service.RotateAsync([valid, invalid]);

        Assert.False(result.Succeeded);
        Assert.Equal(originalValid, await File.ReadAllBytesAsync(valid));
        Assert.Equal(originalInvalid, await File.ReadAllBytesAsync(invalid));
        Assert.Equal("valid secret", Encoding.UTF8.GetString(encryption.Decrypt(originalValid)));
    }

    [Fact]
    public async Task Rotate_PreCanceledRequestDoesNotModifyArtifact()
    {
        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        var service = new ProtectedArtifactKeyRotationService(
            _workspace.PathService, encryption, _workspace.Log);
        string artifact = CreateEncryptedArtifact(encryption, "cancel.enc", "unchanged");
        byte[] original = await File.ReadAllBytesAsync(artifact);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RotateAsync([artifact], cancellation.Token));
        Assert.Equal(original, await File.ReadAllBytesAsync(artifact));
    }

    private string CreateEncryptedArtifact(AesGcmEncryptionService encryption,
        string fileName, string value)
    {
        string path = Path.Combine(_workspace.PathService.DataDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, encryption.Encrypt(Encoding.UTF8.GetBytes(value)));
        return path;
    }
}