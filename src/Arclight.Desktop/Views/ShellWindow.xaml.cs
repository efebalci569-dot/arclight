using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Arclight.Desktop.Controls;
using Arclight.Desktop.Services;
using Arclight.Desktop.ViewModels;

namespace Arclight.Desktop.Views;

public partial class ShellWindow : Window
{
    private readonly ShellViewModel _shell;

    public ShellWindow(ShellViewModel shell)
    {
        InitializeComponent();
        _shell = shell;
        DataContext = shell;
        shell.PropertyChanged += OnShellPropertyChanged;
        SourceInitialized += (_, _) => NativeWindow.ApplyDarkChrome(this);
        StateChanged += (_, _) => UpdateWindowState();
        PreviewKeyDown += OnPreviewKeyDown;
        MouseDown += OnMouseButtons;
    }

    /// <summary>Set by the app: when true, closing the window hides it to the tray instead of exiting.</summary>
    public Func<bool>? ShouldHideOnClose { get; set; }

    public bool IsExiting { get; set; }

    public void SetReduceMotion(bool value) => Ui.SetReduceMotion(this, value);

    public void BringToFront()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!IsExiting && ShouldHideOnClose?.Invoke() == true)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void UpdateWindowState()
    {
        // A maximized chrome-less window overhangs the screen by the resize border; pad it back in.
        if (WindowState == WindowState.Maximized)
        {
            var frame = SystemParameters.WindowResizeBorderThickness;
            Root.Margin = new Thickness(frame.Left + 2, frame.Top + 2, frame.Right + 2, frame.Bottom + 2);
            MaximizeButton.Content = "";
            MaximizeButton.SetResourceReference(ToolTipProperty, "Shell.Restore");
        }
        else
        {
            Root.Margin = new Thickness(0);
            MaximizeButton.Content = "";
            MaximizeButton.SetResourceReference(ToolTipProperty, "Shell.Maximize");
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellViewModel.CurrentPage) || ThemeService.ReduceMotion)
        {
            return;
        }

        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180)));
        if (PageHost.RenderTransform is TranslateTransform slide)
        {
            slide.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.F && modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.N && modifiers == ModifierKeys.Control && _shell.Dialogs.OpenDialogs.Count == 0)
        {
            _shell.AddGameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F5 && _shell.Dialogs.OpenDialogs.Count == 0)
        {
            _shell.ScanCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_shell.Dialogs.DismissTop())
            {
                e.Handled = true;
            }
            else if (SearchBox.IsKeyboardFocusWithin && SearchBox.Text.Length > 0)
            {
                _shell.Search.Text = string.Empty;
                e.Handled = true;
            }
            else if (_shell.CanGoBack)
            {
                _shell.GoBack();
                e.Handled = true;
            }
        }
        else if ((e.Key == Key.Left || e.SystemKey == Key.Left) && modifiers == ModifierKeys.Alt && _shell.CanGoBack)
        {
            _shell.GoBack();
            e.Handled = true;
        }
    }

    private void OnMouseButtons(object sender, MouseButtonEventArgs e)
    {
        // The mouse "back" button navigates back like in a browser.
        if (e.ChangedButton == MouseButton.XButton1 && _shell.CanGoBack)
        {
            _shell.GoBack();
            e.Handled = true;
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
