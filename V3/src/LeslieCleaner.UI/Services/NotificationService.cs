using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.UI.Services;

/// <summary>应用通知（扫描完成/错误/恢复/更新/可撤销操作）。</summary>
public sealed partial class AppNotification : ObservableObject
{
    /// <summary>标题。</summary>
    public required string Title { get; init; }

    /// <summary>正文。</summary>
    public required string Body { get; init; }

    /// <summary>类别（Scan/Error/Restore/Update/Maintenance）。</summary>
    public required string Kind { get; init; }

    /// <summary>时间。</summary>
    public DateTimeOffset Utc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>本地时间（界面显示用）。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTimeOffset Local => Utc.LocalDateTime;

    [ObservableProperty]
    private bool _isRead;
}

/// <summary>
/// 通知中心服务：订阅事件流自动生成通知（扫描完成/删除完成可撤销/恢复完成/异常），
/// 未读计数、全部已读、撤销回调（由宿主注册 CleanEngine 回滚解析器）。最近 50 条落盘。
/// </summary>
public sealed partial class NotificationService : ObservableObject
{
    private const int MaxPersisted = 50;

    private readonly string _storeFile;
    private readonly object _gate = new();

    [ObservableProperty]
    private int _unreadCount;

    /// <summary>通知集合（最新在前）。</summary>
    public ObservableCollection<AppNotification> Notifications { get; } = [];

    /// <summary>以持久化目录创建服务并加载最近通知。</summary>
    /// <param name="directory">持久化目录。</param>
    public NotificationService(string directory)
    {
        Directory.CreateDirectory(directory);
        _storeFile = Path.Combine(directory, "notifications.json");
        Load();
    }

    /// <summary>订阅事件流自动生成通知。</summary>
    /// <param name="tap">事件分接头。</param>
    public void Attach(AppEventTap tap)
    {
        tap.EventEmitted += (kind, message, properties) =>
        {
            switch (kind)
            {
                case LogEventKind.ScanFinish:
                    Add(new AppNotification { Title = "扫描完成", Body = message, Kind = "Scan" });
                    break;
                case LogEventKind.DeleteFinish:
                    Add(new AppNotification { Title = "清理完成", Body = message, Kind = "Delete" });
                    break;
                case LogEventKind.RestoreFinish:
                    Add(new AppNotification { Title = "恢复完成", Body = message, Kind = "Restore" });
                    break;
                case LogEventKind.Exception:
                    Add(new AppNotification { Title = "错误", Body = message, Kind = "Error" });
                    break;
            }
        };
    }

    /// <summary>添加通知（置顶 + 未读计数 + 截尾 + 落盘）。</summary>
    /// <param name="notification">通知。</param>
    public void Add(AppNotification notification)
    {
        // 事件流在任意线程触发（扫描/引擎后台线程），集合变更必须封送 UI 线程
        InvokeOnUiThread(() =>
        {
            Notifications.Insert(0, notification);
            while (Notifications.Count > MaxPersisted)
            {
                Notifications.RemoveAt(Notifications.Count - 1);
            }

            UnreadCount++;
        });
        Persist();
    }

    /// <summary>全部标记已读。</summary>
    public void MarkAllRead()
    {
        InvokeOnUiThread(() =>
        {
            foreach (AppNotification notification in Notifications)
            {
                notification.IsRead = true;
            }

            UnreadCount = 0;
        });
        Persist();
    }

    private static void InvokeOnUiThread(Action action)
    {
        System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storeFile))
            {
                return;
            }

            var items = JsonSerializer.Deserialize<List<AppNotification>>(File.ReadAllText(_storeFile));
            if (items is null)
            {
                return;
            }

            foreach (AppNotification item in items.Take(MaxPersisted))
            {
                item.IsRead = true; // 历史通知默认已读，未读计数只算本次会话新增
                Notifications.Add(item);
            }
        }
        catch (IOException)
        {
            // 通知加载失败静默
        }
        catch (JsonException)
        {
        }
    }

    private void Persist()
    {
        try
        {
            lock (_gate)
            {
                File.WriteAllText(_storeFile, JsonSerializer.Serialize(Notifications.ToList()));
            }
        }
        catch (IOException)
        {
            // 通知落盘失败静默
        }
    }
}
