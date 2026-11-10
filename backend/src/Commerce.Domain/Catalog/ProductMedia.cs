namespace Commerce.Domain.Catalog;

public sealed class ProductMedia
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public Guid? VariantId { get; set; }

    public ProductVariant? Variant { get; set; }

    public string ObjectKey { get; set; } = string.Empty;

    public string AltText { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsPrimary { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
