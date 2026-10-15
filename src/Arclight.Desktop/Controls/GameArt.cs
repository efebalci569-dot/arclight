using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Arclight.Desktop.Controls;

/// <summary>
/// Game artwork with a designed fallback. Without an image it draws a deep gradient (stable per
/// game), the game's icon when one is known, the title, and a faint Arclight arc — so a library
/// without artwork still looks intentional rather than broken.
/// </summary>
public sealed class GameArt : FrameworkElement
{
    private static readonly (Color From, Color To)[] Palette =
    [
        (Color.FromRgb(0x2E, 0x10, 0x65), Color.FromRgb(0x6D, 0x28, 0xD9)),
        (Color.FromRgb(0x0C, 0x2D, 0x48), Color.FromRgb(0x03, 0x69, 0xA1)),
        (Color.FromRgb(0x06, 0x3B, 0x2F), Color.FromRgb(0x04, 0x78, 0x57)),
        (Color.FromRgb(0x4C, 0x05, 0x19), Color.FromRgb(0xB9, 0x1C, 0x3C)),
        (Color.FromRgb(0x43, 0x14, 0x07), Color.FromRgb(0xB4, 0x53, 0x09)),
        (Color.FromRgb(0x1E, 0x1B, 0x4B), Color.FromRgb(0x43, 0x38, 0xCA)),
        (Color.FromRgb(0x08, 0x33, 0x44), Color.FromRgb(0x0E, 0x74, 0x90)),
        (Color.FromRgb(0x3B, 0x07, 0x64), Color.FromRgb(0xA2, 0x1C, 0xAF)),
        (Color.FromRgb(0x1C, 0x19, 0x17), Color.FromRgb(0x57, 0x53, 0x4E)),
    ];

    // The Arclight mark (A + arc) as a faint watermark on placeholders.
    private static readonly Geometry MarkGeometry = Geometry.Parse("M 13.5 52 L 32 12 L 50.5 52 M 18.1 42 A 16 16 0 0 1 45.9 42");

    public static readonly DependencyProperty ImagePathProperty = DependencyProperty.Register(
        nameof(ImagePath), typeof(string), typeof(GameArt), new PropertyMetadata(null, (d, _) => ((GameArt)d).UpdateImage()));

