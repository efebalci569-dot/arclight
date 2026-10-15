using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Arclight.Core.Data;
using Arclight.Core.Launching;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.ViewModels.Dialogs;

/// <summary>A platform choice; <paramref name="NameKey"/> is set when the name needs translating.</summary>
public sealed record PlatformOption(GamePlatform Platform, string Brand, string? NameKey = null)
{
    public string Name => NameKey is null ? Brand : Loc.Get(NameKey);
}

/// <summary>Add a game manually or edit any game's details, launch settings and artwork.</summary>
public sealed partial class GameEditorViewModel : DialogViewModel
{
    private readonly LibraryService _library;
    private readonly ArtworkService _artwork;
    private readonly DialogService _dialogs;
    private readonly Game? _existing;
    private string? _lastAutoUri;

    public GameEditorViewModel(LibraryService library, ArtworkService artwork, DialogService dialogs, Game? existing, string? executablePath = null)
    {
        _library = library;
        _artwork = artwork;
        _dialogs = dialogs;
        _existing = existing;
        KnownTags = library.AllTags;
        KnownGenres = library.AllGenres;

        if (existing is not null)
        {
            _name = existing.Name;
            _selectedPlatform = Platforms.First(p => p.Platform == existing.Platform);
            _platformIdentifier = existing.PlatformIdentifier ?? string.Empty;
            _isExecutable = existing.LaunchMethod is LaunchMethod.Executable or LaunchMethod.None;
            _isLauncherUri = existing.LaunchMethod == LaunchMethod.LauncherUri;
            _isCustomCommand = existing.LaunchMethod == LaunchMethod.CustomCommand;
            _executablePath = existing.ExecutablePath ?? string.Empty;
            _launcherUri = existing.LaunchMethod == LaunchMethod.LauncherUri ? existing.LaunchTarget ?? string.Empty : string.Empty;
            _commandPath = existing.LaunchMethod == LaunchMethod.CustomCommand ? existing.LaunchTarget ?? string.Empty : string.Empty;
            _launchArguments = existing.LaunchArguments ?? string.Empty;
            _installDirectory = existing.InstallDirectory ?? string.Empty;
            _coverImagePath = existing.CoverImagePath;
            _backgroundImagePath = existing.BackgroundImagePath;
            _genre = existing.Genre ?? string.Empty;
            _releaseYear = existing.ReleaseYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _developer = existing.Developer ?? string.Empty;
            _publisher = existing.Publisher ?? string.Empty;
            _description = existing.Description ?? string.Empty;
            _notes = existing.Notes ?? string.Empty;
            _isInstalled = existing.IsInstalled;
            Tags = new ObservableCollection<string>(existing.Tags);
        }
        else
        {
            _selectedPlatform = Platforms[0];
            _isExecutable = true;
            _isInstalled = true;
            Tags = [];
            if (executablePath is not null)
            {
                ApplyExecutable(executablePath);
            }
        }
    }

    public static IReadOnlyList<PlatformOption> Platforms { get; } =
    [
        new(GamePlatform.Manual, "PC", "Editor.Platform.Pc"),
        new(GamePlatform.Steam, "Steam"),
        new(GamePlatform.Epic, "Epic Games"),
        new(GamePlatform.Gog, "GOG"),
        new(GamePlatform.Xbox, "Xbox / Microsoft Store"),
        new(GamePlatform.Ubisoft, "Ubisoft Connect"),
        new(GamePlatform.EA, "EA app"),
        new(GamePlatform.Other, "Other", "Editor.Platform.Other"),
    ];

    public override double DialogWidth => 720;

    public override bool CanDismiss => false;

    public bool IsNew => _existing is null;

    public string Title => IsNew ? Loc.Get("Editor.Title.Add") : Loc.Format("Editor.Title.Edit", _existing!.Name);

    public string SaveText => Loc.Get(IsNew ? "Editor.Save.Add" : "Editor.Save.Edit");

    public IReadOnlyList<string> KnownTags { get; }

    public IReadOnlyList<string> KnownGenres { get; }

