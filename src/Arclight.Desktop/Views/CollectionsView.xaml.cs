using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Arclight.Desktop.Services;
using Arclight.Desktop.ViewModels;

namespace Arclight.Desktop.Views;

/// <summary>Collection tiles with drag-and-drop reordering.</summary>
public partial class CollectionsView : UserControl
{
    private Point _pressPoint;
    private CollectionCardViewModel? _pressed;

    public CollectionsView() => InitializeComponent();

    private CollectionsViewModel? ViewModel => DataContext as CollectionsViewModel;

    private void OnTilePressed(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = e.GetPosition(this);
        _pressed = (sender as FrameworkElement)?.Tag as CollectionCardViewModel;
    }

    private void OnTileMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed is null || e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement element)
        {
            return;
        }

        var delta = e.GetPosition(this) - _pressPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance * 2 &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance * 2)
        {
            return;
        }

        var dragged = _pressed;
        _pressed = null;
        DragDrop.DoDragDrop(element, new DataObject(typeof(CollectionCardViewModel), dragged), DragDropEffects.Move);
    }

    private void OnTileDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(CollectionCardViewModel)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnTileDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is null || sender is not FrameworkElement { Tag: CollectionCardViewModel target } ||
            e.Data.GetData(typeof(CollectionCardViewModel)) is not CollectionCardViewModel dragged || ReferenceEquals(dragged, target))
        {
            return;
        }

        await ViewModel.MoveAsync(dragged, ViewModel.Collections.IndexOf(target));
        e.Handled = true;
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CollectionCardViewModel card } button || ViewModel is not { } vm)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = button };
        menu.Items.Add(new MenuItem { Header = Loc.Get("Collection.Menu.Open"), Command = vm.OpenCommand, CommandParameter = card });
        menu.Items.Add(new MenuItem { Header = Loc.Get("Game.Edit"), Command = vm.EditCommand, CommandParameter = card });
        menu.Items.Add(new Separator());
        var delete = new MenuItem { Header = Loc.Get("Library.DeleteCollection"), Command = vm.DeleteCommand, CommandParameter = card };
        delete.SetResourceReference(ForegroundProperty, "Brush.Danger");
        menu.Items.Add(delete);
        menu.IsOpen = true;
    }
}
