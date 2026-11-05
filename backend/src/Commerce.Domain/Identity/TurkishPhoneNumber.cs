namespace Commerce.Domain.Identity;

/// <summary>Normalizes optional Türkiye numbers to E.164. Verification by SMS is not required.</summary>
public static class TurkishPhoneNumber
{
    public static bool TryNormalize(string? input, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var compact = new string(input.Where(character => char.IsDigit(character) || character == '+').ToArray());
        var national = compact switch
        {
            ['+', '9', '0', ..] => compact[3..],
            ['9', '0', ..] when compact.Length == 12 => compact[2..],
            ['0', ..] when compact.Length == 11 => compact[1..],
            _ when compact.Length == 10 && compact.All(char.IsDigit) => compact,
            _ => null,
        };

        if (national is null || national.Length != 10 || !national.All(char.IsDigit))
        {
            error = "Enter a Türkiye phone number, for example +90 532 000 00 00.";
            return false;
        }

        normalized = "+90" + national;
        return true;
    }
}
