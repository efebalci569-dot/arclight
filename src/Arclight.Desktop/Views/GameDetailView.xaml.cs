using System.Windows;
using System.Windows.Controls;
using Arclight.Desktop.Services;
using Arclight.Desktop.ViewModels;

namespace Arclight.Desktop.Views;

public partial class GameDetailView : UserControl
{
    public GameDetailView() => InitializeComponent();

    /// <summary>Opens the same menu as right-clicking a game card.</summary>
    private void OnMoreClick(object sender, RoutedEventArgs e) => OpenGameMenu(sender as FrameworkElement);

    private void OnAddToCollectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GameItemViewModel item } element)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = element };
        foreach (var option in item.CollectionOptions)
        {
            menu.Items.Add(new MenuItem { Header = (option.Contains ? "✓  " : string.Empty) + option.Name, Command = option.Command });
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(new MenuItem { Header = Loc.Get("Collection.Menu.New"), Command = item.NewCollectionCommand });
        menu.IsOpen = true;
    }

    private static void OpenGameMenu(FrameworkElement? anchor)
    {
        if (anchor?.DataContext is not GameItemViewModel item || Application.Current.TryFindResource("ContextMenu.Game") is not ContextMenu menu)
        {
            return;
        }

        menu.DataContext = item;
        menu.PlacementTarget = anchor;
        menu.IsOpen = true;
    }
}
