using System.Windows;
using System.Windows.Controls;
using Arclight.Desktop.Controls;

namespace Arclight.Desktop.Views;

public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();

    private void OnContinueLeft(object sender, RoutedEventArgs e) => Ui.ScrollHorizontally(ContinueScroller, -1);

    private void OnContinueRight(object sender, RoutedEventArgs e) => Ui.ScrollHorizontally(ContinueScroller, 1);

    private void OnRecentLeft(object sender, RoutedEventArgs e) => Ui.ScrollHorizontally(RecentScroller, -1);

    private void OnRecentRight(object sender, RoutedEventArgs e) => Ui.ScrollHorizontally(RecentScroller, 1);
}
