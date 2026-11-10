namespace Commerce.Domain.Content;

public sealed class ContentRevision
{
    public Guid Id { get; set; }

    public Guid PageId { get; set; }

    public ContentPage Page { get; set; } = null!;

    public int Version { get; set; }

    public string Configuration { get; set; } = "{}";

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<ContentRevisionMedia> Media { get; set; } = [];
}
