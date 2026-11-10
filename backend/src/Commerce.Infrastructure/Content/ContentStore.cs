using System.Data;
using Commerce.Application.Catalog;
using Commerce.Application.Content;
using Commerce.Domain.Catalog;
using Commerce.Domain.Content;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Commerce.Infrastructure.Content;

internal sealed class ContentStore(CommerceDbContext database) : IContentStore
{
    public Task<ContentPage?> FindHomepageAsync(bool includeRevisions, CancellationToken cancellationToken)
    {
        var query = database.ContentPages.AsQueryable();
        if (includeRevisions)
        {
            query = query
                .Include(page => page.DraftRevision)!.ThenInclude(revision => revision!.Media).ThenInclude(link => link.Media)
                .Include(page => page.PublishedRevision)!.ThenInclude(revision => revision!.Media).ThenInclude(link => link.Media);
        }

        return query.FirstOrDefaultAsync(page => page.Key == ContentPage.HomepageKey, cancellationToken);
    }

    public async Task<ContentPage> EnsureHomepageAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await database.ContentPages.FirstOrDefaultAsync(page => page.Key == ContentPage.HomepageKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var page = new ContentPage
        {
            Id = Guid.CreateVersion7(),
            Key = ContentPage.HomepageKey,
            DraftVersion = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        database.ContentPages.Add(page);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return page;
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            return await database.ContentPages.FirstAsync(item => item.Key == ContentPage.HomepageKey, cancellationToken);
        }
    }

    public async Task<CatalogResult<int>> SaveDraftAsync(
        Guid pageId,
        int expectedVersion,
        ContentRevision revision,
        IReadOnlyList<Guid> mediaIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var version = await ExecuteAsync(
            """
            UPDATE content_pages
              SET draft_version = draft_version + 1,
                updated_at = @now
            WHERE id = @id AND draft_version = @expected
            RETURNING draft_version
            """,
            command =>
            {
                Add(command, "now", now);
                Add(command, "id", pageId);
                Add(command, "expected", expectedVersion);
            },
            cancellationToken);
        if (version is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            var current = await database.ContentPages.AsNoTracking().FirstOrDefaultAsync(page => page.Id == pageId, cancellationToken);
            return current is null
                ? CatalogResult<int>.NotFound()
                : CatalogResult<int>.Conflict("draftVersion", "This homepage was saved in another session. Reload to see the latest version.", currentVersion: current.DraftVersion);
        }

        revision.Version = version.Value;
        database.ContentRevisions.Add(revision);
        foreach (var mediaId in mediaIds.Distinct())
        {
            database.ContentRevisionMedia.Add(new ContentRevisionMedia
            {
                RevisionId = revision.Id,
                MediaId = mediaId,
                Revision = revision,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        await ExecuteAsync(
            """
            UPDATE content_pages
            SET draft_revision_id = @revision, updated_at = @now
            WHERE id = @id
            RETURNING draft_version
            """,
            command =>
            {
                Add(command, "revision", revision.Id);
                Add(command, "now", now);
                Add(command, "id", pageId);
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CatalogResult<int>.Ok(version.Value);
    }

    public async Task<CatalogResult<ContentPage>> PublishAsync(int expectedVersion, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var version = await ExecuteAsync(
            """
            UPDATE content_pages
            SET published_revision_id = draft_revision_id,
                published_at = @now,
                updated_at = @now
            WHERE key = 'homepage' AND draft_version = @expected AND draft_revision_id IS NOT NULL
            RETURNING draft_version
            """,
            command =>
            {
                Add(command, "now", now);
                Add(command, "expected", expectedVersion);
            },
            cancellationToken);
        if (version is null)
        {
            var current = await database.ContentPages.AsNoTracking().FirstOrDefaultAsync(page => page.Key == ContentPage.HomepageKey, cancellationToken);
            return current is null || current.DraftRevisionId is null
                ? CatalogResult<ContentPage>.Conflict("draftVersion", "Save a homepage draft before publishing.", currentVersion: current?.DraftVersion)
                : CatalogResult<ContentPage>.Conflict("draftVersion", "This homepage was saved in another session. Reload to see the latest version.", currentVersion: current.DraftVersion);
        }

        var page = await database.ContentPages.AsNoTracking().FirstAsync(item => item.Key == ContentPage.HomepageKey, cancellationToken);
        return CatalogResult<ContentPage>.Ok(page);
    }

    public Task<ContentRevision?> FindRevisionAsync(Guid id, CancellationToken cancellationToken) =>
        database.ContentRevisions
            .Include(revision => revision.Media)
            .FirstOrDefaultAsync(revision => revision.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RevisionSummary>> RevisionsAsync(CancellationToken cancellationToken) =>
        await database.ContentRevisions.AsNoTracking()
            .Where(revision => revision.Page.Key == ContentPage.HomepageKey)
            .OrderByDescending(revision => revision.Version)
            .Select(revision => new RevisionSummary(revision.Id, revision.Version, revision.CreatedAt, revision.CreatedByUserId))
            .ToListAsync(cancellationToken);

    public async Task<bool> MediaExistsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return true;
        }

        var found = await database.ContentMedia.CountAsync(media => distinct.Contains(media.Id), cancellationToken);
        return found == distinct.Length;
    }

    public async Task AddMediaAsync(ContentMedia media, CancellationToken cancellationToken)
    {
        database.ContentMedia.Add(media);
        await database.SaveChangesAsync(cancellationToken);
    }

    public Task<ContentMedia?> FindMediaAsync(Guid id, CancellationToken cancellationToken) =>
        database.ContentMedia.AsNoTracking().FirstOrDefaultAsync(media => media.Id == id, cancellationToken);

    public Task<bool> MediaIsPublishedAsync(Guid id, CancellationToken cancellationToken) =>
        database.ContentRevisionMedia.AsNoTracking().AnyAsync(
            link => link.MediaId == id && link.RevisionId == database.ContentPages
                .Where(page => page.Key == ContentPage.HomepageKey)
                .Select(page => page.PublishedRevisionId)
                .FirstOrDefault(),
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, (string Name, string Slug)>> ActiveProductsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, (string, string)>();
        }

        var rows = await database.Products.AsNoTracking()
            .Where(product => ids.Contains(product.Id) && product.Status == ProductStatus.Active)
            .Select(product => new { product.Id, product.Name, product.Slug })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.Id, row => (row.Name, row.Slug));
    }

    public async Task<IReadOnlyDictionary<Guid, (string Name, string Slug)>> ActiveCollectionsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, (string, string)>();
        }

        var rows = await database.Collections.AsNoTracking()
            .Where(collection => ids.Contains(collection.Id) && collection.IsActive)
            .Select(collection => new { collection.Id, collection.Name, collection.Slug })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.Id, row => (row.Name, row.Slug));
    }

    private async Task<int?> ExecuteAsync(
        string sql,
        Action<System.Data.Common.DbCommand> bind,
        CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = sql;
        bind(command);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Add(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