    public ObservableCollection<string> Tags { get; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlatformIdLabel), nameof(ShowPlatformId))]
    private PlatformOption _selectedPlatform;

    [ObservableProperty]
    private string _platformIdentifier = string.Empty;

    [ObservableProperty]
    private bool _isExecutable;

    [ObservableProperty]
    private bool _isLauncherUri;

    [ObservableProperty]
    private bool _isCustomCommand;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _launcherUri = string.Empty;

    [ObservableProperty]
    private string _commandPath = string.Empty;

    [ObservableProperty]
    private string _launchArguments = string.Empty;

    [ObservableProperty]
    private string _installDirectory = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private string? _coverImagePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackground))]
    private string? _backgroundImagePath;

    [ObservableProperty]
    private string _genre = string.Empty;

    [ObservableProperty]
    private string _releaseYear = string.Empty;

    [ObservableProperty]
    private string _developer = string.Empty;

    [ObservableProperty]
    private string _publisher = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSaving;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasCover => !string.IsNullOrEmpty(CoverImagePath);

    public bool HasBackground => !string.IsNullOrEmpty(BackgroundImagePath);

    public bool ShowPlatformId => SelectedPlatform.Platform is not (GamePlatform.Manual or GamePlatform.Other);

    public string PlatformIdLabel => SelectedPlatform.Platform switch
    {
        GamePlatform.Steam => Loc.Get("Editor.PlatformId.Steam"),
        GamePlatform.Epic => Loc.Get("Editor.PlatformId.Epic"),
        GamePlatform.Gog => Loc.Get("Editor.PlatformId.Gog"),
        GamePlatform.Xbox => Loc.Get("Editor.PlatformId.Xbox"),
        GamePlatform.Ubisoft => Loc.Get("Editor.PlatformId.Ubisoft"),
        _ => Loc.Get("Editor.PlatformId.Other"),
    };

    partial void OnPlatformIdentifierChanged(string value)
    {
        // Typing a Steam app id fills in the matching launcher link.
        var id = value.Trim();
        if (SelectedPlatform.Platform == GamePlatform.Steam && id.Length > 0 && id.All(char.IsAsciiDigit) &&
            (string.IsNullOrWhiteSpace(LauncherUri) || LauncherUri == _lastAutoUri))
        {
            _lastAutoUri = LauncherUris.SteamRun(id);
            LauncherUri = _lastAutoUri;
            IsLauncherUri = true;
            IsExecutable = IsCustomCommand = false;
        }
    }

    [RelayCommand]
    private void BrowseExecutable()
    {
        if (_dialogs.PickExecutable(Directory.Exists(InstallDirectory) ? InstallDirectory : null) is { } path)
        {
            ApplyExecutable(path);
        }
    }

    [RelayCommand]
    private void BrowseCommand()
    {
        if (_dialogs.PickExecutable() is { } path)
        {
            CommandPath = path;
        }
    }

    [RelayCommand]
    private void BrowseInstallFolder()
    {
        if (_dialogs.PickFolder(Loc.Get("Editor.Pick.InstallFolder")) is { } folder)
        {
            InstallDirectory = folder;
        }
    }

    [RelayCommand]
    private void PickCover()
    {
        if (_dialogs.PickImage(Loc.Get("Editor.Pick.Cover")) is { } path)
        {
            CoverImagePath = path;
        }
    }

    [RelayCommand]
    private void ClearCover() => CoverImagePath = null;

    [RelayCommand]
    private void PickBackground()
    {
        if (_dialogs.PickImage(Loc.Get("Editor.Pick.Background")) is { } path)
        {
            BackgroundImagePath = path;
        }
    }

    [RelayCommand]
    private void ClearBackground() => BackgroundImagePath = null;

    [RelayCommand]
    private void Cancel() => Close(null);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        var game = _existing?.Clone() ?? new Game { Source = GameSource.Manual };
        game.Name = Name.Trim();
        game.Platform = SelectedPlatform.Platform;
        game.PlatformIdentifier = NullIfEmpty(PlatformIdentifier);
        game.LaunchArguments = NullIfEmpty(LaunchArguments);
        game.ExecutablePath = NullIfEmpty(ExecutablePath);
        game.InstallDirectory = NullIfEmpty(InstallDirectory);
        game.Genre = NullIfEmpty(Genre);
        game.Developer = NullIfEmpty(Developer);
        game.Publisher = NullIfEmpty(Publisher);
        game.Description = NullIfEmpty(Description);
        game.IsInstalled = IsInstalled;
        game.Tags = [.. Tags];
        if (!string.Equals(game.Notes ?? string.Empty, Notes, StringComparison.Ordinal))
        {
            game.Notes = NullIfEmpty(Notes);
            game.NotesUpdatedAt = DateTimeOffset.UtcNow;
        }

        if (IsLauncherUri)
        {
            game.LaunchMethod = LaunchMethod.LauncherUri;
            game.LaunchTarget = LauncherUri.Trim();
        }
        else if (IsCustomCommand)
        {
            game.LaunchMethod = LaunchMethod.CustomCommand;
            game.LaunchTarget = CommandPath.Trim().Trim('"');
        }
        else
        {
            game.LaunchMethod = LaunchMethod.Executable;
            game.LaunchTarget = null;
            game.ExecutablePath = ExecutablePath.Trim().Trim('"');
        }

        if (game.LaunchMethod == LaunchMethod.Executable && game.InstallDirectory is null && game.ExecutablePath is { Length: > 0 } exe)
        {
            game.InstallDirectory = Path.GetDirectoryName(exe);
        }

        if (string.IsNullOrWhiteSpace(ReleaseYear))
        {
            game.ReleaseYear = null;
        }
        else if (int.TryParse(ReleaseYear.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year is >= 1950 and <= 2100)
        {
            game.ReleaseYear = year;
        }
        else
        {
            ErrorMessage = Loc.Get("Editor.Error.ReleaseYear");
            return;
        }

        var validation = LaunchValidator.ValidateGame(game);
        if (!validation.IsValid)
        {
            ErrorMessage = Loc.Validation(validation);
            return;
        }

        if (game.LaunchMethod == LaunchMethod.Executable && !File.Exists(game.ExecutablePath))
        {
            ErrorMessage = Loc.Get("Editor.Error.ExeMissing");
            return;
        }

        IsSaving = true;
        try
        {
            game.CoverImagePath = ImportIfNeeded(CoverImagePath);
            game.BackgroundImagePath = ImportIfNeeded(BackgroundImagePath);
            if (game.IconImagePath is null && game.ExecutablePath is { } exePath && File.Exists(exePath))
            {
                game.IconImagePath = await Task.Run(() => _artwork.ExtractIcon(exePath, exePath));
            }

            if (_existing is null)
            {
                await _library.AddGameAsync(game);
            }
            else
            {
                await _library.UpdateGameAsync(game);
            }

            Close(game);
        }
        catch (DuplicateGameException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ApplyExecutable(string path)
    {
        ExecutablePath = path;
        IsExecutable = true;
        IsLauncherUri = IsCustomCommand = false;
        if (string.IsNullOrWhiteSpace(InstallDirectory))
        {
            InstallDirectory = Path.GetDirectoryName(path) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = SuggestName(path);
        }
    }

    /// <summary>Uses the exe's product name when it looks like a real title, otherwise its folder name.</summary>
    internal static string SuggestName(string path)
    {
        try
        {
            var product = FileVersionInfo.GetVersionInfo(path).ProductName?.Trim();
            if (!string.IsNullOrWhiteSpace(product) && product.Length is > 1 and < 60 &&
                !product.Contains("Unity", StringComparison.OrdinalIgnoreCase) &&
                !product.Contains("Unreal", StringComparison.OrdinalIgnoreCase) &&
                !product.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                return product;
            }
        }
        catch (FileNotFoundException)
        {
        }

        var folder = new DirectoryInfo(Path.GetDirectoryName(path) ?? string.Empty).Name;
        return folder is { Length: > 1 } && !folder.Equals("bin", StringComparison.OrdinalIgnoreCase) && !folder.StartsWith("x64", StringComparison.OrdinalIgnoreCase) && !folder.Equals("Win64", StringComparison.OrdinalIgnoreCase)
            ? folder
            : Path.GetFileNameWithoutExtension(path);
    }

    private string? ImportIfNeeded(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Images already managed by Arclight are kept as they are.
        if (path == _existing?.CoverImagePath || path == _existing?.BackgroundImagePath || _artwork.IsCachedPath(path))
        {
            return path;
        }

        return _artwork.ImportUserImage(path);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
