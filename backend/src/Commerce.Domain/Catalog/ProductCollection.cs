namespace Commerce.Domain.Catalog;

public sealed class ProductCollection
{
    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public Guid CollectionId { get; set; }

    public Collection Collection { get; set; } = null!;
}
