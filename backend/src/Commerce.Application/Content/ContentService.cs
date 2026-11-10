using System.Text.Json;
using System.Text.Json.Nodes;
using Commerce.Application.Catalog;
using Commerce.Domain.Catalog;
using Commerce.Domain.Content;

namespace Commerce.Application.Content;

public sealed class ContentService(IContentStore store, IObjectStorage storage, TimeProvider time)
{
    public async Task<HomepageDraftResponse> GetDraftAsync(CancellationToken cancellationToken)
    {
        var page = await store.EnsureHomepageAsync(time.GetUtcNow(), cancellationToken);
        page = await store.FindHomepageAsync(includeRevisions: true, cancellationToken) ?? page;
        var configuration = page.DraftRevision?.Configuration ?? """{"blocks":[]}""";
        return new HomepageDraftResponse(
            page.DraftVersion,
            page.DraftRevisionId,
            page.PublishedRevisionId,
            page.PublishedAt,
            JsonSerializer.Deserialize<JsonElement>(configuration));
    }

    public async Task<CatalogResult<HomepageDraftResponse>> SaveDraftAsync(
        string? configuration,
        int expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (!HomepageDocument.TryNormalize(configuration, out var normalized, out var errors, out var mediaIds))
        {
            return CatalogResult<HomepageDraftResponse>.Invalid(errors);
        }

        if (!await store.MediaExistsAsync(mediaIds, cancellationToken))
        {
            return CatalogResult<HomepageDraftResponse>.Invalid(new Dictionary<string, string[]>
            {
                ["configuration"] = ["One or more images are not in the content library."],
            });
        }

        var page = await store.EnsureHomepageAsync(time.GetUtcNow(), cancellationToken);
        var now = time.GetUtcNow();
        var revision = new ContentRevision
        {
            Id = Guid.CreateVersion7(),
            PageId = page.Id,
            Page = page,
            Version = expectedVersion + 1,
            Configuration = normalized,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
        };
        var saved = await store.SaveDraftAsync(page.Id, expectedVersion, revision, mediaIds, now, cancellationToken);
        if (!saved.Succeeded)
        {
            return CatalogResult<HomepageDraftResponse>.Fail(saved.Failure!);
        }

        var version = saved.Value;

        return CatalogResult<HomepageDraftResponse>.Ok(new HomepageDraftResponse(
            version,
            revision.Id,
            page.PublishedRevisionId,
            page.PublishedAt,
            JsonSerializer.Deserialize<JsonElement>(normalized)));
    }

    public async Task<CatalogResult<HomepageDraftResponse>> PublishAsync(int expectedVersion, CancellationToken cancellationToken)
    {
        var published = await store.PublishAsync(expectedVersion, time.GetUtcNow(), cancellationToken);
        if (!published.Succeeded || published.Value is null)
        {
            return CatalogResult<HomepageDraftResponse>.Fail(published.Failure!);
        }

        var page = published.Value;

        var current = await store.FindHomepageAsync(includeRevisions: true, cancellationToken) ?? page;
        var configuration = current.DraftRevision?.Configuration ?? """{"blocks":[]}""";
        return CatalogResult<HomepageDraftResponse>.Ok(new HomepageDraftResponse(
            current.DraftVersion,
            current.DraftRevisionId,
            current.PublishedRevisionId,
            current.PublishedAt,
            JsonSerializer.Deserialize<JsonElement>(configuration)));
    }

