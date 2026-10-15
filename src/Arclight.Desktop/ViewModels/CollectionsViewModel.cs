using System.Collections.ObjectModel;
using Arclight.Core.Formatting;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Desktop.Services;
using Arclight.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.ViewModels;

public sealed class CollectionCardViewModel(GameCollection collection, IReadOnlyList<string?> previewCovers, IReadOnlyList<string> previewNames)
{
    public GameCollection Collection { get; } = collection;

    public long Id => Collection.Id;

    public string Name => Collection.Name;

    public string? Description => Collection.Description;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Collection.Description);

    public string CountText => Loc.Count("Count.Game", Collection.GameIds.Count);

    public string? CoverPath => Collection.CoverImagePath is { } path && File.Exists(path) ? path : null;

    public bool HasCustomCover => CoverPath is not null;

    /// <summary>Up to four game covers shown as a mosaic when the collection has no cover of its own.</summary>
    public IReadOnlyList<string?> PreviewCovers { get; } = previewCovers;

    public IReadOnlyList<string> PreviewNames { get; } = previewNames;

    public bool IsEmpty => Collection.GameIds.Count == 0;
}

public sealed partial class CollectionsViewModel : PageViewModel
{
    private readonly LibraryService _library;
    private readonly ArtworkService _artwork;
    private readonly DialogService _dialogs;
    private readonly Navigator _navigator;
    private readonly NotificationService _notifications;

    public CollectionsViewModel(LibraryService library, GameItemStore items, ArtworkService artwork, DialogService dialogs, Navigator navigator, NotificationService notifications)
    {
        _library = library;
        _artwork = artwork;
        _dialogs = dialogs;
        _navigator = navigator;
        _notifications = notifications;
        items.Changed += (_, e) =>
        {
            if (e.Kind is LibraryChangeKind.CollectionsChanged or LibraryChangeKind.GamesRemoved or LibraryChangeKind.Reloaded or LibraryChangeKind.GamesUpdated)
            {
                Refresh();
            }
        };
        Loc.LanguageChanged += (_, _) => Refresh();
    }

    public override PageKey Key => PageKey.Collections;

    public ObservableCollection<CollectionCardViewModel> Collections { get; } = [];

    [ObservableProperty]
    private bool _isEmpty;

    public override void OnNavigatedTo(object? parameter) => Refresh();

    public void Refresh()
    {
        Collections.Clear();
        foreach (var collection in _library.Collections)
        {
            var games = collection.GameIds.Select(_library.FindGame).Where(g => g is not null).Take(4).ToList();

            // Always four slots so the mosaic can bind by index; empty slots show the placeholder gradient.
            var covers = games.Select(g => g!.CoverImagePath is { } p && File.Exists(p) ? p : null).ToList();
            var names = games.Select(g => g!.Name).ToList();
            while (covers.Count < 4)
            {
                covers.Add(null);
                names.Add(collection.Name + covers.Count);
            }

            Collections.Add(new CollectionCardViewModel(collection, covers, names));
        }

        IsEmpty = Collections.Count == 0;
    }

    [RelayCommand]
    private void Open(CollectionCardViewModel card) => _navigator.Go(PageKey.CollectionDetail, card.Id);

    [RelayCommand]
    private async Task CreateAsync()
    {
        var editor = new CollectionEditorViewModel(_library, _artwork, _dialogs, existing: null);
        if (await _dialogs.ShowAsync<GameCollection>(editor) is { } created)
        {
            _notifications.Show(NoticeKind.Success, Loc.Format("Collection.Created.Title", created.Name), Loc.Get("Collection.Created.Message"), keepInHistory: false);
        }
    }

    [RelayCommand]
    private async Task EditAsync(CollectionCardViewModel card) =>
        await _dialogs.ShowAsync<GameCollection>(new CollectionEditorViewModel(_library, _artwork, _dialogs, card.Collection));

    [RelayCommand]
    private async Task DeleteAsync(CollectionCardViewModel card)
    {
        var answer = await _dialogs.ConfirmAsync(
            Loc.Format("Collection.Delete.Title", card.Name),
            Loc.Get("Collection.Delete.Message"),
            Loc.Get("Library.DeleteCollection"),
            danger: true);
        if (answer.Confirmed)
        {
            await _library.DeleteCollectionAsync(card.Id);
        }
    }

    /// <summary>Called by the view after a drag-and-drop reorder.</summary>
    public async Task MoveAsync(CollectionCardViewModel card, int newIndex)
    {
        var oldIndex = Collections.IndexOf(card);
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Collections.Count || oldIndex == newIndex)
        {
            return;
        }

        Collections.Move(oldIndex, newIndex);
        await _library.ReorderCollectionsAsync(Collections.Select(c => c.Id).ToList());
    }
}
