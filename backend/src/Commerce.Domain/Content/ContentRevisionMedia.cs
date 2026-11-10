namespace Commerce.Domain.Content;

public sealed class ContentRevisionMedia
{
    public Guid RevisionId { get; set; }

    public Guid MediaId { get; set; }

    public ContentRevision Revision { get; set; } = null!;

    public ContentMedia Media { get; set; } = null!;
}