    public async Task<CatalogResult<HomepageDraftResponse>> RestoreAsync(
        Guid revisionId,
        int expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var revision = await store.FindRevisionAsync(revisionId, cancellationToken);
        if (revision is null)
        {
            return CatalogResult<HomepageDraftResponse>.NotFound();
        }

        return await SaveDraftAsync(
            revision.Configuration,
            expectedVersion,
            actorUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<RevisionSummary>> RevisionsAsync(CancellationToken cancellationToken) =>
        await store.RevisionsAsync(cancellationToken);

    public async Task<PublicHomepageResponse> PublicAsync(CancellationToken cancellationToken)
    {
        var page = await store.FindHomepageAsync(includeRevisions: true, cancellationToken);
        if (page?.PublishedRevision is null)
        {
            return new PublicHomepageResponse(null, []);
        }

        var projected = await ProjectAsync(page.PublishedRevision, page.PublishedAt, preview: false, cancellationToken);
        return new PublicHomepageResponse(page.PublishedAt, projected.Blocks);
    }

    public async Task<HomepagePreviewResponse> PreviewAsync(CancellationToken cancellationToken)
    {
        var page = await store.FindHomepageAsync(includeRevisions: true, cancellationToken);
        if (page?.DraftRevision is null)
        {
            return new HomepagePreviewResponse(null, [], []);
        }

        return await ProjectAsync(page.DraftRevision, null, preview: true, cancellationToken);
    }

    public async Task<CatalogResult<ContentMediaResponse>> UploadAsync(
        ReadOnlyMemory<byte> content,
        string? altText,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var errors = CatalogSupport.Text("altText", altText, PublishRules.AltTextMaxLength, required: true);
        var contentType = ImageSignature.DetectContentType(content.Span);
        if (contentType is null)
        {
            errors["file"] = ["Upload a JPEG, PNG or WebP image up to 5 MB."];
        }

        if (content.Length > ImageSignature.MaximumBytes)
        {
            errors["file"] = ["Upload a JPEG, PNG or WebP image up to 5 MB."];
        }

        if (errors.Count > 0 || contentType is null)
        {
            return CatalogResult<ContentMediaResponse>.Invalid(errors);
        }

        var now = time.GetUtcNow();
        var dimensions = ImageHeader.Read(content.Span, contentType);
        var media = new ContentMedia
        {
            Id = Guid.CreateVersion7(),
            ContentType = contentType,
            ByteSize = content.Length,
            Width = dimensions?.Width,
            Height = dimensions?.Height,
            AltText = altText!.Trim(),
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            ObjectKey = $"content/{Guid.Empty}.{ImageSignature.ExtensionFor(contentType)}",
        };
        media.ObjectKey = $"content/{media.Id}.{ImageSignature.ExtensionFor(contentType)}";
        await storage.PutAsync(media.ObjectKey, content, contentType, cancellationToken);
        try
        {
            await store.AddMediaAsync(media, cancellationToken);
            return CatalogResult<ContentMediaResponse>.Ok(Map(media, admin: true));
        }
        catch
        {
            await storage.DeleteAsync(media.ObjectKey, cancellationToken);
            throw;
        }
    }

    public async Task<StoredObject?> OpenAsync(Guid id, bool publishedOnly, CancellationToken cancellationToken)
    {
        var media = await store.FindMediaAsync(id, cancellationToken);
        if (media is null)
        {
            return null;
        }

        if (publishedOnly && !await store.MediaIsPublishedAsync(id, cancellationToken))
        {
            return null;
        }

        return await storage.OpenAsync(media.ObjectKey, cancellationToken);
    }

    private async Task<HomepagePreviewResponse> ProjectAsync(
        ContentRevision revision,
        DateTimeOffset? publishedAt,
        bool preview,
        CancellationToken cancellationToken)
    {
        var document = JsonNode.Parse(revision.Configuration)!.AsObject();
        var blocks = document["blocks"]!.AsArray();
        var productIds = new List<Guid>();
        var collectionIds = new List<Guid>();
        foreach (var node in blocks)
        {
            var config = node!["config"]!.AsObject();
            if (config["productIds"] is JsonArray products)
            {
                productIds.AddRange(products.Select(item => Guid.Parse(item!.GetValue<string>())));
            }

            if (Guid.TryParse(config["collectionId"]?.GetValue<string>(), out var collectionId))
            {
                collectionIds.Add(collectionId);
            }
        }

        var productsById = await store.ActiveProductsAsync(productIds, cancellationToken);
        var collectionsById = await store.ActiveCollectionsAsync(collectionIds, cancellationToken);
        var mediaById = revision.Media.ToDictionary(link => link.MediaId, link => link.Media);
        var projected = new List<PublicBlockResponse>();
        var issues = new List<HomepageIssue>();
        foreach (var node in blocks)
        {
            var block = node!.AsObject();
            var visible = block["visible"]!.GetValue<bool>();
            if (!preview && !visible)
            {
                continue;
            }

            var id = block["id"]!.GetValue<string>();
            var type = block["type"]!.GetValue<string>();
            var config = block["config"]!.AsObject();
            if (config["mediaId"] is JsonNode mediaNode && Guid.TryParse(mediaNode.GetValue<string>(), out var mediaId))
            {
                if (!mediaById.TryGetValue(mediaId, out var media))
                {
                    issues.Add(new HomepageIssue(id, "Image is missing."));
                    if (!preview)
                    {
                        continue;
                    }
                }
                else
                {
                    config.Remove("mediaId");
                    config["sources"] = new JsonArray(
                        new JsonObject
                        {
                            ["url"] = $"/api/v1/content/media/{media.Id}",
                            ["width"] = media.Width,
                            ["contentType"] = media.ContentType,
                        });
                    config["altText"] = media.AltText;
                }
            }

            if (config["productIds"] is JsonArray listed)
            {
                var kept = new JsonArray();
                foreach (var item in listed)
                {
                    var productId = Guid.Parse(item!.GetValue<string>());
                    if (productsById.TryGetValue(productId, out var product))
                    {
                        kept.Add(new JsonObject { ["id"] = productId, ["name"] = product.Name, ["slug"] = product.Slug });
                    }
                    else if (preview)
                    {
                        issues.Add(new HomepageIssue(id, "A featured product is not published."));
                    }
                }

                config["products"] = kept;
                config.Remove("productIds");
            }

            if (Guid.TryParse(config["collectionId"]?.GetValue<string>(), out var selectedCollection))
            {
                if (collectionsById.TryGetValue(selectedCollection, out var collection))
                {
                    config["collection"] = new JsonObject
                    {
                        ["id"] = selectedCollection,
                        ["name"] = collection.Name,
                        ["slug"] = collection.Slug,
                    };
                }
                else
                {
                    issues.Add(new HomepageIssue(id, "The featured collection is not available."));
                    if (!preview)
                    {
                        continue;
                    }
                }

                config.Remove("collectionId");
            }

            projected.Add(new PublicBlockResponse(id, type, JsonSerializer.Deserialize<JsonElement>(config.ToJsonString())));
        }

        return new HomepagePreviewResponse(publishedAt, projected, issues);
    }

    private static ContentMediaResponse Map(ContentMedia media, bool admin) => new(
        media.Id,
        media.AltText,
        media.ContentType,
        media.ByteSize,
        media.Width,
        media.Height,
        admin ? $"/api/v1/admin/content/media/{media.Id}" : $"/api/v1/content/media/{media.Id}",
        [new MediaSource(admin ? $"/api/v1/admin/content/media/{media.Id}" : $"/api/v1/content/media/{media.Id}", media.Width, media.ContentType)]);
}
