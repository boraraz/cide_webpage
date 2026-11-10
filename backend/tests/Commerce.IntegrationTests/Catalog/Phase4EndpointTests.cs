using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Commerce.Application.Catalog;
using Commerce.Application.Content;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Commerce.IntegrationTests.Catalog;

[Collection(CatalogCollection.Name)]
public sealed class Phase4EndpointTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task Product_version_conflicts_and_last_write_wins_when_omitted()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        var created = await CatalogHttp.PostAsync(admin, "/api/v1/admin/products", new
        {
            name = "Version Tee",
            shortDescription = "Version short",
            description = "Version description.",
        });
        var product = (await created.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.Equal(1, product.Version);

        var first = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/products/{product.Id}", new { name = "Version Tee A", expectedVersion = 1 });
        var updated = (await first.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(2, updated.Version);

        var stale = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/products/{product.Id}", new { name = "Version Tee Stale", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var problem = await stale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, problem.GetProperty("currentVersion").GetInt32());
        var reloaded = await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}");
        Assert.Equal("Version Tee A", reloaded!.Name);

        var omitted = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/products/{product.Id}", new { name = "Version Tee B" });
        var latest = (await omitted.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);
        Assert.Equal("Version Tee B", latest.Name);
        Assert.Equal(3, latest.Version);
    }

    [Fact]
    public async Task Bulk_variants_media_order_and_collection_links_are_transactional()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var category = await CatalogHttp.PostAsync(admin, "/api/v1/admin/categories", new { name = $"Tops {suffix}", slug = $"tops-{suffix}" });
        var createdCategory = (await category.Content.ReadFromJsonAsync<CategoryResponse>())!;
        var firstCollection = await CatalogHttp.PostAsync(admin, "/api/v1/admin/collections", new { name = $"Edit {suffix}", slug = $"edit-{suffix}" });
        var secondCollection = await CatalogHttp.PostAsync(admin, "/api/v1/admin/collections", new { name = $"Drop {suffix}", slug = $"drop-{suffix}" });
        var edit = (await firstCollection.Content.ReadFromJsonAsync<CollectionResponse>())!;
        var drop = (await secondCollection.Content.ReadFromJsonAsync<CollectionResponse>())!;
        var created = await CatalogHttp.PostAsync(admin, "/api/v1/admin/products", new
        {
            name = $"Matrix {suffix}",
            shortDescription = "Matrix short",
            description = "Matrix description.",
            categoryIds = new[] { createdCategory.Id },
            collectionIds = new[] { edit.Id },
        });
        var product = (await created.Content.ReadFromJsonAsync<ProductResponse>())!;
        var color = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/options", new { name = "Color", expectedVersion = product.Version });
        var colorBody = (await color.Content.ReadFromJsonAsync<OptionResponse>())!;
        var black = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/options/{colorBody.Id}/values", new { value = "Black", expectedVersion = colorBody.ProductVersion });
        var blackBody = (await black.Content.ReadFromJsonAsync<OptionValueResponse>())!;
        var afterBlack = await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}");
        var white = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/options/{colorBody.Id}/values", new { value = "White", expectedVersion = afterBlack!.Version });
        var whiteBody = (await white.Content.ReadFromJsonAsync<OptionValueResponse>())!;
        var afterWhite = await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}");
        var version = afterWhite!.Version;
        var sku = $"MX-{suffix}".ToUpperInvariant();
        var bulk = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants/bulk", new
        {
            expectedVersion = version,
            variants = new object[]
            {
                new { sku, price = "100.00", optionValueIds = new[] { blackBody.Id }, initialQuantity = 2 },
                new { sku, price = "110.00", optionValueIds = new[] { whiteBody.Id }, initialQuantity = 1 },
            },
        });
        Assert.Equal(HttpStatusCode.Conflict, bulk.StatusCode);
        var afterFailure = await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}");
        Assert.Empty(afterFailure!.Variants);

        var createdVariants = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants/bulk", new
        {
            expectedVersion = version,
            variants = new object[]
            {
                new { sku = $"A-{suffix}".ToUpperInvariant(), price = "100.00", optionValueIds = new[] { blackBody.Id }, initialQuantity = 2 },
                new { sku = $"B-{suffix}".ToUpperInvariant(), price = "110.00", optionValueIds = new[] { whiteBody.Id }, initialQuantity = 1 },
            },
        });
        Assert.Equal(HttpStatusCode.Created, createdVariants.StatusCode);
        var variants = (await createdVariants.Content.ReadFromJsonAsync<VariantResponse[]>())!;
        Assert.Equal(2, variants.Length);

        var firstImage = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{product.Id}/media", CatalogHttp.Png, "First");
        var secondImage = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{product.Id}/media", CatalogHttp.Png, "Second");
        var firstMedia = (await firstImage.Content.ReadFromJsonAsync<MediaResponse>())!;
        var secondMedia = (await secondImage.Content.ReadFromJsonAsync<MediaResponse>())!;
        var current = await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}");
        var ordered = await PutAsync(admin, $"/api/v1/admin/products/{product.Id}/media/order", new { ids = new[] { secondMedia.Id, firstMedia.Id }, expectedVersion = current!.Version });
        Assert.Equal(HttpStatusCode.OK, ordered.StatusCode);
        var orderedProduct = (await ordered.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.Equal(secondMedia.Id, orderedProduct.Media[0].Id);

        var linked = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/collections/{drop.Id}/products", new { productId = product.Id, expectedVersion = orderedProduct.Version });
        Assert.Equal(HttpStatusCode.NoContent, linked.StatusCode);
        var linkedProduct = await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}");
        Assert.Contains(linkedProduct!.Collections, collection => collection.Id == edit.Id);
        Assert.Contains(linkedProduct.Collections, collection => collection.Id == drop.Id);

        var summary = await admin.GetFromJsonAsync<DashboardSummary>("/api/v1/admin/dashboard/summary");
        Assert.True(summary!.TotalProducts >= 1);
        Assert.Equal(5, summary.LowStockThreshold);
        var filtered = await admin.GetAsync($"/api/v1/admin/products?q=Matrix&categoryId={createdCategory.Id}");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
    }

    [Fact]
    public async Task Homepage_revisions_stay_immutable_and_media_follows_the_published_revision()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        using var anonymous = factory.CreateClient();
        var before = await anonymous.GetAsync("/api/v1/content/homepage");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal("public, max-age=30", before.Headers.CacheControl?.ToString());

        var draft = await admin.GetFromJsonAsync<HomepageDraftResponse>("/api/v1/admin/content/homepage");
        var upload = await CatalogHttp.UploadAsync(admin, "/api/v1/admin/content/media", CatalogHttp.Png, "Hero photo");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var media = (await upload.Content.ReadFromJsonAsync<ContentMediaResponse>())!;
        var configuration = Homepage(media.Id, includeMedia: true);
        var saved = await PutAsync(admin, "/api/v1/admin/content/homepage", new { configuration, expectedVersion = draft!.DraftVersion });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedDraft = (await saved.Content.ReadFromJsonAsync<HomepageDraftResponse>())!;
        var unchanged = await anonymous.GetFromJsonAsync<PublicHomepageResponse>("/api/v1/content/homepage");
        Assert.DoesNotContain(unchanged!.Blocks, block => block.Id == "hero-1");

        var hidden = await anonymous.GetAsync($"/api/v1/content/media/{media.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var adminImage = await admin.GetAsync($"/api/v1/admin/content/media/{media.Id}");
        Assert.Equal(HttpStatusCode.OK, adminImage.StatusCode);
        var adminCache = string.Join(", ", adminImage.Headers.GetValues("Cache-Control"));
        Assert.Contains("private", adminCache, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", adminCache, StringComparison.OrdinalIgnoreCase);

        var stale = await PutAsync(admin, "/api/v1/admin/content/homepage", new { configuration, expectedVersion = draft.DraftVersion });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var published = await CatalogHttp.PostAsync(admin, "/api/v1/admin/content/homepage/publish", new { expectedVersion = savedDraft.DraftVersion });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var visible = await anonymous.GetAsync($"/api/v1/content/media/{media.Id}");
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        Assert.Equal("public, max-age=86400, immutable", string.Join(", ", visible.Headers.GetValues("Cache-Control")));

        var unusedUpload = await CatalogHttp.UploadAsync(admin, "/api/v1/admin/content/media", CatalogHttp.Png, "Unused photo");
        var unused = (await unusedUpload.Content.ReadFromJsonAsync<ContentMediaResponse>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/content/media/{unused.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/admin/content/media/{unused.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/v1/content/media/{media.Id}")).StatusCode);

        var stillUsed = Homepage(media.Id, includeMedia: true, heading: "Still used");
        var second = await PutAsync(admin, "/api/v1/admin/content/homepage", new { configuration = stillUsed, expectedVersion = savedDraft.DraftVersion });
        var secondDraft = (await second.Content.ReadFromJsonAsync<HomepageDraftResponse>())!;
        var republished = await CatalogHttp.PostAsync(admin, "/api/v1/admin/content/homepage/publish", new { expectedVersion = secondDraft.DraftVersion });
        Assert.Equal(HttpStatusCode.OK, republished.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/v1/content/media/{media.Id}")).StatusCode);

        var dropped = Homepage(media.Id, includeMedia: false);
        var third = await PutAsync(admin, "/api/v1/admin/content/homepage", new { configuration = dropped, expectedVersion = secondDraft.DraftVersion });
        var thirdDraft = (await third.Content.ReadFromJsonAsync<HomepageDraftResponse>())!;
        await CatalogHttp.PostAsync(admin, "/api/v1/admin/content/homepage/publish", new { expectedVersion = thirdDraft.DraftVersion });
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/content/media/{media.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/admin/content/media/{media.Id}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var revisionId = thirdDraft.DraftRevisionId;
        var exception = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE content_revisions SET version = version WHERE id = {revisionId}"));
        Assert.Equal("55000", exception.SqlState);

        var restored = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/content/homepage/revisions/{savedDraft.DraftRevisionId}/restore", new { expectedVersion = thirdDraft.DraftVersion });
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var publicAfterRestore = await anonymous.GetFromJsonAsync<PublicHomepageResponse>("/api/v1/content/homepage");
        Assert.DoesNotContain(publicAfterRestore!.Blocks, block => block.Id == "hero-1");
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/content/media/{media.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/admin/content/media/{media.Id}")).StatusCode);
    }

    [Fact]
    public async Task Published_featured_products_omit_draft_and_archived_products_and_their_media()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        using var anonymous = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var category = await CatalogHttp.PostAsync(admin, "/api/v1/admin/categories", new { name = $"Featured {suffix}", slug = $"featured-{suffix}" });
        var createdCategory = (await category.Content.ReadFromJsonAsync<CategoryResponse>())!;
        var active = await ShelfAsync(admin, createdCategory.Id, $"Active {suffix}", $"ACT-{suffix}", publish: true, archive: false);
        var draft = await ShelfAsync(admin, createdCategory.Id, $"Draft {suffix}", $"DRF-{suffix}", publish: false, archive: false);
        var archived = await ShelfAsync(admin, createdCategory.Id, $"Archived {suffix}", $"ARC-{suffix}", publish: true, archive: true);

        var renamed = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/media/{active.Media.Id}", new { altText = "Studio front", expectedVersion = active.Product.Version });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var renamedMedia = (await renamed.Content.ReadFromJsonAsync<MediaResponse>())!;
        Assert.Equal("Studio front", renamedMedia.AltText);
        var afterRename = (await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{active.Product.Id}"))!;
        var blank = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/media/{active.Media.Id}", new { altText = " ", expectedVersion = afterRename.Version });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        var tooLong = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/media/{active.Media.Id}", new { altText = new string('a', 201), expectedVersion = afterRename.Version });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var persisted = await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{active.Product.Id}");
        Assert.Equal("Studio front", persisted!.Media.Single().AltText);
        Assert.Equal(afterRename.Version, persisted.Version);

        var draftPage = await admin.GetFromJsonAsync<HomepageDraftResponse>("/api/v1/admin/content/homepage");
        var configuration = "{\"blocks\":[{\"id\":\"featured-1\",\"type\":\"featuredProducts\",\"visible\":true,\"config\":{\"heading\":\"Featured\",\"productIds\":[\""
            + active.Product.Id + "\",\"" + draft.Product.Id + "\",\"" + archived.Product.Id + "\"]}}]}";
        var saved = await PutAsync(admin, "/api/v1/admin/content/homepage", new { configuration, expectedVersion = draftPage!.DraftVersion });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedDraft = (await saved.Content.ReadFromJsonAsync<HomepageDraftResponse>())!;
        var published = await CatalogHttp.PostAsync(admin, "/api/v1/admin/content/homepage/publish", new { expectedVersion = savedDraft.DraftVersion });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var page = (await anonymous.GetFromJsonAsync<PublicHomepageResponse>("/api/v1/content/homepage"))!;
        var block = Assert.Single(page.Blocks, item => item.Id == "featured-1");
        var products = block.Config.GetProperty("products");
        var shown = Assert.Single(products.EnumerateArray());
        Assert.Equal(active.Product.Slug, shown.GetProperty("slug").GetString());
        var document = page.Blocks[0].Config.GetRawText();
        Assert.DoesNotContain(draft.Product.Slug, document, StringComparison.Ordinal);
        Assert.DoesNotContain(archived.Product.Slug, document, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/v1/media/{active.Media.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/media/{draft.Media.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/media/{archived.Media.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/products/{draft.Product.Slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/products/{archived.Product.Slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/admin/media/{draft.Media.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/admin/media/{archived.Media.Id}")).StatusCode);
    }

    private static async Task<(ProductResponse Product, MediaResponse Media)> ShelfAsync(
        HttpClient admin,
        Guid categoryId,
        string name,
        string sku,
        bool publish,
        bool archive)
    {
        var created = await CatalogHttp.PostAsync(admin, "/api/v1/admin/products", new
        {
            name,
            shortDescription = "Shelf short",
            description = "Shelf description.",
            categoryIds = new[] { categoryId },
        });
        var product = (await created.Content.ReadFromJsonAsync<ProductResponse>())!;
        var variants = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants/bulk", new
        {
            expectedVersion = product.Version,
            variants = new object[] { new { sku, price = "25.00", optionValueIds = Array.Empty<Guid>(), initialQuantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.Created, variants.StatusCode);
        var image = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{product.Id}/media", CatalogHttp.Png, $"{name} photo");
        Assert.Equal(HttpStatusCode.Created, image.StatusCode);
        var media = (await image.Content.ReadFromJsonAsync<MediaResponse>())!;
        var current = (await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}"))!;
        if (publish)
        {
            var published = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/publish", new { expectedVersion = current.Version });
            Assert.Equal(HttpStatusCode.OK, published.StatusCode);
            current = (await published.Content.ReadFromJsonAsync<ProductResponse>())!;
        }

        if (archive)
        {
            var archived = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/archive", new { expectedVersion = current.Version });
            Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
            current = (await archived.Content.ReadFromJsonAsync<ProductResponse>())!;
        }

        return (current, media);
    }

    private static string Homepage(Guid mediaId, bool includeMedia, string heading = "Train")
    {
        var block = includeMedia
            ? "{\"id\":\"hero-1\",\"type\":\"hero\",\"visible\":true,\"config\":{\"heading\":\"" + heading + "\",\"mediaId\":\"" + mediaId + "\",\"destination\":\"/products\"}}"
            : "{\"id\":\"space-1\",\"type\":\"spacer\",\"visible\":true,\"config\":{\"size\":\"md\"}}";
        return "{\"blocks\":[" + block + "]}";
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string path, object body)
    {
        await Commerce.IntegrationTests.Identity.AuthHttp.PrepareAsync(client);
        return await client.PutAsJsonAsync(path, body);
    }
}
