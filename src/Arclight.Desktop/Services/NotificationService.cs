using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.Services;

public enum NoticeKind { Info, Success, Warning, Error }

/// <summary>A transient message in the bottom-right corner, optionally with one action.</summary>
public sealed partial class Toast : ObservableObject
{
    private readonly Action<Toast> _dismiss;

    public Toast(NoticeKind kind, string title, string? message, string? actionText, Func<Task>? action, Action<Toast> dismiss)
    {
        Kind = kind;
        Title = title;
        Message = message;
        ActionText = actionText;
        _dismiss = dismiss;
        ActionCommand = new AsyncRelayCommand(async () =>
        {
            _dismiss(this);
            if (action is not null)
            {
                await action();
            }
        });
        DismissCommand = new RelayCommand(() => _dismiss(this));
    }

    public NoticeKind Kind { get; }

    public string Title { get; }

    public string? Message { get; }

    public string? ActionText { get; }

    public bool HasAction => ActionText is not null;

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public ICommand ActionCommand { get; }

    public ICommand DismissCommand { get; }

    public string Glyph => Kind switch
    {
        NoticeKind.Success => "",
        NoticeKind.Warning => "",
        NoticeKind.Error => "",
        _ => "",
    };

    public string BrushKey => Kind switch
    {
        NoticeKind.Success => "Brush.Success",
        NoticeKind.Warning => "Brush.Warning",
        NoticeKind.Error => "Brush.Danger",
        _ => "Brush.Info",
    };
}

public sealed record NotificationEntry(NoticeKind Kind, string Title, string? Message, DateTimeOffset At)
{
    public string TimeText => Core.Formatting.Format.Relative(At, DateTimeOffset.Now);

    public string Glyph => Kind switch
    {
        NoticeKind.Success => "",
        NoticeKind.Warning => "",
        NoticeKind.Error => "",
        _ => "",
    };
}

/// <summary>Toasts plus a short in-memory history shown in the notification center.</summary>
public sealed partial class NotificationService : ObservableObject
{
    private const int MaxVisible = 4;
    private const int MaxHistory = 50;

    [ObservableProperty]
    private int _unreadCount;

    public ObservableCollection<Toast> Toasts { get; } = [];

    public ObservableCollection<NotificationEntry> History { get; } = [];

    public void Info(string title, string? message = null, string? actionText = null, Func<Task>? action = null) =>
        Show(NoticeKind.Info, title, message, actionText, action);

    public void Success(string title, string? message = null, string? actionText = null, Func<Task>? action = null) =>
        Show(NoticeKind.Success, title, message, actionText, action);

    public void Warning(string title, string? message = null, string? actionText = null, Func<Task>? action = null) =>
        Show(NoticeKind.Warning, title, message, actionText, action);

    public void Error(string title, string? message = null, string? actionText = null, Func<Task>? action = null) =>
        Show(NoticeKind.Error, title, message, actionText, action);

    public void Show(NoticeKind kind, string title, string? message = null, string? actionText = null, Func<Task>? action = null, bool keepInHistory = true)
    {
        void Add()
        {
            var toast = new Toast(kind, title, message, actionText, action, Dismiss);
            Toasts.Add(toast);
            while (Toasts.Count > MaxVisible)
            {
                Toasts.RemoveAt(0);
            }

            if (keepInHistory)
            {
                History.Insert(0, new NotificationEntry(kind, title, message, DateTimeOffset.Now));
                while (History.Count > MaxHistory)
                {
                    History.RemoveAt(History.Count - 1);
                }

                UnreadCount++;
            }

            // Errors and actionable toasts stay longer so they can be read and acted on.
            var seconds = kind == NoticeKind.Error || actionText is not null ? 10 : 5;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Dismiss(toast);
            };
            timer.Start();
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Add();
        }
        else
        {
            dispatcher.BeginInvoke(Add);
        }
    }

    public void MarkAllRead() => UnreadCount = 0;

    public void ClearHistory()
    {
        History.Clear();
        UnreadCount = 0;
    }

    private void Dismiss(Toast toast) => Toasts.Remove(toast);
}
