using Commerce.Application.Catalog;
using Commerce.Application.Identity;
using Commerce.Domain.Catalog;
using Commerce.Domain.Identity;

namespace Commerce.Application.Commerce;

public sealed class CommerceService(ICommerceStore store)
{
    public Task<CartModel> GetCartAsync(Guid? customerId, byte[]? guestHash, CancellationToken cancellationToken) =>
        store.GetCartAsync(customerId, guestHash, cancellationToken);

    public Task<CommerceResult<CartChange>> AddItemAsync(
        Guid? customerId, byte[]? guestHash, Guid variantId, int quantity, CancellationToken cancellationToken) =>
        quantity < 1
            ? Task.FromResult(CommerceResult<CartChange>.Invalid(Field("quantity", "Enter a quantity of at least 1.")))
            : store.AddItemAsync(customerId, guestHash, variantId, quantity, cancellationToken);

    public Task<CommerceResult<CartModel>> UpdateItemAsync(
        Guid? customerId, byte[]? guestHash, Guid itemId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity < 1)
        {
            return Task.FromResult(CommerceResult<CartModel>.Invalid(Field("quantity", "Enter a quantity of at least 1.")));
        }

        return store.UpdateItemAsync(customerId, guestHash, itemId, quantity, cancellationToken);
    }

    public Task<CommerceResult<CartModel>> RemoveItemAsync(
        Guid? customerId, byte[]? guestHash, Guid itemId, CancellationToken cancellationToken) =>
        store.RemoveItemAsync(customerId, guestHash, itemId, cancellationToken);

    public Task MergeAsync(string? rawCartToken, Guid customerId, CancellationToken cancellationToken)
    {
        var hash = string.IsNullOrWhiteSpace(rawCartToken) ? null : SecureTokens.Hash(rawCartToken);
        return store.MergeAsync(hash, customerId, cancellationToken);
    }

    public Task<CommerceResult<QuoteModel>> QuoteAsync(Guid? customerId, byte[]? guestHash, CancellationToken cancellationToken) =>
        store.QuoteAsync(customerId, guestHash, cancellationToken);

    public Task<CommerceResult<PlacedOrderModel>> SubmitAsync(
        Guid? customerId,
        byte[]? guestHash,
        string? idempotencyKey,
        string email,
        string phone,
        IReadOnlyList<LineIntent> lines,
        PostalAddress shipping,
        bool billingSameAsShipping,
        PostalAddress? billing,
        string? expectedGrandTotal,
        string? confirmationCookie,
        CancellationToken cancellationToken)
    {
        if (!CatalogSupport.IdempotencyKeyIsValid(idempotencyKey))
        {
            return Task.FromResult(CommerceResult<PlacedOrderModel>.Invalid(
                Field("idempotencyKey", "Send an Idempotency-Key of 16 to 80 letters, numbers, hyphens or underscores.")));
        }

        var errors = new Dictionary<string, string[]>();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail.Length is < 3 or > 254 || !normalizedEmail.Contains('@', StringComparison.Ordinal))
        {
            errors["email"] = ["Enter a valid email."];
        }

        if (!TurkishPhoneNumber.TryNormalize(phone, out var normalizedPhone, out var phoneError) || normalizedPhone is null)
        {
            errors["phone"] = [phoneError ?? "Enter a Türkiye phone number."];
        }

        if (!TryAddress(shipping, "shipping", errors, out var shippingAddress))
        {
            shippingAddress = shipping;
        }

        PostalAddress billingAddress;
        if (billingSameAsShipping)
        {
            billingAddress = shippingAddress;
        }
        else if (!TryAddress(billing ?? shipping, "billing", errors, out billingAddress))
        {
            billingAddress = billing ?? shipping;
        }

        if (lines.Count == 0)
        {
            errors["lines"] = ["Add a product before checkout."];
        }
        else if (lines.Select(line => line.VariantId).Distinct().Count() != lines.Count)
        {
            errors["lines"] = ["Each product option can appear once."];
        }
        else if (lines.Any(line => line.Quantity < 1))
        {
            errors["lines"] = ["Each quantity must be at least 1."];
        }

        if (!Money.TryParse(expectedGrandTotal, out var expected))
        {
            errors["expectedGrandTotal"] = ["Review the total before placing the order."];
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(CommerceResult<PlacedOrderModel>.Invalid(errors));
        }

        var fingerprint = CheckoutFingerprint.Compute(lines, normalizedEmail, normalizedPhone!, shippingAddress, billingAddress);
        var command = new SubmitCommand(
            fingerprint,
            idempotencyKey!.Trim(),
            normalizedEmail,
            normalizedPhone!,
            lines,
            shippingAddress,
            billingAddress,
            expected,
            confirmationCookie);
        return store.SubmitAsync(customerId, guestHash, command, cancellationToken);
    }

    public Task<IReadOnlyList<OrderModel>> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        store.ListForCustomerAsync(customerId, cancellationToken);

    public Task<OrderModel?> FindForCustomerAsync(Guid customerId, string reference, CancellationToken cancellationToken) =>
        store.FindForCustomerAsync(customerId, reference, cancellationToken);

    public Task<OrderModel?> FindByAccessTokenAsync(string? rawToken, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(rawToken)
            ? Task.FromResult<OrderModel?>(null)
            : store.FindByAccessTokenAsync(rawToken, cancellationToken);

    public Task<ConfirmationPageModel?> FindByConfirmationAsync(string? rawCookie, CancellationToken cancellationToken) =>
        store.FindByConfirmationAsync(rawCookie, cancellationToken);

    public Task<CommerceResult<AdminOrderPageModel>> ListAdminAsync(
        string? status, string? reference, int page, int pageSize, CancellationToken cancellationToken) =>
        store.ListAdminAsync(status, reference, page, pageSize, cancellationToken);

    public Task<OrderModel?> FindAdminAsync(Guid orderId, CancellationToken cancellationToken) =>
        store.FindAdminAsync(orderId, cancellationToken);

    public Task<CommerceResult<OrderModel>> CancelAsync(Guid orderId, Guid actorId, string? reason, CancellationToken cancellationToken)
    {
        var trimmed = reason?.Trim();
        if (trimmed is { Length: > 500 })
        {
            return Task.FromResult(CommerceResult<OrderModel>.Invalid(Field("reason", "Use at most 500 characters.")));
        }

        return store.CancelAsync(orderId, actorId, string.IsNullOrEmpty(trimmed) ? null : trimmed, cancellationToken);
    }

    private static bool TryAddress(
        PostalAddress input,
        string prefix,
        Dictionary<string, string[]> errors,
        out PostalAddress address)
    {
        string? FieldError(string suffix, string? value, int max, bool required)
        {
            var text = value?.Trim() ?? string.Empty;
            if (required && text.Length == 0)
            {
                return "This field is required.";
            }

            return text.Length > max ? $"Use at most {max} characters." : null;
        }

        void Add(string suffix, string? error)
        {
            if (error is not null)
            {
                errors[$"{prefix}.{suffix}"] = [error];
            }
        }

        var country = (input.Country ?? string.Empty).Trim();
        var normalizedCountry = country.ToUpperInvariant() switch
        {
            "TR" or "TURKEY" or "TÜRKIYE" or "TURKIYE" or "TÜRKİYE" => "TR",
            _ => country,
        };
        Add("recipient", FieldError("recipient", input.Recipient, 120, true));
        Add("line1", FieldError("line1", input.Line1, 200, true));
        Add("line2", FieldError("line2", input.Line2, 200, false));
        Add("district", FieldError("district", input.District, 120, true));
        Add("city", FieldError("city", input.City, 120, true));
        var postal = input.PostalCode?.Trim() ?? string.Empty;
        Add("postalCode", postal.Length == 5 && postal.All(char.IsDigit) ? null : "Enter a 5-digit postal code.");
        Add("country", normalizedCountry == "TR" ? null : "Delivery is available in Türkiye only.");
        Add("notes", FieldError("notes", input.Notes, 500, false));
        address = new PostalAddress(
            input.Recipient.Trim(),
            input.Line1.Trim(),
            string.IsNullOrWhiteSpace(input.Line2) ? null : input.Line2.Trim(),
            input.District.Trim(),
            input.City.Trim(),
            postal,
            normalizedCountry,
            string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim());
        return !errors.Keys.Any(key => key.StartsWith(prefix + ".", StringComparison.Ordinal));
    }

    private static Dictionary<string, string[]> Field(string name, string message) =>
        new() { [name] = [message] };
}
