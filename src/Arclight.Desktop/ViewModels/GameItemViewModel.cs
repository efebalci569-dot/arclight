using System.Windows.Input;
using Arclight.Core.Formatting;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.ViewModels;

public sealed record CollectionOption(string Name, bool Contains, ICommand Command);

/// <summary>
/// UI wrapper around a shared <see cref="Game"/>. One instance per game is reused by every page
/// (see <see cref="GameItemStore"/>) so a change made anywhere shows up everywhere.
/// </summary>
public sealed partial class GameItemViewModel : ObservableObject
{
    private readonly GameActions _actions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryActionText), nameof(StatusLine))]
    private bool _isRunning;

    public GameItemViewModel(Game game, GameActions actions)
    {
        Game = game;
        _actions = actions;
        PlayCommand = new AsyncRelayCommand(() => _actions.PrimaryActionAsync(Game));
        DetailsCommand = new RelayCommand(() => _actions.OpenDetails(Game));
        ToggleFavoriteCommand = new AsyncRelayCommand(() => _actions.ToggleFavoriteAsync(Game));
        EditCommand = new AsyncRelayCommand(() => _actions.EditAsync(Game));
        RemoveCommand = new AsyncRelayCommand(() => _actions.RemoveAsync(Game));
        OpenFolderCommand = new RelayCommand(() => _actions.OpenInstallFolder(Game), () => HasInstallFolder);
        FeatureCommand = new AsyncRelayCommand(() => _actions.FeatureOnHomeAsync(Game));
        NewCollectionCommand = new AsyncRelayCommand(() => _actions.AddToNewCollectionAsync(Game));
        LocateCommand = new AsyncRelayCommand(() => _actions.LocateAsync(Game));
    }

    public Game Game { get; }

    public long Id => Game.Id;

    public string Name => Game.Name;

    public GamePlatform Platform => Game.Platform;

    public string PlatformName => Loc.PlatformName(Game.Platform);

    public string PlatformBadge => Game.Platform switch
    {
        GamePlatform.Steam => "STEAM",
        GamePlatform.Epic => "EPIC",
        GamePlatform.Xbox => "XBOX",
        GamePlatform.Gog => "GOG",
        GamePlatform.Ubisoft => "UBISOFT",
        GamePlatform.EA => "EA",
        GamePlatform.Other => Loc.Get("Badge.Other"),
        _ => "PC",
    };

    public bool IsInstalled => Game.IsInstalled;

    public string InstallStatus => Loc.Get(Game.IsInstalled ? "Game.Installed" : "Game.NotInstalled");

    public bool IsFavorite => Game.IsFavorite;

    public long PlaytimeSeconds => Game.TotalPlaytimeSeconds;

    public string PlaytimeShort => Format.PlaytimeShort(Game.TotalPlaytimeSeconds);

    public string PlaytimeLong => Format.Playtime(Game.TotalPlaytimeSeconds);

    public string LastPlayedText => Format.Relative(Game.LastPlayed, DateTimeOffset.Now);

    public string LastPlayedDate => Format.Date(Game.LastPlayed);

    public string DateAddedText => Format.Date(Game.DateAdded);

    public int SessionCount => Game.SessionCount;

    public string SessionsText => Loc.Count("Count.Session", Game.SessionCount);

    public string? Genre => Game.Genre;

    public string YearText => Game.ReleaseYear?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";

    public int? Rating => Game.PersonalRating;

    public IReadOnlyList<string> Tags => Game.Tags;

    public string? CoverPath => Existing(Game.CoverImagePath);

    /// <summary>Landscape artwork for wide cards and heroes; falls back to the cover.</summary>
    public string? WideArtPath => Existing(Game.BackgroundImagePath) ?? CoverPath;

    public string? IconPath => Existing(Game.IconImagePath);

    public bool HasInstallFolder => !string.IsNullOrWhiteSpace(Game.InstallDirectory) || !string.IsNullOrWhiteSpace(Game.ExecutablePath);

    public PrimaryAction PrimaryAction => PlayService.GetPrimaryAction(Game);

    public string PrimaryActionText => Loc.Get(IsRunning ? "Game.Playing" : PrimaryAction switch
    {
        PrimaryAction.Install => "Action.Install",
        PrimaryAction.OpenLauncher => "Action.OpenLauncher",
        PrimaryAction.LocateGame => "Action.LocateGame",
        _ => "Action.Play",
    });

    public string MetaLine => $"{PlatformName}  ·  {PlaytimeShort}";

    public string StatusLine => IsRunning ? Loc.Get("Common.PlayingNow") : Game.IsInstalled ? LastPlayedText : Loc.Get("Game.NotInstalled");

    public IReadOnlyList<CollectionOption> CollectionOptions => _actions.GetCollectionOptions(Game);

    public ICommand PlayCommand { get; }

    public ICommand DetailsCommand { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand RemoveCommand { get; }

    public IRelayCommand OpenFolderCommand { get; }

    public ICommand FeatureCommand { get; }

    public ICommand NewCollectionCommand { get; }

    public ICommand LocateCommand { get; }

    /// <summary>Re-reads every property from the underlying game.</summary>
    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
        OpenFolderCommand.NotifyCanExecuteChanged();
    }

    public void RefreshCollections() => OnPropertyChanged(nameof(CollectionOptions));

    private static string? Existing(string? path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
}
