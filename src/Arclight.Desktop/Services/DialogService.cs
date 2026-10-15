using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace Arclight.Desktop.Services;

/// <summary>Base for in-window dialogs. They render over the shell with a dimmed backdrop.</summary>
public abstract partial class DialogViewModel : ObservableObject
{
    private readonly TaskCompletionSource<object?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<object?> Completion => _completion.Task;

    /// <summary>Width of the dialog card in device-independent pixels.</summary>
    public virtual double DialogWidth => 520;

    /// <summary>Whether Esc and clicking the backdrop close the dialog.</summary>
    public virtual bool CanDismiss => true;

    protected void Close(object? result) => _completion.TrySetResult(result);

    [RelayCommand]
    public void Dismiss()
    {
        if (CanDismiss)
        {
            Close(null);
        }
    }
}

public sealed record ConfirmResult(bool Confirmed, bool OptionChecked);

public sealed partial class ConfirmDialogViewModel : DialogViewModel
{
    public required string Title { get; init; }

    public required string Message { get; init; }

    public string ConfirmText { get; init; } = Loc.Get("Common.Confirm");

    public string CancelText { get; init; } = Loc.Get("Common.Cancel");

    public bool IsDanger { get; init; }

    public string? OptionText { get; init; }

    public bool HasOption => OptionText is not null;

    [ObservableProperty]
    private bool _optionChecked;

    public override double DialogWidth => 460;

    [RelayCommand]
    private void Confirm() => Close(new ConfirmResult(true, OptionChecked));

    [RelayCommand]
    private void Cancel() => Close(new ConfirmResult(false, OptionChecked));
}

public sealed class InfoDialogViewModel : DialogViewModel
{
    public required string Title { get; init; }

    public required IReadOnlyList<(string Heading, string Body)> Sections { get; init; }

    public IEnumerable<InfoSection> Items => Sections.Select(s => new InfoSection(s.Heading, s.Body));

    public override double DialogWidth => 600;

    public RelayCommand CloseCommand => new(() => Close(null));

    public RelayCommand OpenRepositoryCommand => new(() => Shell.Open(AppInfo.RepositoryUrl));
}

public sealed record InfoSection(string Heading, string Body);

/// <summary>Shows in-window dialogs and the standard Windows file pickers.</summary>
public sealed class DialogService
{
    public ObservableCollection<DialogViewModel> OpenDialogs { get; } = [];

    public async Task<T?> ShowAsync<T>(DialogViewModel dialog)
    {
        OpenDialogs.Add(dialog);
        try
        {
            var result = await dialog.Completion;
            return result is T typed ? typed : default;
        }
        finally
        {
            OpenDialogs.Remove(dialog);
        }
    }

    public Task ShowAsync(DialogViewModel dialog) => ShowAsync<object>(dialog);

    public async Task<ConfirmResult> ConfirmAsync(string title, string message, string? confirmText = null, bool danger = false, string? optionText = null, bool optionDefault = false)
    {
        var dialog = new ConfirmDialogViewModel
        {
            Title = title,
            Message = message,
            ConfirmText = confirmText ?? Loc.Get("Common.Confirm"),
            IsDanger = danger,
            OptionText = optionText,
            OptionChecked = optionDefault,
        };
        return await ShowAsync<ConfirmResult>(dialog) ?? new ConfirmResult(false, false);
    }

    /// <summary>Closes the top-most dismissable dialog (Esc).</summary>
    public bool DismissTop()
    {
        if (OpenDialogs.Count == 0)
        {
            return false;
        }

        OpenDialogs[^1].Dismiss();
        return true;
    }

    public string? PickExecutable(string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Get("Picker.Exe.Title"),
            Filter = Loc.Get("Picker.Exe.Filter") + " (*.exe;*.lnk;*.url;*.bat;*.cmd)|*.exe;*.lnk;*.url;*.bat;*.cmd|" + Loc.Get("Picker.AllFiles") + " (*.*)|*.*",
            CheckFileExists = true,
            DereferenceLinks = false,
        };
        if (initialDirectory is not null && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickImage(string? title = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title ?? Loc.Get("Picker.Image.Title"),
            Filter = Loc.Get("Picker.Images") + " (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickSaveFile(string title, string fileName, string filter)
    {
        var dialog = new SaveFileDialog { Title = title, FileName = fileName, Filter = filter, AddExtension = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
