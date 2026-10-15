using System.Text.RegularExpressions;
using Arclight.Core.Models;

namespace Arclight.Core.Launching;

/// <summary>
/// Validates launch configuration before anything is executed. Arclight never passes
/// user input to a command shell: executables are started directly and URIs are only
/// opened when their scheme belongs to a known game launcher.
/// </summary>
public static partial class LaunchValidator
{
    /// <summary>URI schemes registered by game launchers that Arclight is willing to open.</summary>
    public static IReadOnlySet<string> AllowedUriSchemes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "steam",
        "com.epicgames.launcher",
        "goggalaxy",
        "uplay",
        "origin",
        "origin2",
        "link2ea",
        "battlenet",
        "itchio",
        "amazon-games",
        "heroic",
    };

    public static IReadOnlySet<string> AllowedExecutableExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".lnk", ".url", ".bat", ".cmd",
    };

    public const string AppsFolderPrefix = @"shell:AppsFolder\";

    public static ValidationResult ValidateExecutablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Invalid("ExeRequired", "Choose the game's executable file.");
        }

        if (ContainsControlCharacters(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return ValidationResult.Invalid("ExeInvalidChars", "The executable path contains invalid characters.");
        }

        if (!Path.IsPathFullyQualified(path))
        {
            return ValidationResult.Invalid("ExeNotFullPath", "Use a full path, for example C:\\Games\\MyGame\\game.exe.");
        }

        var extension = Path.GetExtension(path);
        if (!AllowedExecutableExtensions.Contains(extension))
        {
            return ValidationResult.Invalid("ExeWrongType", "Only .exe, .lnk, .url, .bat and .cmd files can be launched.");
        }

        return ValidationResult.Ok;
    }

    public static ValidationResult ValidateArguments(string? arguments)
    {
        if (string.IsNullOrEmpty(arguments))
        {
            return ValidationResult.Ok;
        }

        if (arguments.Length > 2048)
        {
            return ValidationResult.Invalid("ArgsTooLong", "Launch arguments are too long.");
        }

        return ContainsControlCharacters(arguments)
            ? ValidationResult.Invalid("ArgsControlChars", "Launch arguments cannot contain line breaks or control characters.")
            : ValidationResult.Ok;
    }

    public static ValidationResult ValidateLauncherUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return ValidationResult.Invalid("UriRequired", "Enter the launcher link, for example steam://rungameid/730.");
        }

        if (uri.Length > 2048 || ContainsControlCharacters(uri) || uri.Any(char.IsWhiteSpace))
        {
            return ValidationResult.Invalid("UriInvalidChars", "The launcher link contains spaces or invalid characters.");
        }

        if (uri.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AppsFolderPattern().IsMatch(uri)
                ? ValidationResult.Ok
                : ValidationResult.Invalid("AppIdFormat", "The Windows app id is not in the expected format.");
        }

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return ValidationResult.Invalid("UriNotValid", "The launcher link is not a valid URI.");
        }

        if (!AllowedUriSchemes.Contains(parsed.Scheme))
        {
            var supported = string.Join(", ", AllowedUriSchemes.Order().Select(s => s + "://"));
            return ValidationResult.Invalid(
                "SchemeNotAllowed",
                $"'{parsed.Scheme}://' links are not allowed. Supported: {supported}.",
                parsed.Scheme,
                supported);
        }

        return ValidationResult.Ok;
    }

    /// <summary>Checks that a game has everything needed for its launch method.</summary>
    public static ValidationResult ValidateGame(Game game)
    {
        if (string.IsNullOrWhiteSpace(game.Name))
        {
            return ValidationResult.Invalid("NameRequired", "The game needs a name.");
        }

        return game.LaunchMethod switch
        {
            LaunchMethod.None => ValidationResult.Ok,
            LaunchMethod.Executable => Combine(ValidateExecutablePath(game.ExecutablePath), ValidateArguments(game.LaunchArguments)),
            LaunchMethod.CustomCommand => Combine(ValidateExecutablePath(game.LaunchTarget), ValidateArguments(game.LaunchArguments)),
            LaunchMethod.LauncherUri => ValidateLauncherUri(game.LaunchTarget),
            _ => ValidationResult.Invalid("UnknownMethod", "Unknown launch method."),
        };
    }

    public static string? GetUriScheme(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        if (uri.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "shell";
        }

        var index = uri.IndexOf(':', StringComparison.Ordinal);
        return index > 0 ? uri[..index] : null;
    }

    private static ValidationResult Combine(ValidationResult first, ValidationResult second) => first.IsValid ? second : first;

    private static bool ContainsControlCharacters(string value) => value.Any(char.IsControl);

    [GeneratedRegex(@"^shell:AppsFolder\\[A-Za-z0-9._-]+_[a-z0-9]{13}![A-Za-z0-9._-]+$", RegexOptions.IgnoreCase)]
    private static partial Regex AppsFolderPattern();
}

/// <summary>
/// Result of a validation. <see cref="Code"/> and <see cref="Args"/> let a UI show the message in
/// its own language; <see cref="Error"/> is the English text used in logs and as a fallback.
/// </summary>
public readonly record struct ValidationResult(bool IsValid, string? Error, string? Code = null, object[]? Args = null)
{
    public static ValidationResult Ok => new(true, null);

    public static ValidationResult Invalid(string code, string error, params object[] args) => new(false, error, code, args);
}
