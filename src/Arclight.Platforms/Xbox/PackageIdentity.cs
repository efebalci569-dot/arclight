using System.Security.Cryptography;
using System.Text;

namespace Arclight.Platforms.Xbox;

/// <summary>Computes MSIX package family names from a package name and publisher.</summary>
public static class PackageIdentity
{
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    /// <summary>
    /// The publisher id is the first 8 bytes of SHA-256 over the UTF-16LE publisher string,
    /// written as 13 characters of Crockford-style base32 (65 bits, the last one zero).
    /// </summary>
    public static string PublisherId(string publisher)
    {
        var hash = SHA256.HashData(Encoding.Unicode.GetBytes(publisher));
        ulong value = 0;
        for (var i = 0; i < 8; i++)
        {
            value = (value << 8) | hash[i];
        }

        // Treat the hash as a 65-bit number (value followed by one zero bit) and read it 5 bits at a time.
        var builder = new StringBuilder(13);
        for (var i = 0; i < 13; i++)
        {
            var shift = 60 - (i * 5);
            var index = shift >= 1
                ? (int)((value >> (shift - 1)) & 0x1F)
                : (int)((value << 1) & 0x1F);
            builder.Append(Alphabet[index]);
        }

        return builder.ToString();
    }

    public static string FamilyName(string packageName, string publisher) => $"{packageName}_{PublisherId(publisher)}";
}
