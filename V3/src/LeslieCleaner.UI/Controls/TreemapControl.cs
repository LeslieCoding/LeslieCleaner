using System.Windows;
using System.Windows.Media;
using LeslieCleaner.Modules.Cleaner.Analytics;

namespace LeslieCleaner.UI.Controls;

/// <summary>
/// Treemap 可视化控件（slice-and-dice 布局自绘色块）。
/// 色块颜色由节点名哈希稳定映射到色板；面积与节点大小成正比。
/// </summary>
public sealed class TreemapControl : FrameworkElement
{
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x25, 0x63, 0xEB), Color.FromRgb(0x16, 0xA3, 0x4A),
        Color.FromRgb(0xEA, 0x58, 0x0C), Color.FromRgb(0x8B, 0x5C, 0xF6),
        Color.FromRgb(0x0D, 0x94, 0xA8), Color.FromRgb(0xDB, 0x27, 0x77),
        Color.FromRgb(0x65, 0xA3, 0x0D), Color.FromRgb(0xD9, 0x77, 0x06),
    ];

    /// <summary>数据根节点集合。</summary>
    public static readonly DependencyProperty RootsProperty =
        DependencyProperty.Register(nameof(Roots), typeof(IReadOnlyList<TreemapNode>), typeof(TreemapControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>数据根节点集合。</summary>
    public IReadOnlyList<TreemapNode>? Roots
    {
        get => (IReadOnlyList<TreemapNode>?)GetValue(RootsProperty);
        set => SetValue(RootsProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        IReadOnlyList<TreemapNode>? roots = Roots;
        if (roots is null || roots.Count == 0 || ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        LayoutLevel(roots, bounds, depth: 0, drawingContext);
    }

    private static void LayoutLevel(
        IReadOnlyList<TreemapNode> nodes, Rect bounds, int depth, DrawingContext context)
    {
        long total = nodes.Sum(static n => Math.Max(n.SizeBytes, 0));
        if (total <= 0 || bounds.Width < 2 || bounds.Height < 2 || depth > 3)
        {
            if (depth == 0 || bounds.Width >= 2 && bounds.Height >= 2)
            {
                // 叶子：绘制色块
            }

            return;
        }

        double offset = 0;
        bool horizontal = bounds.Width >= bounds.Height;
        foreach (TreemapNode node in nodes.OrderByDescending(static n => n.SizeBytes))
        {
            double fraction = Math.Max(node.SizeBytes, 0) / (double)total;
            Rect slice;
            if (horizontal)
            {
                double width = bounds.Width * fraction;
                slice = new Rect(bounds.X + offset, bounds.Y, width, bounds.Height);
                offset += width;
            }
            else
            {
                double height = bounds.Height * fraction;
                slice = new Rect(bounds.X, bounds.Y + offset, bounds.Width, height);
                offset += height;
            }

            bool isLeaf = node.Children.Count == 0 || depth >= 2 || slice.Width < 40 || slice.Height < 24;
            if (isLeaf)
            {
                Color color = Palette[Math.Abs(node.Name.GetHashCode()) % Palette.Length];
                var brush = new SolidColorBrush(color);
                context.DrawRectangle(brush, new Pen(Brushes.White, 1), slice);
                if (slice.Width >= 48 && slice.Height >= 28)
                {
                    var text = new FormattedText(
                        node.Name,
                        System.Globalization.CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Microsoft YaHei UI"),
                        11, Brushes.White, 1.0)
                    {
                        MaxTextWidth = slice.Width - 6,
                        MaxTextHeight = slice.Height - 4,
                    };
                    context.DrawText(text, new Point(slice.X + 3, slice.Y + 3));
                }
            }
            else
            {
                LayoutLevel(node.Children, slice, depth + 1, context);
            }
        }
    }
}
