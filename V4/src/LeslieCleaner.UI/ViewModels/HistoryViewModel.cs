using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Data;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>Operation log display row.</summary>
public sealed partial class HistoryLogRow : ObservableObject
{
    [ObservableProperty] private long _id;
    [ObservableProperty] private string? _module = string.Empty;
    [ObservableProperty] private string? _operation = string.Empty;
    [ObservableProperty] private string? _description = string.Empty;
    [ObservableProperty] private string? _level = string.Empty;
    [ObservableProperty] private string? _result = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private DateTimeOffset _createdTimeUtc;

    /// <summary>Display-friendly relative time.</summary>
    public string TimeDisplay => (DateTimeOffset.UtcNow - CreatedTimeUtc) switch
    {
        { TotalSeconds: < 60 } => "刚刚",
        { TotalSeconds: var s } when s < 3600 => $"{(int)s / 60} 分钟前",
        { TotalHours: var h } when h < 24 => $"{(int)h} 小时前",
        { TotalDays: var d } when d < 30 => $"{(int)d} 天前",
        _ => CreatedTimeUtc.ToString("yyyy-MM-dd HH:mm"),
    };

    /// <summary>Level display text.</summary>
    public string LevelDisplay => Level switch
    {
        "Info" => "信息",
        "Warning" => "警告",
        "Error" => "错误",
        _ => Level ?? string.Empty,
    };

    /// <summary>Level display color key.</summary>
    public string LevelBrushKey => Level switch
    {
        "Error" => "DangerBrush",
        "Warning" => "WarningBrush",
        _ => "SuccessBrush",
    };

    /// <summary>Convert internal row to display model.</summary>
    public static HistoryLogRow FromRow(DatabaseV2Repository.OperationLogRow row) => new()
    {
        Id = row.Id,
        Module = row.Module,
        Operation = row.Operation,
        Description = row.Description,
        Level = row.Level,
        Result = row.Result,
        ErrorMessage = row.ErrorMessage,
        CreatedTimeUtc = row.CreatedTimeUtc,
    };
}

/// <summary>
/// History Center: unified operation history with pagination, search, filter by level/module,
/// and prune capability. All pages bounded to prevent memory bloat.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject, IPageActivatable
{
    private readonly IDatabase _database;
    private readonly ILogService _logService;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "加载历史记录…";
    [ObservableProperty] private int _totalRecords;
    [ObservableProperty] private int _currentPage;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private string? _filterModule;
    [ObservableProperty] private bool _showInfo = true;
    [ObservableProperty] private bool _showWarning = true;
    [ObservableProperty] private bool _showError = true;

    /// <summary>All scanned records (for pagination display; not loaded in memory).</summary>
    public ObservableCollection<HistoryLogRow> Logs { get; } = [];

    /// <summary>Module counts for stats dashboard.</summary>
    public IReadOnlyDictionary<string, long> ModuleCounts => _moduleCounts;
    private Dictionary<string, long> _moduleCounts = new();

    /// <summary>Creates the history view model.</summary>
    public HistoryViewModel(IDatabase database, ILogService logService)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    /// <summary>Called when the page becomes active.</summary>
    public Task OnActivatedAsync() => LoadModuleStatsAsync();

    [RelayCommand]
    private async Task LoadModuleStatsAsync()
    {
        try
        {
            var rows = await _database.QueryAsync(
                "SELECT Module, COUNT(*) as cnt FROM OperationLogs GROUP BY Module ORDER BY cnt DESC;",
                static reader => (reader.GetString(0), reader.GetInt64(1)), null, CancellationToken.None)
                .ConfigureAwait(false);

            _moduleCounts = rows.ToDictionary(t => t.Item1, t => t.Item2);
            TotalRecords = checked((int)_moduleCounts.Values.Sum());
            StatusText = $"共 {TotalRecords} 条操作记录，来自 {_moduleCounts.Count} 个模块";
            _logService.LogEvent(Core.Models.LogEventKind.PerformanceMetrics, "历史记录模块统计加载完成",
                new Dictionary<string, object?> { ["Modules"] = _moduleCounts.Count, ["Records"] = TotalRecords });
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "HistoryViewModel.LoadModuleStatsAsync");
            StatusText = "加载模块统计失败（详见日志）";
        }
    }

    partial void OnSearchKeywordChanged(string value) => CurrentPage = 1;
    partial void OnFilterModuleChanged(string? value) => CurrentPage = 1;
    partial void OnShowInfoChanged(bool value) => CurrentPage = 1;
    partial void OnShowWarningChanged(bool value) => CurrentPage = 1;
    partial void OnShowErrorChanged(bool value) => CurrentPage = 1;

    [RelayCommand]
    private async Task LoadLogsAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            await Task.Yield();
            Logs.Clear();
            CurrentPage = 1;
            await LoadPageAsync();
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "HistoryViewModel.LoadLogsAsync");
            StatusText = "加载失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadNextPageAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await LoadPageAsync(page: CurrentPage + 1);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadPreviousPageAsync()
    {
        if (CurrentPage <= 1 || IsBusy) return;
        IsBusy = true;
        try
        {
            await LoadPageAsync(page: CurrentPage - 1);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PruneLogsAsync()
    {
        if (IsBusy || TotalRecords <= 500)
        {
            StatusText = "当前记录数不足 500，无需清理";
            return;
        }

        StatusText = "正在清理旧记录…";
        try
        {
            var repo = new DatabaseV2Repository(_database);
            int deleted = await repo.PruneOperationLogsAsync(keep: 500).ConfigureAwait(false);
            TotalRecords = Math.Max(0, TotalRecords - deleted);
            await LoadModuleStatsAsync();
            await LoadLogsAsync();
            StatusText = $"已清理 {deleted} 条旧记录，保留最近 500 条";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "HistoryViewModel.PruneLogsAsync");
            StatusText = "清理失败（详见日志）";
        }
    }

    private async Task LoadPageAsync(int? page = null)
    {
        int pageNumber = page ?? CurrentPage;
        var repo = new DatabaseV2Repository(_database);
        const int pageSize = 50;

        DatabasePage<DatabaseV2Repository.OperationLogRow> pageData;

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            pageData = await repo.SearchOperationLogsAsync(SearchKeyword.Trim(), pageNumber, pageSize).ConfigureAwait(false);
        }
        else
        {
            pageData = await repo.GetOperationLogsPageAsync(pageNumber, pageSize).ConfigureAwait(false);
        }

        CurrentPage = pageNumber;

        List<HistoryLogRow> filtered = pageData.Items
            .Select(static row => HistoryLogRow.FromRow(row))
            .Where(row =>
            {
                bool levelPass = (row.Level == "Info" && ShowInfo) ||
                                 (row.Level == "Warning" && ShowWarning) ||
                                 (row.Level == "Error" && ShowError);
                if (!levelPass) return false;

                if (FilterModule is not null && row.Module != FilterModule) return false;
                return true;
            })
            .ToList();

        foreach (var r in filtered)
        {
            Logs.Add(r);
        }

        TotalRecords = pageData.TotalCount > int.MaxValue ? int.MaxValue : (int)pageData.TotalCount;
        long total = _moduleCounts.Values.Any() ? _moduleCounts.Values.Sum() : TotalRecords;
        StatusText = $"显示第 {pageNumber} 页，共 {total} 条记录";
    }
}
