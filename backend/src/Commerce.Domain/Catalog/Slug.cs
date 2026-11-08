using System.Text;

namespace Commerce.Domain.Catalog;

/// <summary>
/// URL slugs are lowercase ASCII. Turkish letters are folded so names such as "Şık Üst" become "sik-ust".
/// </summary>
public static class Slug
{
    public const int MaxLength = 120;

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var folded = Fold(value.Trim());
        var builder = new StringBuilder(folded.Length);
        var pendingHyphen = false;
        foreach (var character in folded)
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                pendingHyphen = false;
                builder.Append(character);
            }
            else
            {
                pendingHyphen = true;
            }
        }

        if (builder.Length is < 1 or > MaxLength)
        {
            return null;
        }

        return builder.ToString();
    }

    private static string Fold(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                'ı' or 'I' or 'İ' or 'i' => 'i',
                'ş' or 'Ş' => 's',
                'ğ' or 'Ğ' => 'g',
                'ü' or 'Ü' => 'u',
                'ö' or 'Ö' => 'o',
                'ç' or 'Ç' => 'c',
                _ => char.ToLowerInvariant(character),
            });
        }

        return builder.ToString();
    }
}
