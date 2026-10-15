using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Arclight.Desktop.Controls;

/// <summary>Five-star personal rating. Clicking the current rating again clears it.</summary>
public sealed class RatingControl : StackPanel
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int?), typeof(RatingControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((RatingControl)d).Refresh(null)));

    public static readonly DependencyProperty StarSizeProperty = DependencyProperty.Register(
        nameof(StarSize), typeof(double), typeof(RatingControl), new PropertyMetadata(20.0, (d, _) => ((RatingControl)d).Build()));

    public RatingControl()
    {
        Orientation = Orientation.Horizontal;
        Background = Brushes.Transparent;
        Cursor = Cursors.Hand;
        Build();
        MouseLeave += (_, _) => Refresh(null);
        SetResourceReference(ToolTipProperty, "Rating.ToolTip");
    }

    public int? Value
    {
        get => (int?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double StarSize
    {
        get => (double)GetValue(StarSizeProperty);
        set => SetValue(StarSizeProperty, value);
    }

    private void Build()
    {
        Children.Clear();
        for (var i = 1; i <= 5; i++)
        {
            var star = new TextBlock
            {
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = StarSize,
                Margin = new Thickness(0, 0, 4, 0),
                Tag = i,
            };
            var rating = i;
            star.MouseEnter += (_, _) => Refresh(rating);
            star.MouseLeftButtonUp += (_, e) =>
            {
                Value = Value == rating ? null : rating;
                e.Handled = true;
            };
            Children.Add(star);
        }

        Refresh(null);
    }

    private void Refresh(int? preview)
    {
        var shown = preview ?? Value ?? 0;
        var filled = (Brush)(TryFindResource("Brush.Warning") ?? Brushes.Gold);
        var empty = (Brush)(TryFindResource("Brush.Text.Muted") ?? Brushes.Gray);
        foreach (TextBlock star in Children)
        {
            var index = (int)star.Tag;
            star.Text = index <= shown ? "" : "";
            star.Foreground = index <= shown ? filled : empty;
            star.Opacity = preview is not null && index <= shown ? 0.85 : 1;
        }
    }
}
