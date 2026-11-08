using System.Globalization;

namespace Commerce.Domain.Catalog;

/// <summary>
/// Catalog prices are tax-inclusive Turkish lira amounts with two decimal places.
/// </summary>
public static class Money
{
    public const string Currency = "TRY";

    public const decimal Maximum = 9_999_999_999.99m;

    public static bool TryParse(string? text, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        if (parsed < 0m || parsed > Maximum || parsed != decimal.Round(parsed, 2, MidpointRounding.AwayFromZero))
        {
            return false;
        }

        amount = parsed;
        return true;
    }

    public static string Format(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static bool CompareAtIsValid(decimal price, decimal? compareAtPrice) =>
        compareAtPrice is null || compareAtPrice > price;
}
