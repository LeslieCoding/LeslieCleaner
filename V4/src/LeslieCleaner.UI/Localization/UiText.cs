#pragma warning disable CS1591 // RESX strongly-typed facade properties.
using System.Globalization;
using System.Resources;

namespace LeslieCleaner.UI.Localization;

/// <summary>RESX 支持的 UI 文本入口；缺少翻译时回退到中文中性资源。</summary>
public static class UiText
{
    private static readonly ResourceManager Manager = new("LeslieCleaner.UI.Localization.UiStrings", typeof(UiText).Assembly);

    public static CultureInfo? Culture { get; set; }
    public static string Get(string key) => Manager.GetString(key, Culture ?? CultureInfo.CurrentUICulture) ?? $"[{key}]";

    public static string PlatformCenterTitle => Get(nameof(PlatformCenterTitle));
    public static string PlatformCenterDescription => Get(nameof(PlatformCenterDescription));
    public static string AiAssistant => Get(nameof(AiAssistant));
    public static string AskAi => Get(nameof(AskAi));
    public static string Cancel => Get(nameof(Cancel));
    public static string PluginManager => Get(nameof(PluginManager));
    public static string RefreshAndVerify => Get(nameof(RefreshAndVerify));
    public static string UpdateCenter => Get(nameof(UpdateCenter));
    public static string CheckUpdates => Get(nameof(CheckUpdates));
    public static string SecurityCenter => Get(nameof(SecurityCenter));
    public static string Reassess => Get(nameof(Reassess));
    public static string EnableOrDisable => Get(nameof(EnableOrDisable));
    public static string Retry => Get(nameof(Retry));
}