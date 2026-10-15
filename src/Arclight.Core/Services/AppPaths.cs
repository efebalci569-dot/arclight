namespace Arclight.Core.Services;

/// <summary>
/// Where Arclight keeps its data. By default %LOCALAPPDATA%\Arclight; a file named
/// <c>portable.txt</c> next to the executable switches to a <c>Data</c> folder beside it.
/// </summary>
public sealed class AppPaths
{
    public const string PortableMarkerFile = "portable.txt";

    public AppPaths(string root, bool isPortable = false)
    {
        Root = Path.GetFullPath(root);
        IsPortable = isPortable;
    }

    public string Root { get; }

    public bool IsPortable { get; }

    public string DatabaseFile => Path.Combine(Root, "arclight.db");

    /// <summary>Artwork Arclight downloaded or copied from launchers. Safe to delete.</summary>
    public string ArtworkCache => Path.Combine(Root, "Cache", "Artwork");

    /// <summary>Images the user picked. Never removed by "clear cache".</summary>
    public string UserImages => Path.Combine(Root, "Images");

    public string Logs => Path.Combine(Root, "Logs");

    public string Backups => Path.Combine(Root, "Backups");

    public static AppPaths Resolve(string? overrideRoot, string executableDirectory)
    {
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return new AppPaths(overrideRoot);
        }

        if (File.Exists(Path.Combine(executableDirectory, PortableMarkerFile)))
        {
            return new AppPaths(Path.Combine(executableDirectory, "Data"), isPortable: true);
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPaths(Path.Combine(local, "Arclight"));
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ArtworkCache);
        Directory.CreateDirectory(UserImages);
        Directory.CreateDirectory(Logs);
    }
}
