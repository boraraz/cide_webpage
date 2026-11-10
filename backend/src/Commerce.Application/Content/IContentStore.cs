using Commerce.Application.Catalog;
using Commerce.Domain.Content;

namespace Commerce.Application.Content;

public interface IContentStore
{
    Task<ContentPage?> FindHomepageAsync(bool includeRevisions, CancellationToken cancellationToken);

    Task<ContentPage> EnsureHomepageAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task<CatalogResult<int>> SaveDraftAsync(
        Guid pageId,
        int expectedVersion,
        ContentRevision revision,
        IReadOnlyList<Guid> mediaIds,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<CatalogResult<ContentPage>> PublishAsync(int expectedVersion, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ContentRevision?> FindRevisionAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RevisionSummary>> RevisionsAsync(CancellationToken cancellationToken);

    Task<bool> MediaExistsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task AddMediaAsync(ContentMedia media, CancellationToken cancellationToken);

    Task<ContentMedia?> FindMediaAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> MediaIsPublishedAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, (string Name, string Slug)>> ActiveProductsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, (string Name, string Slug)>> ActiveCollectionsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
}
