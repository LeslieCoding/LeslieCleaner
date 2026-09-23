using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LeslieCleaner.UI.Services;

/// <summary>可勾选行接口（批量选择助手的最小契约）。</summary>
public interface ISelectableRow : INotifyPropertyChanged
{
    /// <summary>是否勾选。</summary>
    bool IsChecked { get; set; }
}

/// <summary>
/// 批量选择助手：三态全选（未选/全选/半选）、全选/取消全选/反选、勾选计数。
/// 性能设计：单项勾选用 O(1) 计数器增量维护；批量操作先挂起逐行联动
/// （_suppress），置完标志后一次性重算并只发一轮通知，避免 10k 行 O(n²) 重算。
/// 列表保持虚拟化，未实现的容器不感知行级 PropertyChanged。
/// </summary>
public sealed class BatchSelectionHelper : ObservableObject
{
    private readonly List<ISelectableRow> _rows = [];
    private bool _suppress;
    private int _checkedCount;
    private bool? _selectAllState = false;

    /// <summary>创建助手并初始化三个批量命令。</summary>
    public BatchSelectionHelper()
    {
        SelectAllCommand = new RelayCommand(() => SetAll(true));
        DeselectAllCommand = new RelayCommand(() => SetAll(false));
        InvertSelectionCommand = new RelayCommand(Invert);
    }

    /// <summary>全选。</summary>
    public IRelayCommand SelectAllCommand { get; }

    /// <summary>取消全选。</summary>
    public IRelayCommand DeselectAllCommand { get; }

    /// <summary>反选。</summary>
    public IRelayCommand InvertSelectionCommand { get; }

    /// <summary>已勾选数。</summary>
    public int CheckedCount
    {
        get => _checkedCount;
        private set
        {
            if (SetProperty(ref _checkedCount, value))
            {
                OnPropertyChanged(nameof(HasChecked));
            }
        }
    }

    /// <summary>是否有勾选项（批量操作栏可见性）。</summary>
    public bool HasChecked => CheckedCount > 0;

    /// <summary>三态全选状态（<see langword="true"/> 全选 / <see langword="false"/> 未选 / <see langword="null"/> 半选）。</summary>
    public bool? SelectAllState
    {
        get => _selectAllState;
        private set
        {
            if (SetProperty(ref _selectAllState, value))
            {
                StateChangeNotifications++;
            }
        }
    }

    /// <summary>三态状态变化通知次数（性能测试断言用：批量操作应为 1）。</summary>
    public int StateChangeNotifications { get; private set; }

    /// <summary>挂接行集合（结果重建后调用；旧行自动退订，重新计数）。</summary>
    /// <param name="rows">当前展示的行。</param>
    public void Attach(IEnumerable<ISelectableRow> rows)
    {
        foreach (ISelectableRow row in _rows)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }

        _rows.Clear();
        _rows.AddRange(rows);
        foreach (ISelectableRow row in _rows)
        {
            row.PropertyChanged += OnRowPropertyChanged;
        }

        Recount();
    }

    /// <summary>全选/取消全选（挂起逐行联动，单批次通知）。</summary>
    /// <param name="isChecked">目标勾选状态。</param>
    public void SetAll(bool isChecked)
    {
        _suppress = true;
        try
        {
            foreach (ISelectableRow row in _rows)
            {
                row.IsChecked = isChecked;
            }
        }
        finally
        {
            _suppress = false;
        }

        Recount();
    }

    /// <summary>反选（挂起逐行联动，单批次通知）。</summary>
    public void Invert()
    {
        _suppress = true;
        try
        {
            foreach (ISelectableRow row in _rows)
            {
                row.IsChecked = !row.IsChecked;
            }
        }
        finally
        {
            _suppress = false;
        }

        Recount();
    }

    /// <summary>头部三态点击：非全选 → 全选；全选 → 全不选。</summary>
    public void ToggleAll() => SetAll(SelectAllState != true);

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // IsChecked 为行原生勾选属性名；Startup/Uninstaller 行沿用 IsSelected 命名（接口转发）
        if (_suppress || sender is not ISelectableRow row ||
            (e.PropertyName != nameof(ISelectableRow.IsChecked) && e.PropertyName != "IsSelected"))
        {
            return;
        }

        CheckedCount += row.IsChecked ? 1 : -1;
        UpdateState();
    }

    private void Recount()
    {
        CheckedCount = _rows.Count(static r => r.IsChecked);
        UpdateState();
    }

    private void UpdateState() =>
        SelectAllState = CheckedCount == 0 ? false : CheckedCount == _rows.Count ? true : null;
}
