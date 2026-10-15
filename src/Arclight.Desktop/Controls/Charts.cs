using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Arclight.Desktop.Services;

namespace Arclight.Desktop.Controls;

public sealed record ChartPoint(string Label, double Value, string Tooltip, bool ShowLabel);

public sealed record ChartSegment(string Label, double Value, Brush Brush);

/// <summary>Daily activity bars with hover read-out. Lightweight: drawn directly in OnRender.</summary>
public sealed class ActivityChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<ChartPoint>), typeof(ActivityChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(ActivityChart),
        new FrameworkPropertyMetadata(Brushes.MediumPurple, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightBrushProperty = DependencyProperty.Register(
        nameof(HighlightBrush), typeof(Brush), typeof(ActivityChart),
        new FrameworkPropertyMetadata(Brushes.SkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface LabelFace = new("Segoe UI");
    private static readonly Brush AxisBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush LabelBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x71, 0x71, 0x7A)));
    private static readonly Brush EmptyBarBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush TooltipBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x2B)));

    private int _hover = -1;

    public IReadOnlyList<ChartPoint>? Points
    {
        get => (IReadOnlyList<ChartPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public Brush HighlightBrush
    {
        get => (Brush)GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var index = IndexAt(e.GetPosition(this).X);
        if (index != _hover)
        {
            _hover = index;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var points = Points;
        if (points is null || points.Count == 0 || ActualWidth < 40 || ActualHeight < 60)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        const double axisWidth = 36;
        const double labelHeight = 22;
        const double top = 30;
        var plotWidth = ActualWidth - axisWidth;
        var plotHeight = ActualHeight - labelHeight - top;
        var max = NiceMax(points.Max(p => p.Value));

        // Horizontal guides at 0, 50% and 100%.
        for (var i = 0; i <= 2; i++)
        {
            var y = top + plotHeight - (plotHeight * i / 2);
            dc.DrawRectangle(AxisBrush, null, new Rect(axisWidth, Math.Round(y), plotWidth, 1));
            var text = Text(FormatHours(max * i / 2), 11, LabelBrush, dpi);
            dc.DrawText(text, new Point(0, y - (text.Height / 2)));
        }

        var slot = plotWidth / points.Count;
        var barWidth = Math.Max(3, Math.Min(22, slot * 0.62));
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var x = axisWidth + (slot * i) + ((slot - barWidth) / 2);
            var height = max <= 0 ? 0 : plotHeight * (point.Value / max);
            var brush = i == _hover ? HighlightBrush : BarBrush;
            if (height < 3)
            {
                dc.DrawRoundedRectangle(EmptyBarBrush, null, new Rect(x, top + plotHeight - 3, barWidth, 3), 1.5, 1.5);
            }
            else
            {
                dc.DrawRoundedRectangle(brush, null, new Rect(x, top + plotHeight - height, barWidth, height), Math.Min(4, barWidth / 2), Math.Min(4, barWidth / 2));
            }

            if (point.ShowLabel)
            {
                var label = Text(point.Label, 11, LabelBrush, dpi);
                dc.DrawText(label, new Point(x + (barWidth / 2) - (label.Width / 2), top + plotHeight + 6));
            }
        }

        if (_hover >= 0 && _hover < points.Count)
        {
            var tip = Text(points[_hover].Tooltip, 12, Brushes.White, dpi);
            var cx = axisWidth + (slot * _hover) + (slot / 2);
            var box = new Rect(Math.Clamp(cx - (tip.Width / 2) - 9, 0, Math.Max(0, ActualWidth - tip.Width - 18)), 0, tip.Width + 18, tip.Height + 8);
            dc.DrawRoundedRectangle(TooltipBrush, null, box, 7, 7);
            dc.DrawText(tip, new Point(box.X + 9, box.Y + 4));
        }
    }

    private int IndexAt(double x)
    {
        var points = Points;
        if (points is null || points.Count == 0)
        {
            return -1;
        }

        const double axisWidth = 36;
        var slot = (ActualWidth - axisWidth) / points.Count;
        var index = (int)((x - axisWidth) / slot);
        return index >= 0 && index < points.Count ? index : -1;
    }

    private static double NiceMax(double seconds)
    {
        var hours = seconds / 3600;
        double[] steps = [0.5, 1, 2, 3, 4, 6, 8, 12, 16, 24];
        foreach (var step in steps)
        {
            if (hours <= step)
            {
                return step * 3600;
            }
        }

        return Math.Ceiling(hours) * 3600;
    }

    private static string FormatHours(double seconds)
    {
        var hours = seconds / 3600;
        return hours switch
        {
            0 => "0",
            < 1 => Loc.Format("Chart.Minutes", Math.Round(hours * 60)),
            _ => Loc.Format("Chart.Hours", hours.ToString("0.#", Loc.Culture)),
        };
    }

    internal static FormattedText Text(string value, double size, Brush brush, double dpi) =>
        new(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelFace, size, brush, dpi);

    internal static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}

/// <summary>Ring chart for proportions (games per platform).</summary>
public sealed class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IReadOnlyList<ChartSegment>), typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenterTextProperty = DependencyProperty.Register(
        nameof(CenterText), typeof(string), typeof(DonutChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenterCaptionProperty = DependencyProperty.Register(
        nameof(CenterCaption), typeof(string), typeof(DonutChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface Display = new(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Brush TrackBrush = ActivityChart.Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush CaptionBrush = ActivityChart.Freeze(new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xAA)));

    public IReadOnlyList<ChartSegment>? Segments
    {
        get => (IReadOnlyList<ChartSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public string CenterText
    {
        get => (string)GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public string CenterCaption
    {
        get => (string)GetValue(CenterCaptionProperty);
        set => SetValue(CenterCaptionProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size < 40)
        {
            return;
        }

        const double thickness = 16;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size / 2) - (thickness / 2) - 2;
        dc.DrawEllipse(null, new Pen(TrackBrush, thickness), center, radius, radius);

        var segments = Segments?.Where(s => s.Value > 0).ToList() ?? [];
        var total = segments.Sum(s => s.Value);
        if (total > 0)
        {
            var gap = segments.Count > 1 ? 2.5 : 0;
            var angle = -90.0;
            foreach (var segment in segments)
            {
                var sweep = 360 * segment.Value / total;
                var drawSweep = Math.Max(0.5, sweep - gap);
                if (drawSweep >= 359.9)
                {
                    dc.DrawEllipse(null, new Pen(segment.Brush, thickness), center, radius, radius);
                }
                else
                {
                    var pen = new Pen(segment.Brush, thickness) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
                    dc.DrawGeometry(null, pen, Arc(center, radius, angle + (gap / 2), drawSweep));
                }

                angle += sweep;
            }
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var main = new FormattedText(CenterText ?? string.Empty, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Display, 26, Brushes.White, dpi);
        var caption = ActivityChart.Text(CenterCaption ?? string.Empty, 12, CaptionBrush, dpi);
        var blockHeight = main.Height + caption.Height;
        dc.DrawText(main, new Point(center.X - (main.Width / 2), center.Y - (blockHeight / 2)));
        dc.DrawText(caption, new Point(center.X - (caption.Width / 2), center.Y - (blockHeight / 2) + main.Height));
    }

    private static StreamGeometry Arc(Point center, double radius, double startDegrees, double sweepDegrees)
    {
        static Point On(Point c, double r, double degrees)
        {
            var radians = degrees * Math.PI / 180;
            return new Point(c.X + (r * Math.Cos(radians)), c.Y + (r * Math.Sin(radians)));
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(On(center, radius, startDegrees), false, false);
            context.ArcTo(On(center, radius, startDegrees + sweepDegrees), new Size(radius, radius), 0, sweepDegrees > 180, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        return geometry;
    }
}
