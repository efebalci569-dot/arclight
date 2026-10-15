using System.Windows;
using Forms = System.Windows.Forms;

namespace Arclight.Desktop.Services;

/// <summary>
/// System tray icon, used when "Close to tray" is on so the optional background tracker can keep
/// running without a window.
/// </summary>
public sealed class TrayService : IDisposable
{
    private Forms.NotifyIcon? _icon;
    private Forms.ToolStripMenuItem? _statusItem;
    private Forms.ToolStripItem? _openItem;
    private Forms.ToolStripItem? _exitItem;

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public bool IsVisible => _icon?.Visible == true;

    public void Show()
    {
        if (_icon is null)
        {
            var menu = new Forms.ContextMenuStrip();
            _statusItem = new Forms.ToolStripMenuItem("Arclight") { Enabled = false };
            menu.Items.Add(_statusItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            _openItem = menu.Items.Add(string.Empty, null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
            _exitItem = menu.Items.Add(string.Empty, null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

            var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/Brand/arclight.ico"))?.Stream;
            _icon = new Forms.NotifyIcon
            {
                Text = "Arclight",
                Icon = iconStream is null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(iconStream),
                ContextMenuStrip = menu,
            };
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left)
                {
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                }
            };
        }

        // Menu text follows the UI language (Show runs again whenever settings change).
        _openItem!.Text = Loc.Get("Tray.Open");
        _exitItem!.Text = Loc.Get("Tray.Exit");
        _icon.Visible = true;
    }

    public void Hide()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
        }
    }

    public void SetStatus(string text)
    {
        if (_statusItem is not null)
        {
            _statusItem.Text = text;
        }

        if (_icon is not null)
        {
            _icon.Text = text.Length > 60 ? text[..60] : text;
        }
    }

    public void Dispose()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
