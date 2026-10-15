using Microsoft.Win32;

namespace Arclight.Platforms.Windows;

/// <summary>Read-only registry access, abstracted so scanners can be tested without touching the real registry.</summary>
public interface IRegistryReader
{
    string? GetString(RegistryHive hive, string keyPath, string valueName);

    IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string keyPath);

    bool KeyExists(RegistryHive hive, string keyPath);

    bool ValueExists(RegistryHive hive, string keyPath, string valueName);
}

public sealed class WindowsRegistryReader : IRegistryReader
{
    public static WindowsRegistryReader Instance { get; } = new();

    public string? GetString(RegistryHive hive, string keyPath, string valueName)
    {
        using var key = Open(hive, keyPath);
        return key?.GetValue(valueName) switch
        {
            string s => s,
            int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string keyPath)
    {
        using var key = Open(hive, keyPath);
        return key?.GetSubKeyNames() ?? [];
    }

    public bool KeyExists(RegistryHive hive, string keyPath)
    {
        using var key = Open(hive, keyPath);
        return key is not null;
    }

    public bool ValueExists(RegistryHive hive, string keyPath, string valueName)
    {
        using var key = Open(hive, keyPath);
        return key?.GetValueNames().Contains(valueName, StringComparer.OrdinalIgnoreCase) == true;
    }

    private static RegistryKey? Open(RegistryHive hive, string keyPath)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            return root.OpenSubKey(keyPath, writable: false);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
