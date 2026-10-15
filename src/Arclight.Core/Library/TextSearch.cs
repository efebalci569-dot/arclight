using System.Globalization;
using System.Text;

namespace Arclight.Core.Library;

/// <summary>Case and accent insensitive text matching used by the library search.</summary>
public static class TextSearch
{
    /// <summary>Lower-cases, strips diacritics and folds Turkish dotless i so "İstanbul", "istanbul" and "ıstanbul" match.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(ch);
            builder.Append(lower == 'ı' ? 'i' : lower);
        }

        return builder.ToString();
    }

    /// <summary>Splits a query into normalized, non-empty terms.</summary>
    public static IReadOnlyList<string> Tokenize(string? query) =>
        Normalize(query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>Compares strings so that "Game 2" sorts before "Game 10".</summary>
public sealed class NaturalStringComparer : IComparer<string?>
{
    public static NaturalStringComparer Instance { get; } = new();

    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    int IComparer<string?>.Compare(string? x, string? y) => CompareStrings(x, y);

    public static int CompareStrings(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                var startX = i;
                var startY = j;
                while (i < x.Length && char.IsAsciiDigit(x[i])) i++;
                while (j < y.Length && char.IsAsciiDigit(y[j])) j++;

                var numberX = x.AsSpan(startX, i - startX).TrimStart('0');
                var numberY = y.AsSpan(startY, j - startY).TrimStart('0');
                if (numberX.Length != numberY.Length)
                {
                    return numberX.Length.CompareTo(numberY.Length);
                }

                var digits = numberX.SequenceCompareTo(numberY);
                if (digits != 0)
                {
                    return Math.Sign(digits);
                }

                continue;
            }

            var runStartX = i;
            var runStartY = j;
            while (i < x.Length && !char.IsAsciiDigit(x[i])) i++;
            while (j < y.Length && !char.IsAsciiDigit(y[j])) j++;

            var text = Compare.Compare(x, runStartX, i - runStartX, y, runStartY, j - runStartY, Options);
            if (text != 0)
            {
                return text;
            }
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }
}
