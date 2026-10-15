using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Threading;
using Arclight.Core.Formatting;
using Arclight.Core.Launching;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Core.Tracking;
using Arclight.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.ViewModels;

public sealed record InfoRow(string Label, string Value);

/// <summary>Everything personal about one game: stats, notes, rating, tags, collections and sessions.</summary>
public sealed partial class GameDetailViewModel : PageViewModel
{
    private readonly LibraryService _library;
    private readonly GameItemStore _items;
    private readonly GameActions _actions;
    private readonly Navigator _navigator;
    private readonly DispatcherTimer _notesTimer;
    private bool _loading;

    public GameDetailViewModel(LibraryService library, GameItemStore items, GameActions actions, Navigator navigator)
    {
        _library = library;
        _items = items;
        _actions = actions;
        _navigator = navigator;
        _notesTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _notesTimer.Tick += async (_, _) =>
        {
            _notesTimer.Stop();
            await SaveNotesAsync();
        };
        Tags.CollectionChanged += OnTagsChanged;
        _items.Changed += OnItemsChanged;
        Loc.LanguageChanged += (_, _) =>
        {
            if (Item is not null)
            {
                Load(Item.Id);
            }
        };
    }

    public override PageKey Key => PageKey.GameDetail;

    public override PageKey NavKey => PageKey.Library;

    public ObservableCollection<string> Tags { get; } = [];

    public ObservableCollection<SessionRow> Sessions { get; } = [];

    public ObservableCollection<InfoRow> Info { get; } = [];

