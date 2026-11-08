namespace Commerce.Domain.Catalog;

public static class Sku
{
    public const int MaxLength = 64;

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > MaxLength)
        {
            return null;
        }

        foreach (var character in normalized)
        {
            if (character is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_'))
            {
                return null;
            }
        }

        return normalized;
    }
}
