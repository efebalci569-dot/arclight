namespace Arclight.Core.Interfaces;

/// <summary>Saves the largest available icon of an executable or .ico file as a PNG.</summary>
public interface IIconExtractor
{
    bool TryExtract(string sourcePath, string targetPngPath);
}
