using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Infrastructure.Diagnostics;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>日志文件行。</summary>
/// <param name="FilePath">完整路径。</param>
/// <param name="Name">文件名。</param>
/// <param name="SizeText">大小文本。</param>
/// <param name="ModifiedText">修改时间文本。</param>
public sealed record LogFileRow(string FilePath, string Name, string SizeText, string ModifiedText);

/// <summary>
/// 运行日志视图模型：日志文件列表 + 只读内容查看（级别筛选/关键词搜索）+
/// 打开日志目录 + 导出诊断报告。
/// </summary>
public sealed partial class LogsViewModel : ObservableObject, IPageActivatable
{
    private const long MaxDisplayBytes = 512 * 1024;

    private readonly IPathService _pathService;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly ILogService _logService;

    private string[] _allLines = [];

    [ObservableProperty]
    private LogFileRow? _selectedFile;

    [ObservableProperty]
    private string _contentText = "选择左侧日志文件查看内容";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _showInfo = true;

    [ObservableProperty]
    private bool _showWarning = true;

    [ObservableProperty]
    private bool _showError = true;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    /// <summary>日志文件列表。</summary>
    public ObservableCollection<LogFileRow> Files { get; } = [];

    /// <summary>以路径、诊断与日志服务创建视图模型。</summary>
    public LogsViewModel(IPathService pathService, DiagnosticsService diagnosticsService, ILogService logService)
    {
        _pathService = pathService;
        _diagnosticsService = diagnosticsService;
        _logService = logService;
    }

    /// <inheritdoc />
    public Task OnActivatedAsync() => LoadFilesAsync();

    /// <summary>加载日志文件列表（按修改时间倒序）。</summary>
    [RelayCommand]
    private async Task LoadFilesAsync()
    {
        Files.Clear();
        if (!Directory.Exists(_pathService.LogDirectory))
        {
            StatusText = "日志目录不存在（尚未产生日志）";
            return;
        }

        await Task.Run(() =>
        {
            foreach (string file in Directory.EnumerateFiles(_pathService.LogDirectory, "*.log")
                .OrderByDescending(static f => File.GetLastWriteTimeUtc(f)))
            {
                var info = new FileInfo(file);
                Files.Add(new LogFileRow(
                    file, info.Name,
                    $"{info.Length / 1024.0:F1} KB",
                    info.LastWriteTime.ToString("MM-dd HH:mm")));
            }
        });

        StatusText = $"{Files.Count} 个日志文件";
        if (Files.Count > 0 && SelectedFile is null)
        {
            SelectedFile = Files[0];
        }
    }

    /// <summary>选中文件变化时加载内容。</summary>
    partial void OnSelectedFileChanged(LogFileRow? value) => _ = LoadContentAsync();

    /// <summary>筛选/搜索条件变化时重新应用。</summary>
    partial void OnShowInfoChanged(bool value) => ApplyFilter();

    /// <summary>筛选变化。</summary>
    partial void OnShowWarningChanged(bool value) => ApplyFilter();

    /// <summary>筛选变化。</summary>
    partial void OnShowErrorChanged(bool value) => ApplyFilter();

    /// <summary>搜索变化。</summary>
    partial void OnSearchKeywordChanged(string value) => ApplyFilter();

    /// <summary>打开日志目录。</summary>
    [RelayCommand]
    private void OpenLogDirectory()
    {
        try
        {
            _pathService.EnsureDirectory(_pathService.LogDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_pathService.LogDirectory}\"",
                UseShellExecute = true,
            });
            _logService.LogEvent(Core.Models.LogEventKind.UserAction, "打开日志目录");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logService.LogException(ex, "LogsViewModel.OpenLogDirectory");
            StatusText = "打开失败（详见日志）";
        }
    }

    /// <summary>导出诊断报告到桌面。</summary>
    [RelayCommand]
    private async Task ExportDiagnosticsAsync()
    {
        try
        {
            StatusText = "正在生成诊断报告…";
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string zipPath = await _diagnosticsService.GenerateReportAsync(desktop);
            StatusText = $"诊断报告已导出：{zipPath}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "LogsViewModel.ExportDiagnosticsAsync");
            StatusText = "导出失败（详见日志）";
        }
    }

    private async Task LoadContentAsync()
    {
        if (SelectedFile is null)
        {
            return;
        }

        try
        {
            _allLines = await Task.Run(async () =>
            {
                var info = new FileInfo(SelectedFile.FilePath);
                long skip = Math.Max(0, info.Length - MaxDisplayBytes);
                await using var stream = new FileStream(SelectedFile.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                stream.Seek(skip, SeekOrigin.Begin);
                using var reader = new StreamReader(stream);
                if (skip > 0)
                {
                    _ = await reader.ReadLineAsync(); // 跳过被截断的首行
                }

                var lines = new List<string>();
                while (await reader.ReadLineAsync() is { } line)
                {
                    lines.Add(line);
                }

                return lines.ToArray();
            });
            ApplyFilter();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "LogsViewModel.LoadContentAsync");
            ContentText = "读取失败（详见日志）";
        }
    }

    private void ApplyFilter()
    {
        if (_allLines.Length == 0)
        {
            return;
        }

        string keyword = SearchKeyword.Trim();
        var output = new List<string>(_allLines.Length);
        bool includePrevious = true;
        foreach (string line in _allLines)
        {
            bool isRecordLine = line.Contains("[INF]") || line.Contains("[WRN]") || line.Contains("[ERR]") || line.Contains("[DBG]");
            if (isRecordLine)
            {
                // 记录行：级别筛选 + 关键词筛选
                bool levelPass = (line.Contains("[INF]") && ShowInfo)
                    || (line.Contains("[WRN]") && ShowWarning)
                    || (line.Contains("[ERR]") && ShowError)
                    || (line.Contains("[DBG]") && ShowInfo);
                includePrevious = levelPass &&
                    (keyword.Length == 0 || line.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }
            else if (includePrevious && keyword.Length > 0)
            {
                // 续行（异常堆栈/多行消息）：关键词模式下仅随命中记录展示
                includePrevious = true;
            }

            if (includePrevious)
            {
                output.Add(line);
            }
        }

        ContentText = output.Count == 0 ? "（无匹配记录）" : string.Join(Environment.NewLine, output);
    }
}
