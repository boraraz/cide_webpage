using System.Security.Cryptography;
using System.Text;
using Commerce.Domain.Catalog;

namespace Commerce.Application.Catalog;

public static class CatalogSupport
{
    public const string StaleProductMessage = "This product was saved in another session. Reload to see the latest version.";

    public static async Task<CatalogResult<T>> SaveProductAsync<T>(
        ICatalogStore store,
        Product product,
        int? expectedVersion,
        DateTimeOffset now,
        Func<T> value,
        CancellationToken cancellationToken)
    {
        try
        {
            var saved = await store.SaveProductGraphAsync(product, expectedVersion, now, cancellationToken);
            return saved.Succeeded
                ? CatalogResult<T>.Ok(value())
                : CatalogResult<T>.Fail(saved.Failure!);
        }
        catch (CatalogConflictException conflict)
        {
            store.Abandon();
            return CatalogResult<T>.Conflict(conflict.Field, conflict.Message, conflict.Status);
        }
    }

    public static async Task<CatalogResult<T>> SaveAsync<T>(
        ICatalogStore store,
        Func<T> value,
        CancellationToken cancellationToken)
    {
        try
        {
            await store.SaveChangesAsync(cancellationToken);
            return CatalogResult<T>.Ok(value());
        }
        catch (CatalogConflictException conflict)
        {
            store.Abandon();
            return CatalogResult<T>.Conflict(conflict.Field, conflict.Message, conflict.Status);
        }
    }

    public static Dictionary<string, string[]> Text(string field, string? value, int maxLength, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return required
                ? new Dictionary<string, string[]> { [field] = ["This field is required."] }
                : [];
        }

        if (value.Trim().Length > maxLength)
        {
            return new Dictionary<string, string[]> { [field] = [$"Use at most {maxLength} characters."] };
        }

        return [];
    }

    public static string? Optional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed : trimmed;
    }

    public static async Task<string?> ResolveSlugAsync(
        ICatalogStore store,
        SlugOwner owner,
        string? requested,
        string fallback,
        Guid? exceptOwnerId,
        CancellationToken cancellationToken)
    {
        var slug = Slug.Normalize(string.IsNullOrWhiteSpace(requested) ? fallback : requested);
        if (slug is null)
        {
            return null;
        }

        if (await store.SlugTakenAsync(owner, slug, exceptOwnerId, cancellationToken))
        {
            return string.Empty;
        }

        return slug;
    }

    public static string HashAdjustment(InventoryMovementType type, int quantity, string? note)
    {
        var payload = $"{type}|{quantity}|{note?.Trim() ?? string.Empty}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static bool IdempotencyKeyIsValid(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && key.Trim().Length is >= 16 and <= 80
        && key.Trim().All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_');
}
