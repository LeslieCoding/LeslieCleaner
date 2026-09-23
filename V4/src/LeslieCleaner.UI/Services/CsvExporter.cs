using System.IO;
using System.Text;
using Microsoft.Win32;

namespace LeslieCleaner.UI.Services;

/// <summary>
/// CSV 导出工具（UTF-8 带 BOM，Excel 直开不乱码；字段含逗号/引号/换行自动转义）。
/// </summary>
public static class CsvExporter
{
    /// <summary>弹出保存对话框选择 CSV 路径；取消返回 <see langword="null"/>。</summary>
    /// <param name="suggestedFileName">建议文件名。</param>
    public static string? PickSavePath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出 CSV",
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = suggestedFileName,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>写 CSV 文件（含表头）。</summary>
    /// <param name="path">目标路径。</param>
    /// <param name="headers">表头。</param>
    /// <param name="rows">数据行（每行字段数应与表头一致）。</param>
    public static void Write(string path, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', headers.Select(Escape)));
        foreach (IReadOnlyList<string> row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(Escape)));
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Escape(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        return field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r')
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
    }
}
