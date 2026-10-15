using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.ViewModels.Dialogs;

/// <summary>Create or edit a collection's name, description and cover.</summary>
public sealed partial class CollectionEditorViewModel : DialogViewModel
{
    private readonly LibraryService _library;
    private readonly ArtworkService _artwork;
    private readonly DialogService _dialogs;
    private readonly GameCollection? _existing;

    public CollectionEditorViewModel(LibraryService library, ArtworkService artwork, DialogService dialogs, GameCollection? existing)
    {
        _library = library;
        _artwork = artwork;
        _dialogs = dialogs;
        _existing = existing;
        _name = existing?.Name ?? string.Empty;
        _description = existing?.Description ?? string.Empty;
        _coverImagePath = existing?.CoverImagePath;
    }

    public override double DialogWidth => 520;

    public string Title => Loc.Get(_existing is null ? "CollectionEditor.Title.New" : "CollectionEditor.Title.Edit");

    public string SaveText => Loc.Get(_existing is null ? "CollectionEditor.Save.New" : "Editor.Save.Edit");

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private string? _coverImagePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasCover => !string.IsNullOrEmpty(CoverImagePath);

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private void PickCover()
    {
        if (_dialogs.PickImage(Loc.Get("CollectionEditor.Pick.Cover")) is { } path)
        {
            CoverImagePath = path;
        }
    }

    [RelayCommand]
    private void ClearCover() => CoverImagePath = null;

    [RelayCommand]
    private void Cancel() => Close(null);

    [RelayCommand]
    private async Task SaveAsync()
    {
        var name = Name.Trim();
        if (name.Length == 0)
        {
            ErrorMessage = Loc.Get("CollectionEditor.Error.NameRequired");
            return;
        }

        if (name.Length > 60)
        {
            ErrorMessage = Loc.Get("CollectionEditor.Error.NameTooLong");
            return;
        }

        try
        {
            var cover = CoverImagePath;
            if (cover is not null && cover != _existing?.CoverImagePath)
            {
                cover = _artwork.ImportUserImage(cover);
            }

            var description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            if (_existing is null)
            {
                Close(await _library.CreateCollectionAsync(name, description, cover));
            }
            else
            {
                var edited = _existing.Clone();
                edited.Name = name;
                edited.Description = description;
                edited.CoverImagePath = cover;
                await _library.UpdateCollectionAsync(edited);
                Close(edited);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ErrorMessage = ex.Message;
        }
    }
}
