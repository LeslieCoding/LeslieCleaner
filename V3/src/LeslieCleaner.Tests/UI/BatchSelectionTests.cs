using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.Tests.UI;

/// <summary>
/// 批量选择助手测试：三态状态机、单选 O(1) 联动、万级批量操作性能与通知次数。
/// </summary>
public sealed partial class BatchSelectionTests
{
    /// <summary>测试用可勾选行。</summary>
    private sealed partial class TestRow : ObservableObject, ISelectableRow
    {
        [ObservableProperty]
        private bool _isChecked;
    }

    private static List<TestRow> CreateRows(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => new TestRow())];

    [Fact]
    public void SelectAll_AllChecked_StateTrue()
    {
        var helper = new BatchSelectionHelper();
        var rows = CreateRows(100);
        helper.Attach(rows);

        helper.SetAll(true);

        Assert.True(helper.SelectAllState);
        Assert.Equal(100, helper.CheckedCount);
        Assert.True(helper.HasChecked);
        Assert.All(rows, r => Assert.True(r.IsChecked));
    }

    [Fact]
    public void PartialCheck_StateIndeterminate()
    {
        var helper = new BatchSelectionHelper();
        var rows = CreateRows(10);
        helper.Attach(rows);
        helper.SetAll(true);

        int notificationsBefore = helper.StateChangeNotifications;
        rows[3].IsChecked = false; // 单项取消：O(1) 增量联动

        Assert.Null(helper.SelectAllState); // 半选
        Assert.Equal(9, helper.CheckedCount);
        Assert.Equal(notificationsBefore + 1, helper.StateChangeNotifications);
    }

    [Fact]
    public void InvertSelection_TogglesEveryRow()
    {
        var helper = new BatchSelectionHelper();
        var rows = CreateRows(10);
        helper.Attach(rows);
        rows[0].IsChecked = true;

        helper.Invert();

        Assert.False(rows[0].IsChecked);
        Assert.Equal(9, rows.Count(r => r.IsChecked));
        Assert.Equal(9, helper.CheckedCount);
    }

    [Fact]
    public void Attach_ReplacesRowsAndRecounts()
    {
        var helper = new BatchSelectionHelper();
        var first = CreateRows(5);
        helper.Attach(first);
        helper.SetAll(true);
        Assert.Equal(5, helper.CheckedCount);

        var second = CreateRows(3);
        helper.Attach(second);
        Assert.Equal(0, helper.CheckedCount);
        Assert.False(helper.SelectAllState);

        // 旧行退订：改旧行不再影响计数
        first[0].IsChecked = false;
        Assert.Equal(0, helper.CheckedCount);
    }

    [Fact]
    public void DeselectAll_StateFalse()
    {
        var helper = new BatchSelectionHelper();
        var rows = CreateRows(10);
        helper.Attach(rows);
        helper.SetAll(true);

        helper.SetAll(false);

        Assert.False(helper.SelectAllState);
        Assert.Equal(0, helper.CheckedCount);
        Assert.False(helper.HasChecked);
    }

    [Fact]
    public void BulkOperations_TenThousandRows_FastAndSingleNotification()
    {
        var helper = new BatchSelectionHelper();
        var rows = CreateRows(10_000);
        helper.Attach(rows);

        int baseline = helper.StateChangeNotifications;
        var watch = Stopwatch.StartNew();
        helper.SetAll(true);
        watch.Stop();
        long selectAllMs = watch.ElapsedMilliseconds;

        Assert.Equal(10_000, helper.CheckedCount);
        Assert.Equal(baseline + 1, helper.StateChangeNotifications); // 单批次只发一次三态通知
        Assert.True(selectAllMs < 1000, $"全选 10000 行耗时 {selectAllMs}ms 超阈值");

        int beforeInvert = helper.StateChangeNotifications;
        watch.Restart();
        helper.Invert();
        watch.Stop();
        long invertMs = watch.ElapsedMilliseconds;

        Assert.Equal(0, helper.CheckedCount);
        Assert.Equal(beforeInvert + 1, helper.StateChangeNotifications);
        Assert.True(invertMs < 1000, $"反选 10000 行耗时 {invertMs}ms 超阈值");
        Console.WriteLine($"[Perf] 10000 行全选 {selectAllMs}ms / 反选 {invertMs}ms / 单次三态通知各 1 次");
    }
}
