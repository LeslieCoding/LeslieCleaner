using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LeslieCleaner.UI.Controls;

/// <summary>圆环评分控件（自绘弧，60fps 无动画依赖）。</summary>
public sealed class ScoreRing : FrameworkElement
{
    /// <summary>值（0-100）依赖属性。</summary>
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(ScoreRing),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>弧线粗细。</summary>
    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(ScoreRing),
            new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>弧线画刷。</summary>
    public static readonly DependencyProperty RingBrushProperty =
        DependencyProperty.Register(nameof(RingBrush), typeof(Brush), typeof(ScoreRing),
            new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>轨道画刷。</summary>
    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(ScoreRing),
            new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>当前值（0-100）。</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>弧线粗细。</summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>弧线画刷。</summary>
    public Brush RingBrush
    {
        get => (Brush)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    /// <summary>轨道画刷。</summary>
    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= StrokeThickness)
        {
            return;
        }

        double radius = (size - StrokeThickness) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var trackPen = new Pen(TrackBrush, StrokeThickness);
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        double clamped = Math.Clamp(Value, 0, 100);
        if (clamped > 0)
        {
            var ringPen = new Pen(RingBrush, StrokeThickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            drawingContext.DrawGeometry(null, ringPen, BuildArcGeometry(center, radius, clamped / 100.0));
        }
    }

    private static Geometry BuildArcGeometry(Point center, double radius, double fraction)
    {
        double startAngle = -90.0;
        double sweepAngle = fraction * 360.0;
        if (sweepAngle >= 359.99)
        {
            sweepAngle = 359.99;
        }

        Point start = PointOnCircle(center, radius, startAngle);
        Point end = PointOnCircle(center, radius, startAngle + sweepAngle);
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(
            end, new Size(radius, radius), 0, sweepAngle > 180, SweepDirection.Clockwise, true));
        return new PathGeometry([figure]);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + (radius * Math.Cos(radians)), center.Y + (radius * Math.Sin(radians)));
    }
}

/// <summary>环形图条目。</summary>
/// <param name="Label">标签。</param>
/// <param name="Value">数值。</param>
/// <param name="Brush">扇区画刷。</param>
public sealed record DonutSlice(string Label, double Value, Brush Brush);

/// <summary>环形图控件（自绘扇区）。</summary>
public sealed class DonutChart : FrameworkElement
{
    /// <summary>扇区集合依赖属性。</summary>
    public static readonly DependencyProperty SlicesProperty =
        DependencyProperty.Register(nameof(Slices), typeof(IReadOnlyList<DonutSlice>), typeof(DonutChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>环厚度比例（0-1）。</summary>
    public static readonly DependencyProperty ThicknessRatioProperty =
        DependencyProperty.Register(nameof(ThicknessRatio), typeof(double), typeof(DonutChart),
            new FrameworkPropertyMetadata(0.28, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>扇区集合。</summary>
    public IReadOnlyList<DonutSlice>? Slices
    {
        get => (IReadOnlyList<DonutSlice>?)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    /// <summary>环厚度比例。</summary>
    public double ThicknessRatio
    {
        get => (double)GetValue(ThicknessRatioProperty);
        set => SetValue(ThicknessRatioProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        IReadOnlyList<DonutSlice>? slices = Slices;
        double size = Math.Min(ActualWidth, ActualHeight);
        if (slices is null || slices.Count == 0 || size <= 4)
        {
            return;
        }

        double total = slices.Sum(static s => Math.Max(s.Value, 0));
        if (total <= 0)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        double outerRadius = size / 2 - 2;
        double innerRadius = outerRadius * (1 - ThicknessRatio);
        double angle = -90.0;

        foreach (DonutSlice slice in slices)
        {
            double sweep = Math.Max(slice.Value, 0) / total * 360.0;
            if (sweep <= 0.01)
            {
                continue;
            }

            drawingContext.DrawGeometry(slice.Brush, null, BuildSlice(center, outerRadius, innerRadius, angle, sweep));
            angle += sweep;
        }
    }

    private static Geometry BuildSlice(Point center, double outerRadius, double innerRadius, double startAngle, double sweepAngle)
    {
        if (sweepAngle >= 359.99)
        {
            sweepAngle = 359.99;
        }

        Point outerStart = PointOnCircle(center, outerRadius, startAngle);
        Point outerEnd = PointOnCircle(center, outerRadius, startAngle + sweepAngle);
        Point innerStart = PointOnCircle(center, innerRadius, startAngle + sweepAngle);
        Point innerEnd = PointOnCircle(center, innerRadius, startAngle);
        bool largeArc = sweepAngle > 180;

        var figure = new PathFigure { StartPoint = outerStart, IsClosed = true };
        figure.Segments.Add(new ArcSegment(outerEnd, new Size(outerRadius, outerRadius), 0, largeArc, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(innerStart, true));
        figure.Segments.Add(new ArcSegment(innerEnd, new Size(innerRadius, innerRadius), 0, largeArc, SweepDirection.Counterclockwise, true));
        return new PathGeometry([figure]);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + (radius * Math.Cos(radians)), center.Y + (radius * Math.Sin(radians)));
    }
}
