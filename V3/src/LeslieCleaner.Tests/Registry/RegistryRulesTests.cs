using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Registry;

namespace LeslieCleaner.Tests.Registry;

public sealed class RegistryRulesTests
{
    private static readonly Func<string, bool> NothingExists = static _ => false;
    private static readonly Func<string, bool> EverythingExists = static _ => true;

    [Fact]
    public void UninstallEntry_MissingDisplayIcon_IsFinding()
    {
        RegistryFinding? finding = RegistryRules.EvaluateUninstallEntry(
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\DeadApp",
            "DeadApp", null, @"C:\Gone\app.exe,0", NothingExists);

        Assert.NotNull(finding);
        Assert.Equal(RegistryIssueKind.InvalidUninstallEntry, finding.Issue);
        Assert.Equal(@"C:\Gone\app.exe", finding.BrokenTarget);
    }

    [Fact]
    public void UninstallEntry_AllPathsExist_NoFinding()
    {
        RegistryFinding? finding = RegistryRules.EvaluateUninstallEntry(
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\LiveApp",
            "LiveApp", @"C:\Apps\Live", @"C:\Apps\Live\app.exe,0", EverythingExists);

        Assert.Null(finding);
    }

    [Fact]
    public void FileExtension_MissingProgId_IsSafeFinding()
    {
        RegistryFinding? finding = RegistryRules.EvaluateFileExtension(
            @"HKCR\.dead", "Dead.ProgId", NothingExists);

        Assert.NotNull(finding);
        Assert.Equal(RegistryIssueKind.InvalidFileExtension, finding.Issue);
        Assert.Equal(RegistryRiskLevel.Safe, finding.Risk);
    }

    [Fact]
    public void FileExtension_EmptyProgId_NoFinding()
    {
        Assert.Null(RegistryRules.EvaluateFileExtension(@"HKCR\.txt", null, NothingExists));
    }

    [Fact]
    public void InprocServer_MissingDll_IsFinding()
    {
        RegistryFinding? finding = RegistryRules.EvaluateInprocServer(
            @"HKCR\CLSID\{GUID}\InprocServer32", @"C:\Gone\server.dll", NothingExists);

        Assert.NotNull(finding);
        Assert.Equal(RegistryIssueKind.InvalidComServer, finding.Issue);
    }

    [Fact]
    public void InprocServer_NullPath_NoFinding()
    {
        Assert.Null(RegistryRules.EvaluateInprocServer(@"HKCR\CLSID\{GUID}\InprocServer32", null, NothingExists));
    }

    [Fact]
    public void InprocServer_BareModuleNameResolvedByLoaderSearch_NoFinding()
    {
        // COM 裸模块名（combase.dll）经 System32/SysWOW64 搜索解析，存在即有效（防误删系统 COM 注册）
        Func<string, bool> existsUnderSystem32 = path =>
            path.EndsWith("combase.dll", StringComparison.OrdinalIgnoreCase) &&
            path.Contains("System32", StringComparison.OrdinalIgnoreCase);

        Assert.Null(RegistryRules.EvaluateInprocServer(
            @"HKCR\CLSID\{GUID}\InprocServer32", "combase.dll", existsUnderSystem32));
    }

    [Fact]
    public void InprocServer_BareModuleNameTrulyMissing_IsFinding()
    {
        RegistryFinding? finding = RegistryRules.EvaluateInprocServer(
            @"HKCR\CLSID\{GUID}\InprocServer32", "nonexistent-module-xyz.dll", NothingExists);

        Assert.NotNull(finding);
        Assert.Equal(RegistryIssueKind.InvalidComServer, finding.Issue);
    }

    [Fact]
    public void Font_MissingFile_IsFinding()
    {
        RegistryFinding? finding = RegistryRules.EvaluateFont(
            @"HKLM\SOFTWARE\Fonts", "Dead Font (TrueType)", "deadfont.ttf", @"C:\Windows\Fonts", NothingExists);

        Assert.NotNull(finding);
        Assert.Equal(RegistryIssueKind.InvalidFontReference, finding.Issue);
        Assert.Equal(@"C:\Windows\Fonts\deadfont.ttf", finding.BrokenTarget);
    }

    [Fact]
    public void PathValue_CommandWithMissingExe_IsFinding()
    {
        RegistryFinding? finding = RegistryRules.EvaluatePathValue(
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "DeadApp",
            @"""C:\Gone\dead.exe"" --silent", NothingExists);

        Assert.NotNull(finding);
        Assert.Equal(RegistryIssueKind.InvalidPathValue, finding.Issue);
        Assert.Equal(@"C:\Gone\dead.exe", finding.BrokenTarget);
    }

    [Theory]
    [InlineData(@"""C:\Apps\app.exe"" /quiet", @"C:\Apps\app.exe")]
    [InlineData(@"C:\Apps\app.exe --flag", @"C:\Apps\app.exe")]
    [InlineData(@"%ProgramFiles%\App\run.exe", null)] // 取决于本机是否存在，此处只验证不为 null 时无引号
    public void ExtractExecutablePath_ParsesCommandLines(string commandLine, string? expected)
    {
        string? result = RegistryRules.ExtractExecutablePath(commandLine);

        if (expected is not null)
        {
            Assert.Equal(expected, result);
        }
        else
        {
            Assert.False(string.IsNullOrEmpty(result));
        }
    }

    [Theory]
    [InlineData("notacommand", null)]
    [InlineData(null, null)]
    public void ExtractExecutablePath_RejectsNonPaths(string? commandLine, string? expected)
    {
        Assert.Equal(expected, RegistryRules.ExtractExecutablePath(commandLine));
    }

    [Fact]
    public void StripIconIndex_RemovesIconSuffix()
    {
        Assert.Equal(@"C:\Apps\app.exe", RegistryRules.StripIconIndex(@"""C:\Apps\app.exe,0"""));
        Assert.Equal(@"C:\Apps\app.exe", RegistryRules.StripIconIndex(@"C:\Apps\app.exe,-12"));
        Assert.Equal(@"C:\Apps\app,plain.exe", RegistryRules.StripIconIndex(@"C:\Apps\app,plain.exe"));
    }

    [Fact]
    public void RiskScorer_UserProfilePath_IsSafe()
    {
        string target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gone", "app.exe");

        Assert.Equal(RegistryRiskLevel.Safe, RegistryRiskScorer.Score(target));
    }

    [Fact]
    public void RiskScorer_SystemPath_IsModerate()
    {
        string target = Path.Combine(
            Environment.GetEnvironmentVariable("ProgramFiles")!, "Gone", "app.exe");

        Assert.Equal(RegistryRiskLevel.Moderate, RegistryRiskScorer.Score(target));
    }

    [Fact]
    public void RiskScorer_Unknown_IsRisky()
    {
        Assert.Equal(RegistryRiskLevel.Risky, RegistryRiskScorer.Score(@"Q:\Unk\app.exe"));
        Assert.Equal(RegistryRiskLevel.Risky, RegistryRiskScorer.Score(null));
    }
}
