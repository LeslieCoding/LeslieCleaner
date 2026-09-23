using System.Security.Cryptography;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Updating;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Updating;

public sealed class UpdateServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;

    public UpdateServiceTests() =>
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);

    public void Dispose()
    {
        _config.Dispose();
        _workspace.Dispose();
    }

    [Theory]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0", "1.0.0", 0)]
    [InlineData("2.0", "1.9.9", 1)]
    [InlineData("1.0.0.1", "1.0.0", 1)]
    public void CompareVersions_Semantic(string a, string b, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(UpdateService.CompareVersions(a, b)));
    }

    [Fact]
    public async Task CheckForUpdate_ServerHasNewer_FindsDeltaFiles()
    {
        string localDir = Path.Combine(_workspace.RootDirectory, "install");
        Directory.CreateDirectory(localDir);
        string unchangedContent = "same-content";
        File.WriteAllText(Path.Combine(localDir, "keep.dll"), unchangedContent);
        File.WriteAllText(Path.Combine(localDir, "old.dll"), "outdated-content");

        string keepHash = await Sha256Hex(Path.Combine(localDir, "keep.dll"));
        var handler = new FakeHttpHandler(_ =>
            FakeHttpHandler.JsonResponse($$"""
                {"version":"1.0.1","files":[
                    {"path":"keep.dll","sha256":"{{keepHash}}","size":12,"url":"files/keep.dll"},
                    {"path":"old.dll","sha256":"0000000000000000000000000000000000000000000000000000000000000000","size":10,"url":"files/old.dll"},
                    {"path":"new.dll","sha256":"1111111111111111111111111111111111111111111111111111111111111111","size":8,"url":"files/new.dll"}
                ]}
                """));
        var service = CreateService(handler, serverUrl: "http://update.test", currentVersion: "1.0.0");

        UpdateCheckResult result = await service.CheckForUpdateAsync(localDir);

        Assert.True(result.UpdateAvailable);
        Assert.Equal(2, result.FilesToDownload.Count);
        Assert.DoesNotContain(result.FilesToDownload, f => f.Path == "keep.dll");
        Assert.Contains(result.FilesToDownload, f => f.Path == "old.dll");
        Assert.Contains(result.FilesToDownload, f => f.Path == "new.dll");
    }

    [Fact]
    public async Task CheckForUpdate_SameVersion_NoUpdate()
    {
        var handler = new FakeHttpHandler(_ =>
            FakeHttpHandler.JsonResponse("""{"version":"1.0.0","files":[]}"""));
        var service = CreateService(handler, serverUrl: "http://update.test", currentVersion: "1.0.0");

        UpdateCheckResult result = await service.CheckForUpdateAsync(_workspace.RootDirectory);

        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_UnsignedManifest_IsRejectedByDefault()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.JsonResponse(
            "{\"version\":\"1.0.1\",\"files\":[]}"));
        await _config.SetValueAsync(UpdateService.ServerUrlConfigKey, "http://update.test");
        await _config.SetValueAsync(UpdateService.CurrentVersionConfigKey, "1.0.0");
        var service = new UpdateService(_config, _workspace.PathService, _workspace.Log, new HttpClient(handler));

        UpdateCheckResult result = await service.CheckForUpdateAsync(_workspace.RootDirectory);

        Assert.False(result.UpdateAvailable);
        Assert.Null(result.Manifest);
    }

    [Fact]
    public async Task DownloadUpdate_RequiresValidEcdsaManifestSignature()
    {
        const string content = "signed-update";
        string source = Path.Combine(_workspace.RootDirectory, "signed.dll");
        await File.WriteAllTextAsync(source, content);
        string hash = await Sha256Hex(source);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var unsigned = new UpdateManifest("1.0.1",
            [new UpdateFileEntry("signed.dll", hash, content.Length, "files/signed.dll")], null,
            SigningKeyId: "test-key");
        UpdateManifest signed = unsigned with
        {
            Signature = Convert.ToBase64String(signer.SignData(
                UpdateService.GetManifestSigningPayload(unsigned), HashAlgorithmName.SHA256)),
        };
        var handler = new FakeHttpHandler(request => request.RequestUri!.AbsolutePath.EndsWith("signed.dll")
            ? FakeHttpHandler.TextResponse(content)
            : FakeHttpHandler.JsonResponse("{}"));
        await _config.SetValueAsync(UpdateService.ServerUrlConfigKey, "http://update.test");
        await _config.SetValueAsync(UpdateService.RequireSignedManifestConfigKey, true);
        await _config.SetValueAsync(UpdateService.SigningPublicKeyConfigKey,
            Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo()));
        var service = new UpdateService(_config, _workspace.PathService, _workspace.Log, new HttpClient(handler));

        string staging = await service.DownloadUpdateAsync(signed, signed.Files);
        Assert.True(File.Exists(Path.Combine(staging, "signed.dll")));

        await Assert.ThrowsAsync<Core.Diagnostics.LeslieCleanerException>(() =>
            service.DownloadUpdateAsync(signed with { Version = "1.0.2" }, signed.Files));
    }

    [Fact]
    public async Task DownloadUpdate_HashMismatch_CleansStagingAndThrows()
    {
        string stagingRoot = _workspace.PathService.DataDirectory;
        var handler = new FakeHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("bad.dll"))
            {
                return FakeHttpHandler.TextResponse("tampered-content");
            }

            return FakeHttpHandler.JsonResponse("""{"version":"1.0.1","files":[]}""");
        });
        var service = CreateService(handler, serverUrl: "http://update.test", currentVersion: "1.0.0");
        var manifest = new UpdateManifest("1.0.1",
            [new UpdateFileEntry("bad.dll", new string('0', 64), 16, "files/bad.dll")], null);

        await Assert.ThrowsAsync<Core.Diagnostics.LeslieCleanerException>(
            () => service.DownloadUpdateAsync(manifest, manifest.Files));

        Assert.False(Directory.Exists(Path.Combine(stagingRoot, "UpdateStaging", "1.0.1")));
    }

    [Fact]
    public async Task DownloadUpdate_ValidFiles_StagedAndVerified()
    {
        const string content = "real-update-payload";
        string tempFile = Path.Combine(_workspace.RootDirectory, "tmp.dll");
        File.WriteAllText(tempFile, content);
        string hash = await Sha256Hex(tempFile);

        var handler = new FakeHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("good.dll"))
            {
                return FakeHttpHandler.TextResponse(content);
            }

            return FakeHttpHandler.JsonResponse("""{"version":"1.0.1","files":[]}""");
        });
        var service = CreateService(handler, serverUrl: "http://update.test", currentVersion: "1.0.0");
        var manifest = new UpdateManifest("1.0.1", [new UpdateFileEntry("good.dll", hash, content.Length, "files/good.dll")], null);

        string staging = await service.DownloadUpdateAsync(manifest, manifest.Files);

        Assert.True(Directory.Exists(staging));
        Assert.Equal(content, await File.ReadAllTextAsync(Path.Combine(staging, "good.dll")));
        Assert.True(File.Exists(Path.Combine(staging, "manifest.json")));
    }

    [Fact]
    public async Task DownloadUpdate_PathTraversalIsRejectedBeforeWritingOutsideStaging()
    {
        string outsidePath = Path.Combine(_workspace.RootDirectory, "outside.dll");
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.TextResponse("unexpected"));
        var service = CreateService(handler, serverUrl: "http://update.test", currentVersion: "1.0.0");
        var manifest = new UpdateManifest("1.0.1",
            [new UpdateFileEntry("..\\outside.dll", new string('0', 64), 1, "files/outside.dll")], null);

        await Assert.ThrowsAsync<Core.Diagnostics.LeslieCleanerException>(
            () => service.DownloadUpdateAsync(manifest, manifest.Files));

        Assert.False(File.Exists(outsidePath));
        Assert.False(Directory.Exists(Path.Combine(_workspace.PathService.DataDirectory, "UpdateStaging", "1.0.1")));
    }

    [Fact]
    public async Task CheckForUpdate_UnsafeManifestIsRejected()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.JsonResponse("""
            {"version":"1.0.1","files":[{"path":"../outside.dll","sha256":"0000000000000000000000000000000000000000000000000000000000000000","size":1,"url":"outside.dll"}]}
            """));
        var service = CreateService(handler, serverUrl: "http://update.test", currentVersion: "1.0.0");

        UpdateCheckResult result = await service.CheckForUpdateAsync(_workspace.RootDirectory);

        Assert.False(result.UpdateAvailable);
        Assert.Null(result.Manifest);
    }

    [Fact]
    public void Updater_ReplaceFiles_CopiesAndSkipsManifest()
    {
        string source = Path.Combine(_workspace.RootDirectory, "staging");
        string target = Path.Combine(_workspace.RootDirectory, "install");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source, "app.dll"), "new-binary");
        File.WriteAllText(Path.Combine(source, "manifest.json"), "{}");
        File.WriteAllText(Path.Combine(target, "app.dll"), "old-binary");

        int replaced = LeslieCleaner.Updater.Program.ReplaceFiles(source, target);

        Assert.Equal(1, replaced);
        Assert.Equal("new-binary", File.ReadAllText(Path.Combine(target, "app.dll")));
    }

    [Fact]
    public void Updater_ReplaceFiles_CommitsExistingAndNewFilesAfterSuccess()
    {
        string source = Path.Combine(_workspace.RootDirectory, "commit-staging");
        string target = Path.Combine(_workspace.RootDirectory, "commit-install");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source, "existing.dll"), "new-existing");
        File.WriteAllText(Path.Combine(source, "nested", "added.dll"), "new-added");
        File.WriteAllText(Path.Combine(target, "existing.dll"), "old-existing");

        int replaced = LeslieCleaner.Updater.Program.ReplaceFiles(source, target);

        Assert.Equal(2, replaced);
        Assert.Equal("new-existing", File.ReadAllText(Path.Combine(target, "existing.dll")));
        Assert.Equal("new-added", File.ReadAllText(Path.Combine(target, "nested", "added.dll")));
    }

    [Fact]
    public void Updater_ReplaceFiles_MidwayFailureRestoresExistingAndDeletesNewFiles()
    {
        string source = Path.Combine(_workspace.RootDirectory, "rollback-staging");
        string target = Path.Combine(_workspace.RootDirectory, "rollback-install");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source, "a-existing.dll"), "new-a");
        File.WriteAllText(Path.Combine(source, "b-added.dll"), "new-b");
        File.WriteAllText(Path.Combine(source, "c-existing.dll"), "new-c");
        File.WriteAllText(Path.Combine(target, "a-existing.dll"), "old-a");
        File.WriteAllText(Path.Combine(target, "c-existing.dll"), "old-c");

        IOException error = Assert.Throws<IOException>(() =>
            LeslieCleaner.Updater.Program.ReplaceFiles(source, target, (_, completed) =>
            {
                if (completed == 2)
                {
                    throw new IOException("injected failure");
                }
            }));

        Assert.Equal("injected failure", error.Message);
        Assert.Equal("old-a", File.ReadAllText(Path.Combine(target, "a-existing.dll")));
        Assert.Equal("old-c", File.ReadAllText(Path.Combine(target, "c-existing.dll")));
        Assert.False(File.Exists(Path.Combine(target, "b-added.dll")));
        Assert.Empty(Directory.EnumerateFiles(target, "*.update-tmp-*", SearchOption.AllDirectories));
    }
    private UpdateService CreateService(FakeHttpHandler handler, string serverUrl, string currentVersion)
    {
        _ = _config.SetValueAsync(UpdateService.ServerUrlConfigKey, serverUrl);
        _ = _config.SetValueAsync(UpdateService.CurrentVersionConfigKey, currentVersion);
        _ = _config.SetValueAsync(UpdateService.RequireSignedManifestConfigKey, false);
        return new UpdateService(_config, _workspace.PathService, _workspace.Log, new HttpClient(handler));
    }

    private static async Task<string> Sha256Hex(string path)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
        byte[] hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>娴嬭瘯 HTTP 澶勭悊鍣紙娉ㄥ叆鍋囨湇鍔″櫒鍝嶅簲锛夈€?/summary>
public sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(responder(request));

    public static HttpResponseMessage JsonResponse(string json) => TextResponse(json);

    public static HttpResponseMessage TextResponse(string content) =>
        new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/octet-stream"),
        };
}

