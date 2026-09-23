#pragma warning disable CS1591 // Public members retain existing stable API; behavior is documented at type level.
using System.ComponentModel;
using System.IO;
using LeslieCleaner.Core.Abstractions;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;

namespace LeslieCleaner.UI.Theming;

public enum ThemeMode
{
    Light,
    Dark,
    Auto,
}

/// <summary>Light/Dark/Auto 与 Windows 高对比模式主题服务。</summary>
public sealed class ThemeService : IDisposable
{
    public const string ThemeConfigKey = "ui:theme";
    public const string MicaEnabledConfigKey = "ui:micaEnabled";

    private static readonly Uri LightUri = new("/LeslieCleaner.UI;component/Themes/Colors.Light.xaml", UriKind.Relative);
    private static readonly Uri DarkUri = new("/LeslieCleaner.UI;component/Themes/Colors.Dark.xaml", UriKind.Relative);
    private static readonly Uri HighContrastUri = new("/LeslieCleaner.UI;component/Themes/Colors.HighContrast.xaml", UriKind.Relative);

    private readonly IConfigService _configService;
    private readonly DispatcherTimer _systemWatcher;
    private ThemeMode _mode = ThemeMode.Auto;
    private bool _lastSystemLight = true;
    private bool _lastHighContrast;
    private int _disposed;

    public event EventHandler? ThemeApplied;
    public ThemeMode Mode => _mode;
    public bool MicaEnabled { get; }
    public bool IsHighContrast => SystemParameters.HighContrast;

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
        _systemWatcher.Tick += OnSystemWatcherTick;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    public bool IsDark =>
        _mode == ThemeMode.Dark || (_mode == ThemeMode.Auto && !IsSystemLightTheme());

    public void Initialize()
    {
        _lastSystemLight = IsSystemLightTheme();
        _lastHighContrast = SystemParameters.HighContrast;
        ApplyResolved(IsDark ? ThemeMode.Dark : ThemeMode.Light);
        _systemWatcher.Start();
    }

    public async Task SetModeAsync(ThemeMode mode)
    {
        _mode = mode;
        await _configService.SetValueAsync(ThemeConfigKey,
            mode.ToString().ToLowerInvariant(), CancellationToken.None).ConfigureAwait(false);
        ApplyResolved(IsDark ? ThemeMode.Dark : ThemeMode.Light);
    }

    /// <summary>返回将使用的资源文件名；用于代码级可访问性验收。</summary>
    public static string ResolveThemeResourceName(ThemeMode mode, bool systemLight, bool highContrast)
    {
        if (highContrast)
        {
            return "Colors.HighContrast.xaml";
        }

        bool dark = mode == ThemeMode.Dark || (mode == ThemeMode.Auto && !systemLight);
        return dark ? "Colors.Dark.xaml" : "Colors.Light.xaml";
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _systemWatcher.Stop();
            _systemWatcher.Tick -= OnSystemWatcherTick;
            SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        }
    }

    private void OnSystemWatcherTick(object? sender, EventArgs e)
    {
        bool highContrast = SystemParameters.HighContrast;
        bool light = IsSystemLightTheme();
        if (highContrast != _lastHighContrast || (_mode == ThemeMode.Auto && light != _lastSystemLight))
        {
            _lastHighContrast = highContrast;
            _lastSystemLight = light;
            ApplyResolved(IsDark ? ThemeMode.Dark : ThemeMode.Light);
        }
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
        {
            _lastHighContrast = SystemParameters.HighContrast;
            ApplyResolved(IsDark ? ThemeMode.Dark : ThemeMode.Light);
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
            Uri selected = SystemParameters.HighContrast
                ? HighContrastUri
                : resolved == ThemeMode.Dark ? DarkUri : LightUri;
            ResourceDictionary theme = new() { Source = selected };
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            for (int i = dictionaries.Count - 1; i >= 0; i--)
            {
                string? source = dictionaries[i].Source?.OriginalString;
                if (source is not null && source.Contains("Colors.", StringComparison.OrdinalIgnoreCase))
                {
                    dictionaries.RemoveAt(i);
                }
            }

            dictionaries.Add(theme);
            ThemeApplied?.Invoke(this, EventArgs.Empty);
        });
    }
}