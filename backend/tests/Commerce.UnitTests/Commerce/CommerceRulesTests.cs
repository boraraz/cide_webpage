using Commerce.Application.Commerce;
using Commerce.Domain.Commerce;

namespace Commerce.UnitTests.Orders;

public sealed class CommerceRulesTests
{
    [Fact]
    public void Shipping_is_149_until_merchandise_reaches_2500()
    {
        Assert.Equal(149m, CommerceMath.Shipping(2499.99m, 149m, 2500m));
        Assert.Equal(0m, CommerceMath.Shipping(2500m, 149m, 2500m));
        Assert.Equal(10.01m, CommerceMath.LineTotal(10.005m, 1));
    }

    [Fact]
    public void Fingerprint_ignores_server_totals_and_changes_when_intent_changes()
    {
        var line = new LineIntent(Guid.Parse("11111111-1111-7111-8111-111111111111"), 2);
        var address = new PostalAddress("Ada", "Istiklal 1", null, "Beyoglu", "Istanbul", "34430", "TR", null);
        var first = CheckoutFingerprint.Compute([line], "ada@example.com", "+905550000001", address, address);
        var same = CheckoutFingerprint.Compute([new LineIntent(line.VariantId, 2)], "Ada@Example.com", "+905550000001", address, address);
        var different = CheckoutFingerprint.Compute([line], "ada@example.com", "+905550000002", address, address);
        Assert.Equal(first, same);
        Assert.NotEqual(first, different);
        Assert.Equal(64, first.Length);
    }
}
