using Arclight.Platforms.Windows;
using Microsoft.Win32;

namespace Arclight.Platforms.Tests;

/// <summary>In-memory registry used to test scanners without touching the real one.</summary>
internal sealed class FakeRegistry : IRegistryReader
{
    private readonly Dictionary<string, Dictionary<string, string>> _keys = new(StringComparer.OrdinalIgnoreCase);

    public FakeRegistry Set(RegistryHive hive, string key, string name, string value)
    {
        var path = $@"{hive}\{key}";
        if (!_keys.TryGetValue(path, out var values))
        {
            _keys[path] = values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        values[name] = value;

        // Make every parent key exist too.
        var parent = path;
        while (parent.LastIndexOf('\\') is var index and > 0)
        {
            parent = parent[..index];
            _keys.TryAdd(parent, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        return this;
    }

    public string? GetString(RegistryHive hive, string keyPath, string valueName) =>
        _keys.TryGetValue($@"{hive}\{keyPath}", out var values) && values.TryGetValue(valueName, out var value) ? value : null;

    public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string keyPath)
    {
        var prefix = $@"{hive}\{keyPath}\";
        return _keys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[prefix.Length..].Split('\\')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool KeyExists(RegistryHive hive, string keyPath) => _keys.ContainsKey($@"{hive}\{keyPath}");

    public bool ValueExists(RegistryHive hive, string keyPath, string valueName) => GetString(hive, keyPath, valueName) is not null;
}

internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arclight-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string relative, string content = "")
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        return full;
    }

    public string Binary(string relative, int size)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllBytes(full, new byte[size]);
        return full;
    }

    public string Dir(string relative)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(full);
        return full;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
