using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Arclight.Desktop.Views;

public partial class LibraryView : UserControl
{
    public LibraryView() => InitializeComponent();

    /// <summary>Un-checks the dropdown chip when its popup closes (click outside, Esc).</summary>
    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (sender is Popup { Tag: ToggleButton toggle })
        {
            toggle.IsChecked = false;
        }
    }
}
