namespace Commerce.Domain.Catalog;

public sealed class Product
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ShortDescription { get; set; } = string.Empty;

    public ProductStatus Status { get; set; } = ProductStatus.Draft;

    public string? Brand { get; set; }

    public bool IsFeatured { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ProductCategory> Categories { get; set; } = [];

    public ICollection<ProductCollection> Collections { get; set; } = [];

    public ICollection<ProductOption> Options { get; set; } = [];

    public ICollection<ProductVariant> Variants { get; set; } = [];

    public ICollection<ProductMedia> Media { get; set; } = [];
}
