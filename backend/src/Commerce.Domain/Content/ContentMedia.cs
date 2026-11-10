namespace Commerce.Domain.Content;

public sealed class ContentMedia
{
    public Guid Id { get; set; }

    public string ObjectKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long ByteSize { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string AltText { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
