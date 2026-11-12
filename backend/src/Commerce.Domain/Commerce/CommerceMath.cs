namespace Commerce.Domain.Commerce;

public static class CommerceMath
{
    public static decimal LineTotal(decimal unitPrice, int quantity) =>
        decimal.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);

    public static decimal Shipping(decimal merchandise, decimal fee, decimal freeThreshold) =>
        merchandise >= freeThreshold ? 0m : fee;
}
