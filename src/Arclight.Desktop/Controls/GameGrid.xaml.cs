using System.Windows;
using System.Windows.Controls;
using Arclight.Core.Models;
using Arclight.Desktop.ViewModels;

namespace Arclight.Desktop.Controls;

public sealed record GameRow(IReadOnlyList<GameItemViewModel> Items);

/// <summary>
/// Shows games as a card grid, compact grid or list. The grid is split into rows so the standard
/// VirtualizingStackPanel can virtualize it: thousands of games only create the rows on screen.
/// Cards stretch so each row fills the available width.
/// </summary>
public partial class GameGrid : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IReadOnlyList<GameItemViewModel>), typeof(GameGrid), new PropertyMetadata(null, (d, _) => ((GameGrid)d).Rebuild(force: true)));

    public static readonly DependencyProperty ViewModeProperty = DependencyProperty.Register(
        nameof(ViewMode), typeof(LibraryViewMode), typeof(GameGrid), new PropertyMetadata(LibraryViewMode.Grid, (d, _) => ((GameGrid)d).Rebuild(force: true)));

    public static readonly DependencyProperty BaseCardWidthProperty = DependencyProperty.Register(
        nameof(BaseCardWidth), typeof(double), typeof(GameGrid), new PropertyMetadata(188.0, (d, _) => ((GameGrid)d).Rebuild(force: true)));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(GameGrid), new PropertyMetadata(20.0, (d, _) => ((GameGrid)d).Rebuild(force: true)));

    public static readonly DependencyProperty CardWidthProperty = DependencyProperty.Register(
        nameof(CardWidth), typeof(double), typeof(GameGrid), new PropertyMetadata(188.0));

    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(GameGrid), new PropertyMetadata(1));

    public static readonly DependencyProperty RowWidthProperty = DependencyProperty.Register(
        nameof(RowWidth), typeof(double), typeof(GameGrid), new PropertyMetadata(double.NaN));

    public static readonly DependencyProperty RowMarginProperty = DependencyProperty.Register(
        nameof(RowMargin), typeof(Thickness), typeof(GameGrid), new PropertyMetadata(new Thickness(0, 0, 0, 20)));

    public static readonly DependencyProperty CardTemplateProperty = DependencyProperty.Register(
        nameof(CardTemplate), typeof(DataTemplate), typeof(GameGrid), new PropertyMetadata(null));

    private int _lastColumns = -1;

    public GameGrid()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Rebuild(force: false);
    }

    public IReadOnlyList<GameItemViewModel>? ItemsSource
    {
        get => (IReadOnlyList<GameItemViewModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public LibraryViewMode ViewMode
    {
        get => (LibraryViewMode)GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    public double BaseCardWidth
    {
        get => (double)GetValue(BaseCardWidthProperty);
        set => SetValue(BaseCardWidthProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double CardWidth
    {
        get => (double)GetValue(CardWidthProperty);
        private set => SetValue(CardWidthProperty, value);
    }

    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        private set => SetValue(ColumnsProperty, value);
    }

    public double RowWidth
    {
        get => (double)GetValue(RowWidthProperty);
        private set => SetValue(RowWidthProperty, value);
    }

    public Thickness RowMargin
    {
        get => (Thickness)GetValue(RowMarginProperty);
        private set => SetValue(RowMarginProperty, value);
    }

    public DataTemplate? CardTemplate
    {
        get => (DataTemplate?)GetValue(CardTemplateProperty);
        private set => SetValue(CardTemplateProperty, value);
    }

    private void Rebuild(bool force)
    {
        var items = ItemsSource ?? [];
        var isList = ViewMode == LibraryViewMode.List;
        GridHost.Visibility = isList ? Visibility.Collapsed : Visibility.Visible;
        ListPanel.Visibility = isList ? Visibility.Visible : Visibility.Collapsed;

        if (isList)
        {
            if (force || !ReferenceEquals(ListHost.ItemsSource, items))
            {
                ListHost.ItemsSource = items;
            }

            GridHost.ItemsSource = null;
            _lastColumns = -1;
            return;
        }

        ListHost.ItemsSource = null;
        CardTemplate = (DataTemplate)FindResource(ViewMode == LibraryViewMode.CompactGrid ? "Template.GameCardCompact" : "Template.GameCard");

        // Horizontal padding of the scroll host plus room for the scrollbar.
        var available = Math.Max(0, ActualWidth - GridHost.Padding.Left - GridHost.Padding.Right - 4);
        if (available <= 0)
        {
            return;
        }

        var gap = Gap;
        var columns = Math.Max(1, (int)((available + gap) / (BaseCardWidth + gap)));
        var cell = available / columns;
        CardWidth = Math.Floor(cell - gap);
        RowWidth = cell * columns;
        RowMargin = new Thickness(-gap / 2, 0, 0, ViewMode == LibraryViewMode.CompactGrid ? gap : gap + 4);

        if (!force && columns == _lastColumns)
        {
            return;
        }

        _lastColumns = columns;
        Columns = columns;
        var rows = new List<GameRow>((items.Count / columns) + 1);
        for (var i = 0; i < items.Count; i += columns)
        {
            rows.Add(new GameRow(items.Skip(i).Take(columns).ToList()));
        }

        GridHost.ItemsSource = rows;
    }
}
