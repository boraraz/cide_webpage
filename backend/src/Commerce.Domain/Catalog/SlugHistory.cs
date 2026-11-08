namespace Commerce.Domain.Catalog;

public sealed class SlugHistory
{
    public Guid Id { get; set; }

    public SlugOwner OwnerType { get; set; }

    public Guid OwnerId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
