namespace Commerce.Domain.Content;

public sealed class ContentPage
{
    public const string HomepageKey = "homepage";

    public Guid Id { get; set; }

    public string Key { get; set; } = HomepageKey;

    public Guid? DraftRevisionId { get; set; }

    public Guid? PublishedRevisionId { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int DraftVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ContentRevision? DraftRevision { get; set; }

    public ContentRevision? PublishedRevision { get; set; }
}
