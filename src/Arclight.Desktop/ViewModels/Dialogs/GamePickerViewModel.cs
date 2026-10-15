using System.Collections.ObjectModel;
using Arclight.Core.Library;
using Arclight.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Arclight.Desktop.Services;

namespace Arclight.Desktop.ViewModels.Dialogs;

public sealed partial class PickableGame(GameItemViewModel item) : ObservableObject
{
    public GameItemViewModel Item { get; } = item;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Search-and-pick list used to choose the featured game or add games to a collection.</summary>
public sealed partial class GamePickerViewModel : DialogViewModel
{
    private readonly List<PickableGame> _all;

    public GamePickerViewModel(IEnumerable<GameItemViewModel> games, string title, string confirmText, bool multiSelect, IReadOnlySet<long>? excluded = null)
    {
        Title = title;
        ConfirmText = confirmText;
        IsMultiSelect = multiSelect;
        _all = games
            .Where(g => excluded is null || !excluded.Contains(g.Id))
            .OrderBy(g => g.Name, NaturalStringComparer.Instance)
            .Select(g => new PickableGame(g))
            .ToList();
        foreach (var game in _all)
        {
            game.PropertyChanged += (_, _) => ConfirmCommand.NotifyCanExecuteChanged();
        }

        ApplyFilter();
    }

    public string Title { get; }

    public string ConfirmText { get; }

    public bool IsMultiSelect { get; }

    public override double DialogWidth => 560;

    public ObservableCollection<PickableGame> Visible { get; } = [];

    public bool IsEmpty => _all.Count == 0;

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void Pick(PickableGame game)
    {
        if (IsMultiSelect)
        {
            game.IsSelected = !game.IsSelected;
        }
        else
        {
            Close(new List<Game> { game.Item.Game });
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => Close(_all.Where(g => g.IsSelected).Select(g => g.Item.Game).ToList());

    private bool CanConfirm() => _all.Any(g => g.IsSelected);

    [RelayCommand]
    private void Cancel() => Close(null);

    private void ApplyFilter()
    {
        var terms = TextSearch.Tokenize(SearchText);
        Visible.Clear();
        foreach (var game in _all.Where(g => terms.Count == 0 || terms.All(t => TextSearch.Normalize(g.Item.Name).Contains(t, StringComparison.Ordinal))))
        {
            Visible.Add(game);
        }
    }
}
