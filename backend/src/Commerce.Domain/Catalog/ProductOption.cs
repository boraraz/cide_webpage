namespace Commerce.Domain.Catalog;

public sealed class ProductOption
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public int Position { get; set; }

    public ICollection<ProductOptionValue> Values { get; set; } = [];
}