    public static readonly DependencyProperty IconPathProperty = DependencyProperty.Register(
        nameof(IconPath), typeof(string), typeof(GameArt), new PropertyMetadata(null, (d, _) => ((GameArt)d).UpdateIcon()));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(GameArt), new PropertyMetadata(string.Empty, (d, _) => ((GameArt)d).UpdatePlaceholder()));

    public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.Register(
        nameof(DecodeWidth), typeof(int), typeof(GameArt), new PropertyMetadata(360, (d, _) => ((GameArt)d).UpdateImage()));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(GameArt), new PropertyMetadata(12.0, (d, _) => ((GameArt)d).UpdateCorner()));

    public static readonly DependencyProperty ShowTitleProperty = DependencyProperty.Register(
        nameof(ShowTitle), typeof(bool), typeof(GameArt), new PropertyMetadata(true, (d, _) => ((GameArt)d).UpdatePlaceholder()));

    public static readonly DependencyProperty TitleSizeProperty = DependencyProperty.Register(
        nameof(TitleSize), typeof(double), typeof(GameArt), new PropertyMetadata(15.0, (d, _) => ((GameArt)d).UpdatePlaceholder()));

    private readonly ClipBorder _root = new();
    private readonly Border _placeholder = new();
    private readonly System.Windows.Shapes.Path _arc = new();
    private readonly Image _icon = new() { Width = 64, Height = 64, Stretch = Stretch.Uniform };
    private readonly TextBlock _title = new();
    private readonly Image _image = new() { Stretch = Stretch.UniformToFill };

    public GameArt()
    {
        Focusable = false;
        _arc.Data = MarkGeometry;
        _arc.Stretch = Stretch.Uniform;
        _arc.StrokeThickness = 4;
        _arc.StrokeLineJoin = PenLineJoin.Round;
        _arc.StrokeStartLineCap = PenLineCap.Round;
        _arc.StrokeEndLineCap = PenLineCap.Round;
        _arc.Stroke = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        _arc.Width = 46;
        _arc.HorizontalAlignment = HorizontalAlignment.Center;
        _arc.VerticalAlignment = VerticalAlignment.Center;
        _arc.Margin = new Thickness(0, 0, 0, 30);

        _title.Foreground = Brushes.White;
        _title.FontWeight = FontWeights.SemiBold;
        _title.TextWrapping = TextWrapping.Wrap;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.TextAlignment = TextAlignment.Center;
        _title.VerticalAlignment = VerticalAlignment.Bottom;
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.Margin = new Thickness(12, 0, 12, 16);
        _title.MaxHeight = 64;

        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _icon.VerticalAlignment = VerticalAlignment.Center;
        _icon.Margin = new Thickness(0, 0, 0, 24);
        RenderOptions.SetBitmapScalingMode(_icon, BitmapScalingMode.HighQuality);
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

        var placeholderContent = new Grid();
        placeholderContent.Children.Add(_arc);
        placeholderContent.Children.Add(_icon);
        placeholderContent.Children.Add(_title);
        _placeholder.Child = placeholderContent;

        var layers = new Grid();
        layers.Children.Add(_placeholder);
        layers.Children.Add(_image);
        _root.Child = layers;

        AddVisualChild(_root);
        AddLogicalChild(_root);
        UpdatePlaceholder();
        UpdateCorner();
    }

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public string? IconPath
    {
        get => (string?)GetValue(IconPathProperty);
        set => SetValue(IconPathProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public int DecodeWidth
    {
        get => (int)GetValue(DecodeWidthProperty);
        set => SetValue(DecodeWidthProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public bool ShowTitle
    {
        get => (bool)GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    public double TitleSize
    {
        get => (double)GetValue(TitleSizeProperty);
        set => SetValue(TitleSizeProperty, value);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _root;

    protected override Size MeasureOverride(Size constraint)
    {
        _root.Measure(constraint);
        return new Size(
            double.IsInfinity(constraint.Width) ? _root.DesiredSize.Width : constraint.Width,
            double.IsInfinity(constraint.Height) ? _root.DesiredSize.Height : constraint.Height);
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        // Tiny thumbnails only show the gradient; the watermark would just be noise.
        _arc.Visibility = arrangeBounds.Width < 70 || !string.IsNullOrEmpty(IconPath) ? Visibility.Collapsed : Visibility.Visible;
        _root.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    public static LinearGradientBrush CreatePlaceholderBrush(string? seed)
    {
        var (from, to) = Palette[StableHash(seed) % Palette.Length];
        var brush = new LinearGradientBrush(to, from, new Point(0, 0), new Point(0.4, 1));
        brush.Freeze();
        return brush;
    }

    private void UpdateImage()
    {
        AsyncImage.SetDecodeWidth(_image, DecodeWidth);
        AsyncImage.SetPath(_image, ImagePath);
    }

    private void UpdateIcon()
    {
        AsyncImage.SetDecodeWidth(_icon, 128);
        AsyncImage.SetPath(_icon, IconPath);
        _arc.Visibility = string.IsNullOrEmpty(IconPath) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePlaceholder()
    {
        _placeholder.Background = CreatePlaceholderBrush(Title);
        _title.Text = Title;
        _title.FontSize = TitleSize;
        _title.Visibility = ShowTitle ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateCorner() => _root.CornerRadius = new System.Windows.CornerRadius(CornerRadius);

    private static int StableHash(string? value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var ch in value ?? string.Empty)
            {
                hash = (hash * 31) + char.ToLowerInvariant(ch);
            }

            return hash & 0x7FFFFFFF;
        }
    }
}
