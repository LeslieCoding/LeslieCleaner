using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Infrastructure;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace LeslieCleaner.Tests.Security;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class EncryptedConfigServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly string _configPath;

    public EncryptedConfigServiceTests()
    {
        _configPath = Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json");
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task EncryptedConfig_PersistsWithoutPlaintextAndReloads()
    {
        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        using (var service = new JsonConfigService(_configPath, _workspace.Log, encryption))
        {
            await service.SetValueAsync("privacy:apiToken", "secret-value-must-not-be-on-disk");
        }

        string persisted = await File.ReadAllTextAsync(_configPath);
        Assert.Contains("lesliecleaner.config.aes-gcm.v1", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value-must-not-be-on-disk", persisted, StringComparison.Ordinal);

        var reloadedEncryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        using var reloaded = new JsonConfigService(_configPath, _workspace.Log, reloadedEncryption);
        Assert.Equal("secret-value-must-not-be-on-disk", reloaded.GetValue("privacy:apiToken", string.Empty));
    }

    [Fact]
    public async Task EncryptedConfig_TamperingIsRejected()
    {
        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        using (var service = new JsonConfigService(_configPath, _workspace.Log, encryption))
        {
            await service.SetValueAsync("security:mode", "strict");
        }

        string persisted = await File.ReadAllTextAsync(_configPath);
        int payloadOffset = persisted.IndexOf("Payload", StringComparison.Ordinal);
        persisted = persisted[..(payloadOffset + 16)] + "A" + persisted[(payloadOffset + 17)..];
        await File.WriteAllTextAsync(_configPath, persisted);

        using var reloaded = new JsonConfigService(_configPath, _workspace.Log,
            new AesGcmEncryptionService(_workspace.PathService, _workspace.Log));
        Assert.Equal("safe-default", reloaded.GetValue("security:mode", "safe-default"));
    }

    [Fact]
    public async Task PlaintextConfig_IsReadAndMigratedOnNextWrite()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        await File.WriteAllTextAsync(_configPath, "{\"ui\":{\"theme\":\"Dark\"}}");
        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        using var service = new JsonConfigService(_configPath, _workspace.Log, encryption);

        Assert.Equal("Dark", service.GetValue("ui:theme", string.Empty));
        await service.SetValueAsync("ui:scale", 125);

        string persisted = await File.ReadAllTextAsync(_configPath);
        Assert.Contains("lesliecleaner.config.aes-gcm.v1", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("\"theme\": \"Dark\"", persisted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InfrastructureRegistration_UsesEncryptedConfigurationStorage()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPathService>(_workspace.PathService);
        services.AddLeslieCleanerInfrastructure();
        using ServiceProvider provider = services.BuildServiceProvider();
        IConfigService config = provider.GetRequiredService<IConfigService>();

        await config.SetValueAsync("security:localSecret", "production-registration-secret");

        string persisted = await File.ReadAllTextAsync(_configPath);
        Assert.Contains("lesliecleaner.config.aes-gcm.v1", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("production-registration-secret", persisted, StringComparison.Ordinal);
    }
}
