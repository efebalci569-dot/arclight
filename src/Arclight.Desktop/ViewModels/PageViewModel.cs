using Arclight.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arclight.Desktop.ViewModels;

/// <summary>Base for pages shown in the shell's content area.</summary>
public abstract class PageViewModel : ObservableObject
{
    public abstract PageKey Key { get; }

    /// <summary>The sidebar item highlighted while this page is shown.</summary>
    public virtual PageKey NavKey => Key;

    /// <summary>Pages that list games react to the global search box.</summary>
    public virtual bool UsesSearch => false;

    public virtual void OnNavigatedTo(object? parameter)
    {
    }

    public virtual void OnNavigatedFrom()
    {
    }
}
