namespace Commerce.Domain.Identity;

public static class EmailNormalizer
{
    public const int MaxLength = 320;

    /// <summary>Returns the trimmed address and its uppercase lookup key, or null when the address is unusable.</summary>
    public static (string Email, string Normalized)? Canonicalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > MaxLength || !IsPlausible(trimmed))
        {
            return null;
        }

        return (trimmed, trimmed.ToUpperInvariant());
    }

    private static bool IsPlausible(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
        {
            return false;
        }

        var domain = email[(at + 1)..];
        return domain.Contains('.', StringComparison.Ordinal)
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !email.Any(char.IsWhiteSpace);
    }
}
