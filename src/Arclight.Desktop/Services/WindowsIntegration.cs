using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Arclight.Core.Interfaces;
using Microsoft.Win32;

namespace Arclight.Desktop.Services;

/// <summary>Ensures one Arclight per data folder so two trackers never count the same session.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate, Action onActivate)
    {
        _mutex = mutex;
        _activate = activate;
        _registration = ThreadPool.RegisterWaitForSingleObject(activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Returns null (after asking the running instance to show itself) when Arclight is already open.</summary>
    public static SingleInstance? TryAcquire(string scope, Action onActivate)
    {
        var id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scope.ToUpperInvariant())))[..16];
        var mutex = new Mutex(initiallyOwned: true, $@"Local\Arclight.Instance.{id}", out var created);
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\Arclight.Activate.{id}");
        if (!created)
        {
            activate.Set();
            activate.Dispose();
            mutex.Dispose();
            return null;
        }

        return new SingleInstance(mutex, activate, onActivate);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activate.Dispose();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex.Dispose();
    }
}

/// <summary>"Launch Arclight when Windows starts" via the per-user Run key (no admin rights needed).</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Arclight";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{AppInfo.ExecutablePath}\" --minimized");
        }
        else if (key.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

/// <summary>Extracts the largest icon of an executable or .ico file into a PNG (alpha preserved).</summary>
public sealed class ShellIconExtractor : IIconExtractor
{
    public bool TryExtract(string sourcePath, string targetPngPath)
    {
        System.Drawing.Icon? icon = null;
        try
        {
            icon = sourcePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                ? new System.Drawing.Icon(sourcePath, 256, 256)
                : System.Drawing.Icon.ExtractIcon(sourcePath, 0, 256);
            if (icon is null)
            {
                return false;
            }

            using var bitmap = icon.ToBitmap();
            Directory.CreateDirectory(Path.GetDirectoryName(targetPngPath)!);
            bitmap.Save(targetPngPath, System.Drawing.Imaging.ImageFormat.Png);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or System.ComponentModel.Win32Exception or ExternalException)
        {
            return false;
        }
        finally
        {
            icon?.Dispose();
        }
    }
}

public static class Shell
{
    /// <summary>Opens a web link or folder with the default handler. Only http(s) links and existing folders are allowed.</summary>
    public static void Open(string target)
    {
        if (target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(target))
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
    }

    /// <summary>Opens Explorer with the file selected.</summary>
    public static void RevealFile(string path)
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false })?.Dispose();
        }
        else if (Directory.Exists(path))
        {
            Open(path);
        }
    }
}

/// <summary>Native window tweaks: dark title bar for system UI and Windows 11 rounded corners.</summary>
public static class NativeWindow
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    public static void ApplyDarkChrome(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var dark = 1;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        var corners = DwmwcpRound;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corners, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