    public IReadOnlyList<string> KnownTags => _library.AllTags;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItem))]
    private GameItemViewModel? _item;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _notesStatus = string.Empty;

    [ObservableProperty]
    private int? _rating;

    [ObservableProperty]
    private bool _trackingEnabled;

    [ObservableProperty]
    private bool _hasSessions;

    [ObservableProperty]
    private string? _description;

    public bool HasItem => Item is not null;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool CanTrack => Item is not null && TrackingService.IsTrackable(Item.Game);

    public string TrackingHint => Item is null
        ? string.Empty
        : !_library.Settings.PlaytimeTrackingEnabled
            ? Loc.Get("Detail.Hint.TrackingOff")
            : CanTrack
                ? Loc.Get("Detail.Hint.Tracked")
                : Loc.Get("Detail.Hint.Untrackable");

    public IReadOnlyList<CollectionOption> CollectionOptions => Item?.CollectionOptions ?? [];

    public IReadOnlyList<GameCollection> MemberOf => Item is null ? [] : _library.CollectionsContaining(Item.Id);

    public bool HasCollections => MemberOf.Count > 0;

    public override void OnNavigatedTo(object? parameter)
    {
        if (parameter is long id)
        {
            Load(id);
        }
    }

    public override void OnNavigatedFrom()
    {
        if (_notesTimer.IsEnabled)
        {
            _notesTimer.Stop();
            _ = SaveNotesAsync();
        }
    }

    private async void Load(long id)
    {
        _loading = true;
        Item = _items.Find(id);
        if (Item is null)
        {
            _loading = false;
            return;
        }

        var game = Item.Game;
        Notes = game.Notes ?? string.Empty;
        NotesStatus = game.NotesUpdatedAt is { } at ? Loc.Format("Detail.Notes.Saved", Format.Relative(at, DateTimeOffset.Now).ToLower(Loc.Culture)) : Loc.Get("Detail.Notes.AutoSave");
        Rating = game.PersonalRating;
        TrackingEnabled = game.IsTrackingEnabled;
        Description = game.Description;
        Tags.Clear();
        foreach (var tag in game.Tags)
        {
            Tags.Add(tag);
        }

        BuildInfo(game);
        OnPropertyChanged(nameof(KnownTags));
        OnPropertyChanged(nameof(CollectionOptions));
        OnPropertyChanged(nameof(MemberOf));
        OnPropertyChanged(nameof(HasCollections));
        OnPropertyChanged(nameof(HasDescription));
        OnPropertyChanged(nameof(CanTrack));
        OnPropertyChanged(nameof(TrackingHint));
        _loading = false;

        var sessions = await _library.GetSessionsAsync(gameId: id, limit: 12);
        Sessions.Clear();
        foreach (var session in sessions)
        {
            var local = session.StartedAt.ToLocalTime();
            Sessions.Add(new SessionRow(Item, game.Name,
                local.ToString(Loc.Get("Detail.SessionDateFormat"), Loc.Culture),
                local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                Format.Playtime(session.DurationSeconds),
                Loc.Get(session.Source == SessionSource.BackgroundWatcher ? "Session.Source.Watcher" : "Session.Source.Arclight"),
                session.Source == SessionSource.BackgroundWatcher ? "" : ""));
        }

        HasSessions = Sessions.Count > 0;
    }

    private void BuildInfo(Game game)
    {
        Info.Clear();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                Info.Add(new InfoRow(label, value));
            }
        }

        Add(Loc.Get("Editor.Platform"), Loc.PlatformName(game.Platform));
        Add(Loc.Get("Editor.Genre"), game.Genre);
        Add(Loc.Get("Editor.ReleaseYear"), game.ReleaseYear?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(Loc.Get("Editor.Developer"), game.Developer);
        Add(Loc.Get("Editor.Publisher"), game.Publisher);
        Add(Loc.Get("Detail.Info.Added"), Format.Date(game.DateAdded));
        Add(Loc.Get("Detail.Info.Launch"), game.LaunchMethod switch
        {
            LaunchMethod.LauncherUri when LaunchValidator.GetUriScheme(game.LaunchTarget) == "shell" => Loc.Get("Detail.Info.WindowsApp"),
            LaunchMethod.LauncherUri => Loc.Format("Detail.Info.Through", Loc.PlatformName(game.Platform)),
            LaunchMethod.CustomCommand => Loc.Get("Editor.CustomCommand"),
            LaunchMethod.Executable => Loc.Get("Detail.Info.Executable"),
            _ => Loc.Get("Detail.Info.NotSet"),
        });
        Add(Loc.Get("Detail.Info.InstallFolder"), game.InstallDirectory);
        Add(Loc.Get("Detail.Info.Executable"), game.ExecutablePath);
        if (game.Platform == GamePlatform.Steam)
        {
            Add("Steam App ID", game.PlatformIdentifier);
        }
    }

    partial void OnNotesChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        NotesStatus = Loc.Get("Detail.Notes.Editing");
        _notesTimer.Stop();
        _notesTimer.Start();
    }

    partial void OnRatingChanged(int? value)
    {
        if (!_loading && Item is not null && value != Item.Game.PersonalRating)
        {
            _ = _library.SetRatingAsync(Item.Id, value);
        }
    }

    partial void OnTrackingEnabledChanged(bool value)
    {
        if (!_loading && Item is not null && value != Item.Game.IsTrackingEnabled)
        {
            _ = _library.SetTrackingEnabledAsync(Item.Id, value);
        }
    }

    private void OnTagsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_loading && Item is not null)
        {
            _ = _library.SetTagsAsync(Item.Id, Tags.ToList());
        }
    }

    private async Task SaveNotesAsync()
    {
        if (Item is null)
        {
            return;
        }

        var text = string.IsNullOrWhiteSpace(Notes) ? null : Notes;
        if (text != Item.Game.Notes)
        {
            await _library.SaveNotesAsync(Item.Id, text);
        }

        NotesStatus = Loc.Get("Detail.Notes.SavedNow");
    }

    private void OnItemsChanged(object? sender, LibraryChangedEventArgs e)
    {
        if (Item is null)
        {
            return;
        }

        if (e.Kind == LibraryChangeKind.GamesRemoved && e.GameIds.Contains(Item.Id))
        {
            Item = null;
            _navigator.Back();
            return;
        }

        if (e.Kind == LibraryChangeKind.CollectionsChanged)
        {
            OnPropertyChanged(nameof(CollectionOptions));
            OnPropertyChanged(nameof(MemberOf));
            OnPropertyChanged(nameof(HasCollections));
        }
        else if (e.Kind == LibraryChangeKind.GamesUpdated && e.GameIds.Contains(Item.Id))
        {
            _loading = true;
            Description = Item.Game.Description;
            BuildInfo(Item.Game);
            if (!Item.Game.Tags.SequenceEqual(Tags))
            {
                Tags.Clear();
                foreach (var tag in Item.Game.Tags)
                {
                    Tags.Add(tag);
                }
            }

            OnPropertyChanged(nameof(HasDescription));
            OnPropertyChanged(nameof(CanTrack));
            OnPropertyChanged(nameof(TrackingHint));
            _loading = false;
        }
        else if (e.Kind == LibraryChangeKind.PlaytimeChanged && e.GameIds.Contains(Item.Id))
        {
            Load(Item.Id);
        }
    }

    [RelayCommand]
    private void Back() => _navigator.Back();

    [RelayCommand]
    private void RemoveFromCollection(GameCollection collection)
    {
        if (Item is not null)
        {
            _ = _library.RemoveFromCollectionAsync(collection.Id, Item.Id);
        }
    }
}
