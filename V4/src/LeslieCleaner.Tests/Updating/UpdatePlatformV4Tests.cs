using System.Net;
using System.Security.Cryptography;
using LeslieCleaner.Core.Threading;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Updating;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Updating;

public sealed class UpdatePlatformV4Tests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;

    public UpdatePlatformV4Tests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "update-v4.json"), _workspace.Log);
        _config.SetValueAsync(UpdateService.ServerUrlConfigKey, "https://updates.test").GetAwaiter().GetResult();
        _config.SetValueAsync(UpdateService.CurrentVersionConfigKey, "4.0.0").GetAwaiter().GetResult();
        _config.SetValueAsync(UpdateService.RequireSignedManifestConfigKey, false).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Check_ChannelAndIncrementalPackage_AreSelected()
    {
        await _config.SetValueAsync(UpdateService.ChannelConfigKey, "Beta");
        string hash = Sha256Hex("delta");
        var manifest = new UpdateManifest(
            "4.1.0",
            [new UpdateFileEntry("full.bin", Sha256Hex("full"), 4, "full.bin")],
            "beta",
            Channel: UpdateChannel.Beta,
            Packages:
            [
                new UpdatePackageDescriptor(UpdatePackageKind.Full, 100, Files:
                    [new UpdateFileEntry("full.bin", Sha256Hex("full"), 4, "full.bin")]),
                new UpdatePackageDescriptor(UpdatePackageKind.Incremental, 5, "4.0.0",
                    [new UpdateFileEntry("delta.bin", hash, 5, "delta.bin")]),
            ]);
        var service = CreateService(new FakeHttpHandler(_ =>
            FakeHttpHandler.JsonResponse(System.Text.Json.JsonSerializer.Serialize(manifest))));

        UpdateCheckResult result = await service.CheckForUpdateAsync(_workspace.RootDirectory);

        Assert.True(result.UpdateAvailable);
        Assert.Equal(UpdatePackageKind.Incremental, result.PackageKind);
        Assert.Single(result.FilesToDownload);
        Assert.Equal("delta.bin", result.FilesToDownload[0].Path);
    }

    [Fact]
    public void Compatibility_RejectsMissingDependencyAndWrongArchitecture()
    {
        var missing = new UpdateManifest("4.1.0", [], null, Compatibility:
            new UpdateCompatibility(RequiredFiles: ["required.dll"]));
        Assert.False(UpdateService.EvaluateCompatibility(
            missing, "4.0.0", _workspace.RootDirectory).IsCompatible);

        var architecture = new UpdateManifest("4.1.0", [], null, Compatibility:
            new UpdateCompatibility(Architectures: ["definitely-not-this-architecture"]));
        Assert.False(UpdateService.EvaluateCompatibility(
            architecture, "4.0.0", _workspace.RootDirectory).IsCompatible);
    }

    [Fact]
    public async Task Download_ExistingPartial_UsesRangeAndCompletes()
    {
        const string content = "resumable-payload";
        var entry = new UpdateFileEntry("app.bin", Sha256Hex(content), content.Length, "app.bin");
        var manifest = new UpdateManifest("4.1.0", [entry], null);
        string staging = Path.Combine(
            _workspace.PathService.DataDirectory, "UpdateStaging", manifest.Version);
        Directory.CreateDirectory(staging);
        await File.WriteAllTextAsync(Path.Combine(staging, "app.bin.part"), content[..6]);
        RangeHeaderValueCapture? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = new RangeHeaderValueCapture(request.Headers.Range?.Ranges.Single().From);
            return new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new StringContent(content[6..]),
            };
        });
        var service = CreateService(handler);

        string result = await service.DownloadUpdateAsync(manifest, [entry]);

        Assert.Equal(6, captured?.From);
        Assert.Equal(content, await File.ReadAllTextAsync(Path.Combine(result, "app.bin")));
        Assert.False(File.Exists(Path.Combine(result, "app.bin.part")));
    }

    [Fact]
    public async Task Download_Cancellation_IsPropagated()
    {
        const string content = "cancel-me";
        var entry = new UpdateFileEntry("cancel.bin", Sha256Hex(content), content.Length, "cancel.bin");
        var manifest = new UpdateManifest("4.1.1", [entry], null);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService(new FakeHttpHandler(_ => FakeHttpHandler.TextResponse(content)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DownloadUpdateAsync(manifest, [entry],
                new UpdateDownloadOptions(PauseToken.None, Timeout: TimeSpan.FromSeconds(5)),
                cancellation.Token));
    }

    [Fact]
    public async Task InstallTransaction_MigrationFailure_RestoresBackupAndReportsHistory()
    {
        var migration = new FailingMigration();
        var manifest = new UpdateManifest("4.1.0", [], null, Migrations:
            [new UpdateMigrationDescriptor("schema-4.1", "4.0.0", "4.1.0")]);
        var service = CreateService(new FakeHttpHandler(_ => FakeHttpHandler.JsonResponse("{}")));
        bool applied = false;

        UpdateTransactionResult result = await service.ExecuteInstallTransactionAsync(
            manifest, "4.0.0", UpdatePackageKind.Full, migration,
            _ =>
            {
                applied = true;
                return Task.CompletedTask;
            });

        Assert.False(result.Success);
        Assert.True(result.RolledBack);
        Assert.True(migration.Restored);
        Assert.False(applied);
        Assert.True(File.Exists(result.ReportPath));
        Assert.Contains(await service.GetHistoryAsync(), item =>
            item.NewVersion == "4.1.0" && item.Outcome == UpdateOutcome.RolledBack);
    }

    [Fact]
    public async Task Download_AbsoluteCrossOriginFileUrl_IsRejected()
    {
        const string content = "x";
        var entry = new UpdateFileEntry(
            "safe.bin", Sha256Hex(content), 1, "https://evil.test/payload.bin");
        var manifest = new UpdateManifest("4.2.0", [entry], null);
        var service = CreateService(new FakeHttpHandler(_ => FakeHttpHandler.TextResponse(content)));

        await Assert.ThrowsAsync<Core.Diagnostics.LeslieCleanerException>(
            () => service.DownloadUpdateAsync(manifest, [entry]));
    }

    private UpdateService CreateService(FakeHttpHandler handler) =>
        new(_config, _workspace.PathService, _workspace.Log, new HttpClient(handler));

    private static string Sha256Hex(string content) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)))
            .ToLowerInvariant();

    private sealed record RangeHeaderValueCapture(long? From);

    private sealed class FailingMigration : IUpdateDataMigration
    {
        public bool Restored { get; private set; }

        public Task<string> BackupAsync(
            string oldVersion,
            string newVersion,
            CancellationToken cancellationToken) => Task.FromResult("backup-1");

        public Task MigrateAsync(
            IReadOnlyList<UpdateMigrationDescriptor> migrations,
            CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("migration failed"));

        public Task<bool> VerifyAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task RestoreAsync(string backupId, CancellationToken cancellationToken)
        {
            Restored = true;
            return Task.CompletedTask;
        }
    }
}