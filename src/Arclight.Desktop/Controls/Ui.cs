using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Arclight.Desktop.Controls;

/// <summary>Small attached properties shared by styles and views.</summary>
public static class Ui
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius", typeof(CornerRadius), typeof(Ui), new FrameworkPropertyMetadata(new CornerRadius(10)));

    /// <summary>Inherited flag set on the window when the user turns on "Reduce animations"; templates skip motion when true.</summary>
    public static readonly DependencyProperty ReduceMotionProperty = DependencyProperty.RegisterAttached(
        "ReduceMotion", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetReduceMotion(DependencyObject element) => (bool)element.GetValue(ReduceMotionProperty);

    public static void SetReduceMotion(DependencyObject element, bool value) => element.SetValue(ReduceMotionProperty, value);

    public static CornerRadius GetCornerRadius(DependencyObject element) => (CornerRadius)element.GetValue(CornerRadiusProperty);

    public static void SetCornerRadius(DependencyObject element, CornerRadius value) => element.SetValue(CornerRadiusProperty, value);

    /// <summary>
    /// Lets a horizontal ScrollViewer pass vertical wheel input to its parent so the page keeps
    /// scrolling. Shift+wheel scrolls the row horizontally.
    /// </summary>
    public static readonly DependencyProperty ForwardVerticalWheelProperty = DependencyProperty.RegisterAttached(
        "ForwardVerticalWheel", typeof(bool), typeof(Ui), new PropertyMetadata(false, OnForwardVerticalWheelChanged));

    public static bool GetForwardVerticalWheel(DependencyObject element) => (bool)element.GetValue(ForwardVerticalWheelProperty);

    public static void SetForwardVerticalWheel(DependencyObject element, bool value) => element.SetValue(ForwardVerticalWheelProperty, value);

    private static void OnForwardVerticalWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            element.PreviewMouseWheel -= ForwardWheel;
            if ((bool)e.NewValue)
            {
                element.PreviewMouseWheel += ForwardWheel;
            }
        }
    }

    private static void ForwardWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not DependencyObject source)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Shift && sender is ScrollViewer viewer)
        {
            viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta);
            e.Handled = true;
            return;
        }

        e.Handled = true;
        var parent = VisualTreeHelper.GetParent(source) as UIElement;
        parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender,
        });
    }

    /// <summary>
    /// Runs a command when the element is clicked, unless the click landed on a button inside it.
    /// Used for game cards: clicking the card opens details, while its own buttons keep their actions.
    /// </summary>
    public static readonly DependencyProperty ClickCommandProperty = DependencyProperty.RegisterAttached(
        "ClickCommand", typeof(ICommand), typeof(Ui), new PropertyMetadata(null, OnClickCommandChanged));

    public static ICommand? GetClickCommand(DependencyObject element) => (ICommand?)element.GetValue(ClickCommandProperty);

    public static void SetClickCommand(DependencyObject element, ICommand? value) => element.SetValue(ClickCommandProperty, value);

    public static readonly DependencyProperty ClickCommandParameterProperty = DependencyProperty.RegisterAttached(
        "ClickCommandParameter", typeof(object), typeof(Ui), new PropertyMetadata(null));

    public static object? GetClickCommandParameter(DependencyObject element) => element.GetValue(ClickCommandParameterProperty);

    public static void SetClickCommandParameter(DependencyObject element, object? value) => element.SetValue(ClickCommandParameterProperty, value);

    private static readonly DependencyProperty PressedProperty = DependencyProperty.RegisterAttached(
        "Pressed", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    private static void OnClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        element.MouseLeftButtonDown -= OnClickDown;
        element.MouseLeftButtonUp -= OnClickUp;
        if (e.NewValue is not null)
        {
            element.MouseLeftButtonDown += OnClickDown;
            element.MouseLeftButtonUp += OnClickUp;
        }
    }

    private static void OnClickDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element && !IsInsideButton(e.OriginalSource as DependencyObject, element))
        {
            element.SetValue(PressedProperty, true);
        }
    }

    private static void OnClickUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement element || !(bool)element.GetValue(PressedProperty))
        {
            return;
        }

        element.SetValue(PressedProperty, false);
        if (IsInsideButton(e.OriginalSource as DependencyObject, element) || !element.IsMouseOver)
        {
            return;
        }

        var command = GetClickCommand(element);
        var parameter = GetClickCommandParameter(element);
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
            e.Handled = true;
        }
    }

    private static bool IsInsideButton(DependencyObject? source, DependencyObject root)
    {
        while (source is not null && !ReferenceEquals(source, root))
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            source = source is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    /// <summary>Scrolls a horizontal row by most of its width (used by the arrow buttons on Home).</summary>
    public static void ScrollHorizontally(ScrollViewer viewer, int direction) =>
        viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset + (direction * Math.Max(200, viewer.ViewportWidth * 0.8)));
}

/// <summary>A Border that clips its content to its (uniform) rounded corners.</summary>
public sealed class ClipBorder : Border
{
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateClip();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == CornerRadiusProperty)
        {
            UpdateClip();
        }
    }

    private void UpdateClip()
    {
        var radius = CornerRadius.TopLeft;
        var clip = new RectangleGeometry(new Rect(RenderSize), radius, radius);
        clip.Freeze();
        Clip = clip;
    }
}
