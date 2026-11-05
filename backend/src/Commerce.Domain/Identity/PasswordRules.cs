namespace Commerce.Domain.Identity;

public static class PasswordRules
{
    public const int MinimumLength = 10;
    public const int MaximumLength = 128;

    public static Dictionary<string, string[]> Validate(string? password, string? confirmation, string? email)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
        {
            Add(errors, "password", $"Use at least {MinimumLength} characters.");
        }
        else if (password.Length > MaximumLength)
        {
            Add(errors, "password", $"Use at most {MaximumLength} characters.");
        }
        else
        {
            if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            {
                Add(errors, "password", "Include at least one letter and one number.");
            }

            if (email is not null && string.Equals(password, email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Add(errors, "password", "The password must not match the email address.");
            }
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            Add(errors, "passwordConfirmation", "The passwords do not match.");
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = [];
            errors[key] = list;
        }

        list.Add(message);
    }
}
