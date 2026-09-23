using System.IO;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;

namespace LeslieCleaner.UI.Theming;

/// <summary>主题模式。</summary>
public enum ThemeMode
{
    /// <summary>浅色。</summary>
    Light,

    /// <summary>深色。</summary>
    Dark,

    /// <summary>跟随系统（监听 AppsUseLightTheme 变化）。</summary>
    Auto,
}

/// <summary>
/// 主题服务：Light/Dark/Auto（跟随系统，3s 轮询注册表 AppsUseLightTheme）。
/// 主题选择持久化到配置键 <see cref="ThemeConfigKey"/>，热更新即时生效。
/// </summary>
public sealed class ThemeService : IDisposable
{
    /// <summary>主题配置键（light/dark/auto）。</summary>
    public const string ThemeConfigKey = "ui:theme";

    /// <summary>Mica 背景配置键；默认关闭以避免显著的图形驱动空闲工作集。</summary>
    public const string MicaEnabledConfigKey = "ui:micaEnabled";

    private static readonly Uri LightUri = new("/LeslieCleaner.UI;component/Themes/Colors.Light.xaml", UriKind.Relative);
    private static readonly Uri DarkUri = new("/LeslieCleaner.UI;component/Themes/Colors.Dark.xaml", UriKind.Relative);

    private readonly IConfigService _configService;
    private readonly DispatcherTimer _systemWatcher;
    private ThemeMode _mode = ThemeMode.Auto;
    private bool _lastSystemLight = true;
    private int _disposed;

    /// <summary>主题实际应用（Light/Dark 解析后）时触发。</summary>
    public event EventHandler? ThemeApplied;

    /// <summary>当前主题模式。</summary>
    public ThemeMode Mode => _mode;

    /// <summary>是否显式启用 Mica 窗口背景。</summary>
    public bool MicaEnabled { get; }

    /// <summary>以配置服务创建主题服务并应用已保存主题。</summary>
    /// <param name="configService">配置服务。</param>
    public ThemeService(IConfigService configService)
    {
        _configService = configService;
        string saved = _configService.GetValue(ThemeConfigKey, "light");
        _mode = saved.ToLowerInvariant() switch
        {
            "dark" => ThemeMode.Dark,
            "auto" => ThemeMode.Auto,
            _ => ThemeMode.Light,
        };
        MicaEnabled = _configService.GetValue(MicaEnabledConfigKey, false);

        _systemWatcher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _systemWatcher.Tick += (_, _) =>
        {
            if (_mode == ThemeMode.Auto)
            {
                bool light = IsSystemLightTheme();
                if (light != _lastSystemLight)
                {
                    _lastSystemLight = light;
                    ApplyResolved(light ? ThemeMode.Light : ThemeMode.Dark);
                }
            }
        };
    }

    /// <summary>当前是否为深色（解析 Auto 后）。</summary>
    public bool IsDark =>
        _mode == ThemeMode.Dark || (_mode == ThemeMode.Auto && !IsSystemLightTheme());

    /// <summary>初始化：应用主题并启动系统监听。</summary>
    public void Initialize()
    {
        _lastSystemLight = IsSystemLightTheme();
        ApplyResolved(IsDark ? ThemeMode.Dark : ThemeMode.Light);
        _systemWatcher.Start();
    }

    /// <summary>
    /// 切换主题模式（持久化 + 即时生效）。
    /// </summary>
    /// <param name="mode">主题模式。</param>
    public async Task SetModeAsync(ThemeMode mode)
    {
        _mode = mode;
        await _configService.SetValueAsync(ThemeConfigKey,
            mode.ToString().ToLowerInvariant(), CancellationToken.None).ConfigureAwait(false);
        ApplyResolved(IsDark ? ThemeMode.Dark : ThemeMode.Light);
    }

    /// <summary>释放系统监听。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _systemWatcher.Stop();
        }
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: false);
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return true;
        }
    }

    private void ApplyResolved(ThemeMode resolved)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ResourceDictionary theme = new() { Source = resolved == ThemeMode.Dark ? DarkUri : LightUri };
            System.Collections.ObjectModel.Collection<ResourceDictionary> dictionaries =
                Application.Current.Resources.MergedDictionaries;
            for (int i = dictionaries.Count - 1; i >= 0; i--)
            {
                string? source = dictionaries[i].Source?.OriginalString;
                if (source is not null &&
                    (source.Contains("Colors.Light", StringComparison.OrdinalIgnoreCase) ||
                     source.Contains("Colors.Dark", StringComparison.OrdinalIgnoreCase)))
                {
                    dictionaries.RemoveAt(i);
                }
            }

            // 追加到末尾：同键后加载者生效
            dictionaries.Add(theme);
            ThemeApplied?.Invoke(this, EventArgs.Empty);
        });
    }
}
