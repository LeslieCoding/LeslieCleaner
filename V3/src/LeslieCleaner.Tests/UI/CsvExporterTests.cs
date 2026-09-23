using LeslieCleaner.Tests.Support;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.Tests.UI;

/// <summary>CSV 导出工具测试（转义/BOM/表头）。</summary>
public sealed class CsvExporterTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    /// <inheritdoc />
    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Write_EscapesSpecialFieldsAndWritesBom()
    {
        string path = Path.Combine(_workspace.RootDirectory, "out.csv");
        CsvExporter.Write(path, ["名称", "备注"],
        [
            ["plain", "with,comma"],
            ["quo\"te", ""],
        ]);

        byte[] raw = File.ReadAllBytes(path);
        Assert.True(raw.Length > 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF); // UTF-8 BOM

        string[] lines = File.ReadAllLines(path);
        Assert.Equal("名称,备注", lines[0]);
        Assert.Equal("plain,\"with,comma\"", lines[1]);
        Assert.Equal("\"quo\"\"te\",", lines[2]);
    }

    [Fact]
    public void Write_NewlineField_QuotedAsSingleField()
    {
        string path = Path.Combine(_workspace.RootDirectory, "out.csv");
        CsvExporter.Write(path, ["a"], [["line\nbreak"]]);
        string content = File.ReadAllText(path);
        Assert.Contains("\"line\nbreak\"", content);
    }

    [Fact]
    public void Write_EmptyField_StaysEmpty()
    {
        string path = Path.Combine(_workspace.RootDirectory, "out.csv");
        CsvExporter.Write(path, ["a", "b"], [["", "x"]]);
        Assert.Equal("a,b", File.ReadAllLines(path)[0]);
        Assert.Equal(",x", File.ReadAllLines(path)[1]);
    }
}
