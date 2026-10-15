using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Arclight.Desktop.Controls;

/// <summary>Chip-style tag editor with suggestions from tags already used in the library.</summary>
public partial class TagEditor : UserControl
{
    public static readonly DependencyProperty TagsProperty = DependencyProperty.Register(
        nameof(Tags), typeof(ObservableCollection<string>), typeof(TagEditor), new PropertyMetadata(null));

    public static readonly DependencyProperty KnownTagsProperty = DependencyProperty.Register(
        nameof(KnownTags), typeof(IEnumerable<string>), typeof(TagEditor), new PropertyMetadata(null));

    public TagEditor()
    {
        InitializeComponent();
    }

    public ObservableCollection<string>? Tags
    {
        get => (ObservableCollection<string>?)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public IEnumerable<string>? KnownTags
    {
        get => (IEnumerable<string>?)GetValue(KnownTagsProperty);
        set => SetValue(KnownTagsProperty, value);
    }

    private void AddTag(string? text)
    {
        var tag = text?.Trim();
        if (string.IsNullOrEmpty(tag) || Tags is null)
        {
            return;
        }

        if (tag.Length > 40)
        {
            tag = tag[..40];
        }

        if (!Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            Tags.Add(tag);
        }

        Input.Clear();
        SuggestionPopup.IsOpen = false;
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab && !string.IsNullOrWhiteSpace(Input.Text))
        {
            AddTag(Input.Text);
            e.Handled = true;
        }
        else if (e.Key == Key.Back && Input.Text.Length == 0 && Tags is { Count: > 0 })
        {
            Tags.RemoveAt(Tags.Count - 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SuggestionPopup.IsOpen = false;
        }
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = Input.Text.Trim();
        if (text.Length == 0 || KnownTags is null)
        {
            SuggestionPopup.IsOpen = false;
            return;
        }

        var matches = KnownTags
            .Where(t => t.Contains(text, StringComparison.OrdinalIgnoreCase) && Tags?.Contains(t, StringComparer.OrdinalIgnoreCase) != true)
            .Take(6)
            .ToList();
        Suggestions.ItemsSource = matches;
        SuggestionPopup.IsOpen = matches.Count > 0;
    }

    private void OnInputLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!SuggestionPopup.IsKeyboardFocusWithin && !SuggestionPopup.IsMouseOver)
        {
            AddTag(Input.Text);
        }
    }

    private void OnSuggestionClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string tag })
        {
            AddTag(tag);
            Input.Focus();
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string tag })
        {
            Tags?.Remove(tag);
        }
    }
}
