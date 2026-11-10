using System.Text.Json;

namespace Commerce.Application.Content;

public sealed record HomepageDraftResponse(
    int DraftVersion,
    Guid? DraftRevisionId,
    Guid? PublishedRevisionId,
    DateTimeOffset? PublishedAt,
    JsonElement Configuration);

public sealed record PublicBlockResponse(string Id, string Type, JsonElement Config);

public sealed record PublicHomepageResponse(DateTimeOffset? PublishedAt, IReadOnlyList<PublicBlockResponse> Blocks);

public sealed record HomepageIssue(string BlockId, string Message);

public sealed record HomepagePreviewResponse(
    DateTimeOffset? PublishedAt,
    IReadOnlyList<PublicBlockResponse> Blocks,
    IReadOnlyList<HomepageIssue> Issues);

public sealed record RevisionSummary(Guid Id, int Version, DateTimeOffset CreatedAt, Guid CreatedByUserId);

public sealed record ContentMediaResponse(
    Guid Id,
    string AltText,
    string ContentType,
    long ByteSize,
    int? Width,
    int? Height,
    string Path,
    IReadOnlyList<Catalog.MediaSource> Sources);
