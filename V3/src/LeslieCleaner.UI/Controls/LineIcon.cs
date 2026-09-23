using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LeslieCleaner.UI.Controls;

/// <summary>
/// 细线条图标控件（Lucide/Material Outlined 风格，1.5px 笔画）。
/// 图标几何数据集中定义于 <see cref="LineIcons"/>，颜色随 Foreground（禁彩色厚重图标）。
/// </summary>
public sealed class LineIcon : Control
{
    /// <summary>图标名（<see cref="LineIcons"/> 键）。</summary>
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(string), typeof(LineIcon),
            new FrameworkPropertyMetadata("home", FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>笔画画刷（默认随主题文字色）。</summary>
    public static readonly DependencyProperty StrokeBrushProperty =
        DependencyProperty.Register(nameof(StrokeBrush), typeof(Brush), typeof(LineIcon),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>笔画粗细（默认 1.5px）。</summary>
    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(LineIcon),
            new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>图标名。</summary>
    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>笔画画刷；为 <see langword="null"/> 时用 Foreground。</summary>
    public Brush? StrokeBrush
    {
        get => (Brush?)GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    /// <summary>笔画粗细。</summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    static LineIcon()
    {
        ForegroundProperty.OverrideMetadata(typeof(LineIcon),
            new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (!LineIcons.TryGet(Kind, out string data))
        {
            return;
        }

        Geometry geometry = Geometry.Parse(data);
        Brush brush = StrokeBrush ?? Foreground;
        var pen = new Pen(brush, StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        double scale = Math.Min(ActualWidth, ActualHeight) / 24.0;
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(null, pen, geometry);
        drawingContext.Pop();
    }
}

/// <summary>细线条图标几何数据（24x24 视口，Lucide 风格 1.5px 笔画）。</summary>
public static class LineIcons
{
    /// <summary>按名称取几何数据。</summary>
    /// <param name="kind">图标名。</param>
    /// <param name="data">几何数据。</param>
    public static bool TryGet(string kind, out string data) => Map.TryGetValue(kind, out data!);

    /// <summary>图标名 → 几何数据。</summary>
    public static IReadOnlyDictionary<string, string> Map { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["home"] = "M3 10.5 L12 3 L21 10.5 V20 A1 1 0 0 1 20 21 H4 A1 1 0 0 1 3 20 Z M9 21 V13 H15 V21",
        ["zap"] = "M13 2 L4 14 H11 L10 22 L20 10 H13 Z",
        ["shield"] = "M12 3 L20 6 V11 C20 16 16.5 19.5 12 21 C7.5 19.5 4 16 4 11 V6 Z",
        ["trash"] = "M4 7 H20 M9 7 V4 H15 V7 M6 7 L7 21 H17 L18 7 M10 11 V17 M14 11 V17",
        ["rocket"] = "M12 2 C15 4 17 7 17 11 L22 16 L17 17 L14 20 L10 20 L7 17 L2 16 L7 11 C7 7 9 4 12 2 Z M12 12 A2 2 0 1 0 12 8 A2 2 0 0 0 12 12",
        ["lock"] = "M5 11 H19 V21 H5 Z M8 11 V7 A4 4 0 0 1 16 7 V11 M12 15 V18",
        ["scissors"] = "M6 6 A2.5 2.5 0 1 0 6 11 A2.5 2.5 0 0 0 6 6 M6 13 A2.5 2.5 0 1 0 6 18 A2.5 2.5 0 0 0 6 13 M8 10 L20 4 M8 14 L20 20",
        ["copy"] = "M8 8 H20 V20 H8 Z M4 16 V4 H16",
        ["chart"] = "M4 20 V12 M10 20 V4 M16 20 V10 M22 20 H2",
        ["toolbox"] = "M3 8 H21 V20 H3 Z M8 8 V5 A2 2 0 0 1 10 3 H14 A2 2 0 0 1 16 5 V8 M3 13 H21",
        ["file"] = "M6 2 H14 L19 7 V22 H6 Z M14 2 V7 H19 M9 12 H16 M9 16 H16",
        ["settings"] = "M12 15 A3 3 0 1 0 12 9 A3 3 0 0 0 12 15 M12 2 V5 M12 19 V22 M2 12 H5 M19 12 H22 M4.9 4.9 L7 7 M17 17 L19.1 19.1 M19.1 4.9 L17 7 M7 17 L4.9 19.1",
        ["search"] = "M11 18 A7 7 0 1 0 11 4 A7 7 0 0 0 11 18 M20 20 L16.5 16.5",
        ["bell"] = "M18 8 A6 6 0 0 0 6 8 C6 15 3 17 3 17 H21 C21 17 18 15 18 8 M10 20 A2 2 0 0 0 14 20",
        ["refresh"] = "M20 11 A8 8 0 0 0 4 11 M4 13 A8 8 0 0 0 20 13 M18 5 L20 11 L14 9 M6 19 L4 13 L10 15",
    };
}
