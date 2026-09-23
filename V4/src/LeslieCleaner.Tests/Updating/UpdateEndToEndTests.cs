using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Updating;
using LeslieCleaner.Tests.Support;
using Xunit.Abstractions;

namespace LeslieCleaner.Tests.Updating;

/// <summary>
/// 更新系统端到端实测：本机临时 HTTP 目录作"更新服务器"，发布 v1.0.0/v1.0.1，
/// 真实走通 检查 → 下载 → 校验 → 替换 → 重启 全流程。
/// </summary>
public sealed class UpdateEndToEndTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly ITestOutputHelper _output;
    private readonly HttpListener _httpListener;
    private readonly Task _serverLoop;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly string _serverDir;
    private readonly string _installDir;
    private readonly string _serverUrl = string.Empty;

    public UpdateEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
        _serverDir = Path.Combine(_workspace.RootDirectory, "server");
        _installDir = Path.Combine(_workspace.RootDirectory, "install");
        Directory.CreateDirectory(_serverDir);
        Directory.CreateDirectory(_installDir);

        // 端口绑定尝试逻辑
        HttpListener listener = null!;
        int port = GetFreePort();
        string prefix = $"http://127.0.0.1:{port}/";

        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                listener.Start();
                _httpListener = listener;
                _serverUrl = prefix;
                break;
            }
            catch
            {
                try { listener.Stop(); } catch { }
                try { listener.Close(); } catch { }
                port = GetFreePort();
                prefix = $"http://127.0.0.1:{port}/";
            }
        }

        if (_httpListener == null)
            throw new InvalidOperationException("无法绑定 HTTP 监听器");

        // v1.0.1 服务器内容
        File.WriteAllText(Path.Combine(_serverDir, "keep.dll"), "keep-content-v1");
        File.WriteAllText(Path.Combine(_serverDir, "app.dll"), "app-content-v1.0.1");
        File.WriteAllText(Path.Combine(_serverDir, "new-feature.dll"), "new-feature-binary");
        string manifest = JsonSerializer.Serialize(new
        {
            version = "1.0.1",
            releaseNotes = "端到端测试版本",
            files = new[]
            {
                Entry("keep.dll"),
                Entry("app.dll"),
                Entry("new-feature.dll"),
            },
        });
        File.WriteAllText(Path.Combine(_serverDir, "manifest.json"), manifest);

        // v1.0.0 本地安装（keep.dll 相同，app.dll 过旧，new-feature.dll 缺失）
        File.WriteAllText(Path.Combine(_installDir, "keep.dll"), "keep-content-v1");
        File.WriteAllText(Path.Combine(_installDir, "app.dll"), "app-content-v1.0.0");

        _serverLoop = RunServerLoopAsync(_serverCts.Token);
    }

    private async Task RunServerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _httpListener.GetContextAsync().WaitAsync(cancellationToken);
                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return; // Expected during shutdown
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                    continue;
                return;
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            string relativePath = (request.RawUrl?.TrimStart('/') ?? string.Empty).TrimStart('/');

            if (string.IsNullOrEmpty(relativePath))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            string fullPath = Path.Combine(_serverDir, relativePath);
            if (File.Exists(fullPath))
            {
                byte[] buffer = File.ReadAllBytes(fullPath);
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer);
                context.Response.Close();
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }
        catch (Exception)
        {
            try { context.Response.Close(); } catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _serverCts.Cancel();
            // GetContextAsync historically has no cancellation-token overload. Stop the listener
            // before awaiting the loop so teardown cannot deadlock after an otherwise passing test.
            try { _httpListener.Stop(); } catch { }
            try { _httpListener.Close(); } catch { }
            try { await _serverLoop.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (Exception exception) when (exception is OperationCanceledException or TimeoutException or
                                               ObjectDisposedException or HttpListenerException) { }
        }
        finally
        {
            _serverCts.Dispose();
            _workspace.Dispose();
        }
    }

    void IDisposable.Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Fact]
    public async Task FullUpdateFlow_CheckDownloadVerifyApplyRestart()
    {
        using var config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        await config.SetValueAsync(UpdateService.ServerUrlConfigKey, _serverUrl);
        await config.SetValueAsync(UpdateService.CurrentVersionConfigKey, "1.0.0");
        await config.SetValueAsync(UpdateService.RequireSignedManifestConfigKey, false);
        var service = new UpdateService(config, _workspace.PathService, _workspace.Log);

        // 1. 检查：版本比较 + 文件级增量
        UpdateCheckResult check = await service.CheckForUpdateAsync(_installDir);
        Assert.True(check.UpdateAvailable);
        Assert.Equal(2, check.FilesToDownload.Count);
        Assert.DoesNotContain(check.FilesToDownload, f => f.Path == "keep.dll");
        _output.WriteLine($"[E2E] 检查通过：{check.FilesToDownload.Count} 个变更文件");

        // 2. 下载 + SHA256 校验（真实 HTTP）
        string staging = await service.DownloadUpdateAsync(check.Manifest!, check.FilesToDownload);
        Assert.True(File.Exists(Path.Combine(staging, "app.dll")));
        _output.WriteLine($"[E2E] 下载校验通过：{staging}");

        // 3. 替换 + 重启：Updater 辅助进程（等待"主程序"退出 → 替换 → 重启）
        string markerBat = Path.Combine(_installDir, "restart-marker.bat");
        string markerTxt = Path.Combine(_installDir, "restarted.txt");
        File.WriteAllText(markerBat, $"@echo off\r\necho restarted>\"{markerTxt}\"\r\n");

        using var fakeMain = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-NoProfile -Command \"Start-Sleep -Seconds 2\"",
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        string updaterExe = FindUpdaterExe();
        using var updater = Process.Start(new ProcessStartInfo
        {
            FileName = updaterExe,
            Arguments = $"--pid {fakeMain.Id} --source \"{staging}\" --target \"{_installDir}\" --restart \"{markerBat}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        updater.StartInfo.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(Environment.ProcessPath)!;
        await updater.WaitForExitAsync();
        _output.WriteLine($"[E2E] Updater 退出码: {updater.ExitCode}, 输出: {await updater.StandardOutput.ReadToEndAsync()}");

        Assert.Equal(0, updater.ExitCode);
        Assert.Equal("app-content-v1.0.1", await File.ReadAllTextAsync(Path.Combine(_installDir, "app.dll")));
        Assert.True(File.Exists(Path.Combine(_installDir, "new-feature.dll")));
        Assert.False(Directory.Exists(staging)); // 暂存目录已清理
        _output.WriteLine("[E2E] 替换完成");

        // 4. 重启验证（marker bat 被执行）
        for (int i = 0; i < 40 && !File.Exists(markerTxt); i++)
        {
            await Task.Delay(250);
        }

        Assert.True(File.Exists(markerTxt), "重启标记未生成（restart 未执行）");
        _output.WriteLine("[E2E] 重启验证通过：检查→下载→校验→替换→重启 全流程走通");
    }

    private object Entry(string path)
    {
        string fullPath = Path.Combine(_serverDir, path);
        byte[] hash = SHA256.HashData(File.ReadAllBytes(fullPath));
        return new
        {
            path,
            sha256 = Convert.ToHexString(hash).ToLowerInvariant(),
            size = new FileInfo(fullPath).Length,
            url = path,
        };
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private void WaitForServerReady()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (int i = 0; i < 40; i++)
        {
            try
            {
                using HttpResponseMessage response = client.GetAsync(_serverUrl + "/manifest.json").Result;
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AggregateException)
            {
                // .Result 会把超时/连接拒绝包进 AggregateException，统一视为"服务器未就绪"重试
            }

            Thread.Sleep(250);
        }

        throw new InvalidOperationException("临时 HTTP 服务器未就绪");
    }

    private static string FindUpdaterExe()
    {
        // 测试输出目录（ProjectReference 会复制被引用 Exe 项目的 apphost）
        string candidate = Path.Combine(AppContext.BaseDirectory, "LeslieCleaner.Updater.exe");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        string publish = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "installer", "publish", "LeslieCleaner.Updater.exe"));
        if (File.Exists(publish))
        {
            return publish;
        }

        throw new FileNotFoundException("LeslieCleaner.Updater.exe 未找到");
    }
}
