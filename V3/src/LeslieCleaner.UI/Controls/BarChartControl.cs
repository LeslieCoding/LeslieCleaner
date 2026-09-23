using System.Windows;
using System.Windows.Media;

namespace LeslieCleaner.UI.Controls;

/// <summary>柱状图数据点。</summary>
/// <param name="Label">标签。</param>
/// <param name="Value">数值。</param>
/// <param name="Brush">柱画刷。</param>
public sealed record BarChartPoint(string Label, double Value, Brush Brush);

/// <summary>简易柱状图控件（自绘，底部分析图用）。</summary>
public sealed class BarChartControl : FrameworkElement
{
    /// <summary>数据点集合。</summary>
    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(nameof(Points), typeof(IReadOnlyList<BarChartPoint>), typeof(BarChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>数据点集合。</summary>
    public IReadOnlyList<BarChartPoint>? Points
    {
        get => (IReadOnlyList<BarChartPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        IReadOnlyList<BarChartPoint>? points = Points;
        if (points is null || points.Count == 0 || ActualWidth <= 4 || ActualHeight <= 4)
        {
            return;
        }

        double max = points.Max(static p => Math.Max(p.Value, 0));
        if (max <= 0)
        {
            return;
        }

        double gap = 6;
        double barWidth = (ActualWidth - gap * (points.Count - 1)) / points.Count;
        double usableHeight = ActualHeight - 18;
        var typeface = new Typeface("Microsoft YaHei UI");

        for (int i = 0; i < points.Count; i++)
        {
            double height = Math.Max(points[i].Value / max * usableHeight, 2);
            double x = i * (barWidth + gap);
            double y = usableHeight - height;
            drawingContext.DrawRectangle(points[i].Brush, null,
                new Rect(x, y, barWidth, height));

            var text = new FormattedText(
                points[i].Label,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface, 10, Brushes.Gray, 1.0)
            {
                MaxTextWidth = barWidth + gap,
            };
            drawingContext.DrawText(text, new Point(x, usableHeight + 2));
        }
    }
}
