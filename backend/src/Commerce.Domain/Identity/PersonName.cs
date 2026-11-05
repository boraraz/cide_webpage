namespace Commerce.Domain.Identity;

public static class PersonName
{
    public const int MaxLength = 100;

    public static string? Validate(string? value, string field, string label, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxLength)
        {
            errors[field] = [$"Enter a {label} of 1 to {MaxLength} characters."];
            return null;
        }

        if (trimmed.Any(char.IsControl))
        {
            errors[field] = ["This name contains characters that cannot be used."];
            return null;
        }

        return trimmed;
    }
}
