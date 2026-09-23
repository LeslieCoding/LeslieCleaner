using LeslieCleaner.Modules.Startup;
using LeslieCleaner.Modules.Startup.Classification;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Startup;

public sealed class StartupClassifierTests
{
    [Fact]
    public void Classify_MicrosoftCompany_IsMicrosoftWindows()
    {
        StartupClassification result = StartupClassifier.Classify(
            "OneDrive", "Microsoft Corporation", null, false, @"C:\Apps\onedrive.exe");

        Assert.Equal(StartupSourceKind.MicrosoftWindows, result.Kind);
        Assert.Equal(DisableProtection.ConfirmSystemCritical, StartupClassifier.GetProtection(result.Kind));
    }

    [Fact]
    public void Classify_MicrosoftSigner_IsMicrosoftWindows()
    {
        StartupClassification result = StartupClassifier.Classify(
            "SecurityHealth", null, "Microsoft Windows", true, @"C:\Windows\System32\SecurityHealthSystray.exe");

        Assert.Equal(StartupSourceKind.MicrosoftWindows, result.Kind);
        Assert.True(result.Confidence >= 0.9);
    }

    [Fact]
    public void Classify_MicrosoftPathWithValidSignature_IsMicrosoftWindows()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "x.exe");

        StartupClassification result = StartupClassifier.Classify("x", null, null, true, path);

        Assert.Equal(StartupSourceKind.MicrosoftWindows, result.Kind);
    }

    [Theory]
    [InlineData("Intel Corporation")]
    [InlineData("NVIDIA Corporation")]
    [InlineData("Realtek Semiconductor")]
    [InlineData("Logitech Inc")]
    [InlineData("Dell Inc.")]
    [InlineData("HP Inc.")]
    [InlineData("Lenovo")]
    [InlineData("ASUSTeK Computer Inc.")]
    [InlineData("Micro-Star International")]
    [InlineData("Corsair Components")]
    [InlineData("Razer Inc.")]
    [InlineData("SteelSeries ApS")]
    public void Classify_DriverVendors_AreHardwareDriver(string publisher)
    {
        StartupClassification result = StartupClassifier.Classify(
            "vendor-tool", publisher, null, false, @"C:\Program Files\Vendor\tool.exe");

        Assert.Equal(StartupSourceKind.HardwareDriver, result.Kind);
        Assert.Equal(DisableProtection.ConfirmHardware, StartupClassifier.GetProtection(result.Kind));
    }

    [Fact]
    public void Classify_EmptyPublisherUnsignedNoSigner_IsUnknown()
    {
        StartupClassification result = StartupClassifier.Classify(
            "mystery", null, null, false, @"C:\Temp\mystery.exe");

        Assert.Equal(StartupSourceKind.StartupUnknown, result.Kind);
        Assert.Equal(DisableProtection.Allowed, StartupClassifier.GetProtection(result.Kind));
    }

    [Fact]
    public void Classify_NormalThirdParty_IsUserApplication()
    {
        StartupClassification result = StartupClassifier.Classify(
            "WeChat", "Tencent", "Tencent Technology", true, @"C:\Program Files (x86)\Tencent\WeChat\WeChat.exe");

        Assert.Equal(StartupSourceKind.UserApplication, result.Kind);
        Assert.Equal(DisableProtection.Allowed, StartupClassifier.GetProtection(result.Kind));
    }

    [Fact]
    public void Classify_UnsignedButKnownPublisher_IsUserApplication()
    {
        StartupClassification result = StartupClassifier.Classify(
            "Tool", "Small Vendor Inc", null, false, @"C:\Tools\tool.exe");

        Assert.Equal(StartupSourceKind.UserApplication, result.Kind);
    }

    [Fact]
    public void ImpactAssessor_HeavyProgram_HigherStars()
    {
        StartupImpactEstimate heavy = StartupImpactAssessor.Assess(
            "steam.exe", 200 * 1024 * 1024, StartupSourceKind.UserApplication, true);
        StartupImpactEstimate light = StartupImpactAssessor.Assess(
            "tiny.exe", 100 * 1024, StartupSourceKind.MicrosoftWindows, true);

        Assert.True(heavy.Stars > light.Stars);
        Assert.True(heavy.MemoryEstimateMB > light.MemoryEstimateMB);
        Assert.True(heavy.TimeEstimateSeconds > light.TimeEstimateSeconds);
    }

    [Fact]
    public void Advisor_SystemDefender_Forbids()
    {
        var classification = new StartupClassification(StartupSourceKind.MicrosoftWindows, ["厂商为 Microsoft"], 0.98);

        string advice = StartupAdvisor.Advise("Windows Defender SecurityHealth", classification, true);

        Assert.Contains("禁止关闭", advice);
        Assert.Contains("安全", advice);
    }

    [Fact]
    public void Advisor_Driver_Keeps()
    {
        var classification = new StartupClassification(StartupSourceKind.HardwareDriver, ["命中驱动/OEM 厂商名单：intel"], 0.9);

        string advice = StartupAdvisor.Advise("Intel Graphics", classification, true);

        Assert.Contains("建议保持", advice);
    }

    [Fact]
    public void Advisor_Updater_Closable()
    {
        var classification = new StartupClassification(StartupSourceKind.UserApplication, ["第三方来源"], 0.75);

        string advice = StartupAdvisor.Advise("Adobe Updater", classification, true);

        Assert.Contains("建议关闭", advice);
        Assert.Contains("仅更新服务", advice);
    }

    [Fact]
    public void Advisor_Unknown_WarnsRisk()
    {
        var classification = new StartupClassification(StartupSourceKind.StartupUnknown, ["发布商为空"], 0.85);

        string advice = StartupAdvisor.Advise("mystery", classification, false);

        Assert.Contains("谨慎处理", advice);
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SignatureVerifierTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly SignatureVerifier _verifier;

    public SignatureVerifierTests() => _verifier = new SignatureVerifier(_workspace.Log);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Verify_RealSignedMicrosoftFile_IsValidWithMicrosoftSigner()
    {
        // explorer.exe 为微软内嵌签名文件（catalog 签名的系统文件可能返回 NOSIGNATURE，
        // 分类层经路径规则覆盖；此处验证内嵌签名链路与 Signer 提取）
        string explorer = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        SignatureInfo info = await _verifier.VerifyAsync(explorer);

        Assert.True(info.IsValid, "explorer.exe 应具有有效微软签名");
        Assert.NotNull(info.SignerSubject);
        Assert.True(StartupClassifier.IsMicrosoftSigner(info.SignerSubject),
            $"签名者应为微软，实际: {info.SignerSubject}");
    }

    [Fact]
    public async Task Verify_UnsignedTempFile_IsInvalid()
    {
        string unsigned = _workspace.CreateFile("unsigned.exe", "MZ-not-a-real-exe");

        SignatureInfo info = await _verifier.VerifyAsync(unsigned);

        Assert.False(info.IsValid);
        Assert.Null(info.SignerSubject);
    }

    [Fact]
    public async Task Verify_MissingFile_IsInvalid()
    {
        SignatureInfo info = await _verifier.VerifyAsync(
            Path.Combine(_workspace.RootDirectory, "ghost.exe"));

        Assert.False(info.IsValid);
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class StartupDetailAggregationTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly StartupManagerEngine _engine;

    public StartupDetailAggregationTests() =>
        _engine = new StartupManagerEngine(_workspace.PathService, _workspace.Log);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task EnumerateDetailed_RealMachine_AllClassifiedWithReasons()
    {
        IReadOnlyList<StartupItemDetail> details = await _engine.EnumerateDetailedAsync();

        Assert.NotEmpty(details);
        Assert.All(details, d =>
        {
            Assert.NotEmpty(d.Classification.Reasons);
            Assert.InRange(d.Impact.Stars, 1, 5);
            Assert.NotEmpty(d.Advice);
        });

        // 统计聚合：四类计数之和等于总数
        int total = details.Count(d => d.Classification.Kind == StartupSourceKind.MicrosoftWindows)
            + details.Count(d => d.Classification.Kind == StartupSourceKind.HardwareDriver)
            + details.Count(d => d.Classification.Kind == StartupSourceKind.UserApplication)
            + details.Count(d => d.Classification.Kind == StartupSourceKind.StartupUnknown);
        Assert.Equal(details.Count, total);

        // 含真实文件项：有签名结果与 SHA256
        Assert.Contains(details, d => d.FilePath is not null && d.Sha256 is not null);
    }

    [Fact]
    public void ResolveExecutablePath_ParsesCommandLines()
    {
        var withQuotes = new StartupItem("1", "App", StartupItemKind.RegistryRun, "loc",
            @"""C:\Program Files\App\app.exe"" /silent", null, true, StartupImpact.Medium, "");
        var plain = new StartupItem("2", "App2", StartupItemKind.RegistryRun, "loc",
            @"C:\Apps\tool.exe --flag", null, true, StartupImpact.Medium, "");
        var none = new StartupItem("3", "App3", StartupItemKind.ScheduledTask, "loc", null, null, true, StartupImpact.Medium, "");

        Assert.Equal(@"C:\Program Files\App\app.exe", StartupManagerEngine.ResolveExecutablePath(withQuotes));
        Assert.Equal(@"C:\Apps\tool.exe", StartupManagerEngine.ResolveExecutablePath(plain));
        Assert.Null(StartupManagerEngine.ResolveExecutablePath(none));
    }
}
