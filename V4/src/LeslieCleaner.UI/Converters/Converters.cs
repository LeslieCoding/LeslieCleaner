using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LeslieCleaner.UI.Converters;

/// <summary>布尔 → Visibility（true=Visible）。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>为 true 时反转（true=Collapsed）。</summary>
    public bool Invert { get; set; }

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is true;
        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible ? !Invert : Invert;
}

/// <summary>非空 → Visibility（非 null=Visible）。</summary>
public sealed class NotNullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>数值为零 → Visibility（0=Visible，用于空态提示）。</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>字节数 → 人类可读（GB/MB/KB）。</summary>
public sealed class BytesToStringConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        long bytes = value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            _ => 0L,
        };

        return FormatBytes(bytes);
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    /// <summary>格式化字节数。</summary>
    /// <param name="bytes">字节数。</param>
    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F1} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F1} KB",
        _ => $"{bytes} B",
    };
}

/// <summary>相等性 → Visibility（参数为期望值）。</summary>
public sealed class EqualityToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Equals(value?.ToString(), parameter?.ToString()) ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>字符串 → 画刷资源（"Safe"/"Moderate"/"Risky" 语义映射，其他值按资源键解析）。</summary>
public sealed class RiskToBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value?.ToString() switch
        {
            "Safe" => "SuccessBrush",
            "Moderate" => "WarningBrush",
            "Risky" => "DangerBrush",
            { Length: > 0 } raw => raw,
            _ => "DangerBrush",
        };

        return Application.Current.Resources.Contains(key)
            ? Application.Current.Resources[key]
            : Application.Current.Resources["TextBrush"];
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>影响评级 → 星标文本（High=★★★，Medium=★★，Low=★）。</summary>
public sealed class ImpactToStarsConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "High" => "★★★",
            "Medium" => "★★",
            _ => "★",
        };

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
