using System.Reflection;

namespace Arclight.Desktop.Services;

public static class AppInfo
{
    public const string Name = "Arclight";
    public const string Tagline = "Your Games. One Universe.";
    public const string RepositoryOwner = "efebalci569-dot";
    public const string RepositoryName = "arclight";
    public const string License = "MIT License";

    public static string RepositoryUrl => $"https://github.com/{RepositoryOwner}/{RepositoryName}";

    public static string IssuesUrl => RepositoryUrl + "/issues";

    public static Version Version { get; } = typeof(AppInfo).Assembly.GetName().Version ?? new Version(0, 1, 0);

    public static string DisplayVersion { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Version.ToString(3);

    public static string ExecutablePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Arclight.exe");
}
