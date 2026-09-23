using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Diagnostics;
using LeslieCleaner.Infrastructure.Updating;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Updating;

public sealed class UpdateDiagnosticsTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;

    public UpdateDiagnosticsTests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        Directory.CreateDirectory(_workspace.PathService.LogDirectory);
        string today = DateTime.Now.ToString("yyyyMMdd");
        File.WriteAllText(
            Path.Combine(_workspace.PathService.LogDirectory, $"lesliecleaner-{today}.log"),
            "2026-07-24 10:00:02.000 +08:00 [ERR] [Exception] [清理] 异常\n" +
            $"System.IO.IOException: C:\\Users\\{Environment.UserName}\\Secret\\file.dat 无法访问\n");
    }

    public void Dispose()
    {
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task EmitDiagnostics_WritesLocalFile_WithErrorSummary()
    {
        var service = CreateService(new FakeHttpHandler(_ => FakeHttpHandler.TextResponse("{}")));

        string json = await service.EmitDiagnosticsAsync("1.0.1", "1.0.2", string.Empty);

        string file = Path.Combine(_workspace.PathService.DataDirectory, "update-diagnostics.json");
        Assert.True(File.Exists(file));
        string written = await File.ReadAllTextAsync(file);
        Assert.Equal(json, written);
        Assert.Contains("System.IO.IOException", written);
        Assert.Contains("\"errors\":1", written.Replace(" ", string.Empty));
    }

    [Fact]
    public async Task EmitDiagnostics_PayloadContainsNoPrivacyFields()
    {
        var service = CreateService(new FakeHttpHandler(_ => FakeHttpHandler.TextResponse("{}")));

        string json = await service.EmitDiagnosticsAsync("1.0.1", "1.0.2", string.Empty);

        Assert.DoesNotContain(Environment.UserName, json);
        Assert.DoesNotContain(@"C:\", json);
        Assert.DoesNotContain("Secret", json);
        Assert.DoesNotContain(Environment.MachineName, json);
    }

    [Fact]
    public async Task EmitDiagnostics_UploadSuccess_Recorded()
    {
        var requests = new List<string>();
        var handler = new FakeHttpHandler(request =>
        {
            requests.Add(request.RequestUri!.ToString());
            return FakeHttpHandler.TextResponse("{}");
        });
        var service = CreateService(handler);

        await service.EmitDiagnosticsAsync("1.0.1", "1.0.2", "http://update.test");

        Assert.Single(requests);
        Assert.EndsWith("/diagnostics", requests[0]);
        Assert.True(_workspace.Log.HasEvent(Core.Models.LogEventKind.UserAction));
    }

    [Fact]
    public async Task EmitDiagnostics_UploadFailure_DegradesSilently()
    {
        var handler = new FakeHttpHandler(_ => new System.Net.Http.HttpResponseMessage(
            System.Net.HttpStatusCode.InternalServerError));
        var service = CreateService(handler);

        // 上送 500 不抛异常；本地存档仍生成
        string json = await service.EmitDiagnosticsAsync("1.0.1", "1.0.2", "http://update.test");

        Assert.False(string.IsNullOrEmpty(json));
        Assert.True(File.Exists(Path.Combine(_workspace.PathService.DataDirectory, "update-diagnostics.json")));
    }

    private UpdateService CreateService(FakeHttpHandler handler)
    {
        var diagnostics = new DiagnosticsService(_workspace.PathService, _config, _workspace.Log);
        return new UpdateService(_config, _workspace.PathService, _workspace.Log,
            new HttpClient(handler), diagnostics);
    }
}
