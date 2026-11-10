using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Commerce.Application.Catalog;
using Commerce.Application.Content;
using Commerce.IntegrationTests.Identity;

namespace Commerce.IntegrationTests.Catalog;

[Collection(CatalogCollection.Name)]
public sealed class Phase5EndpointTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task Price_basis_preserves_variant_bounds_and_from_price_is_explicit()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        using var anonymous = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var category = await CreateCategoryAsync(admin, $"Range {suffix}", $"range-{suffix}");
        var split = await CreatePricedProductAsync(admin, category.Id, $"Alpha {suffix}", $"alpha-{suffix}", "100.00", "900.00");
        await CreateSingleProductAsync(admin, category.Id, $"Zebra {suffix}", $"zebra-{suffix}", "500.00", 6);

        var legacy = await anonymous.GetFromJsonAsync<ProductPage>(
            $"/api/v1/products?q={suffix}&minPrice=400&maxPrice=600&sort=name");
        Assert.Equal(2, legacy!.Total);
        Assert.Equal($"alpha-{suffix}", legacy.Items[0].Slug);
        Assert.Equal("100.00", legacy.Items[0].FromPrice);

        var displayed = await anonymous.GetFromJsonAsync<ProductPage>(
            $"/api/v1/products?q={suffix}&minPrice=400&maxPrice=600&priceBasis=from&sort=name");
        var only = Assert.Single(displayed!.Items);
        Assert.Equal($"zebra-{suffix}", only.Slug);

        var filters = await anonymous.GetFromJsonAsync<PublicCatalogFilters>(
            $"/api/v1/products/filters?q={suffix}&priceBasis=from");
        Assert.Equal("from", filters!.PriceBasis);
        Assert.Equal("100.00", filters.MinPrice);
        Assert.Equal("500.00", filters.MaxPrice);
        var variantBounds = await anonymous.GetFromJsonAsync<PublicCatalogFilters>($"/api/v1/products/filters?q={suffix}");
        Assert.Equal("variant", variantBounds!.PriceBasis);
        Assert.Equal("900.00", variantBounds.MaxPrice);
        Assert.Contains(filters.Colors, choice => choice.Value == "black");

        var detail = await anonymous.GetFromJsonAsync<PublicProductDetail>($"/api/v1/products/{split.Slug}");
        Assert.Contains(detail!.Variants, variant => variant.Availability == "limited");
        Assert.Contains(detail.Variants, variant => variant.Availability == "out_of_stock");
        var stocked = await anonymous.GetFromJsonAsync<PublicProductDetail>($"/api/v1/products/zebra-{suffix}");
        Assert.Equal("in_stock", Assert.Single(stocked!.Variants).Availability);
    }

    [Fact]
    public async Task Related_products_prefer_collection_then_category_and_skip_drafts()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        using var anonymous = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var shared = await CreateCategoryAsync(admin, $"Shared {suffix}", $"shared-{suffix}");
        var other = await CreateCategoryAsync(admin, $"Other {suffix}", $"other-{suffix}");
        var collection = await CreateCollectionAsync(admin, $"Drop {suffix}", $"drop-{suffix}");
        var first = await CreateSingleProductAsync(admin, shared.Id, $"First {suffix}", $"first-{suffix}", "40.00", 3, collection.Id);
        var second = await CreateSingleProductAsync(admin, shared.Id, $"Second {suffix}", $"second-{suffix}", "40.00", 3, collection.Id);
        await CreateSingleProductAsync(admin, shared.Id, $"Third {suffix}", $"third-{suffix}", "40.00", 3);
        await CreateSingleProductAsync(admin, other.Id, $"Fourth {suffix}", $"fourth-{suffix}", "40.00", 3);
        var draft = await CatalogHttp.PostAsync(admin, "/api/v1/admin/products", new
        {
            name = $"Hidden {suffix}",
            slug = $"hidden-{suffix}",
            shortDescription = "Hidden short",
            description = "Hidden description.",
            categoryIds = new[] { shared.Id },
            collectionIds = new[] { collection.Id },
        });
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);

        var related = await anonymous.GetFromJsonAsync<PublicProductCard[]>($"/api/v1/products/{first.Slug}/related?limit=8");
        Assert.Equal($"second-{suffix}", related![0].Slug);
        Assert.Equal($"third-{suffix}", related[1].Slug);
        Assert.DoesNotContain(related, card => card.Slug == $"hidden-{suffix}");
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/products/missing-{suffix}/related")).StatusCode);
    }

    [Fact]
    public async Task Derivatives_are_advertised_only_when_stored_and_follow_publication()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        using var anonymous = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var category = await CreateCategoryAsync(admin, $"Media {suffix}", $"media-{suffix}");
        var product = await CreateSingleProductAsync(admin, category.Id, $"Frame {suffix}", $"frame-{suffix}", "80.00", 2, image: WidePng());
        var detail = await anonymous.GetFromJsonAsync<PublicProductDetail>($"/api/v1/products/{product.Slug}");
        var media = Assert.Single(detail!.Media);
        Assert.Contains(media.Sources, source => source.Url.EndsWith($"?w=480", StringComparison.Ordinal) && source.MimeType == "image/webp");
        Assert.DoesNotContain(media.Sources, source => source.Url.Contains("?w=960", StringComparison.Ordinal));

        var derivative = await anonymous.GetAsync($"/api/v1/media/{media.Id}?w=480");
        Assert.Equal(HttpStatusCode.OK, derivative.StatusCode);
        Assert.Equal("image/webp", derivative.Content.Headers.ContentType?.MediaType);
        Assert.Equal("public, max-age=86400, immutable", string.Join(", ", derivative.Headers.GetValues("Cache-Control")));
        var bytes = await derivative.Content.ReadAsByteArrayAsync();
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WEBP", Encoding.ASCII.GetString(bytes, 8, 4));

        var fallback = await anonymous.GetAsync($"/api/v1/media/{media.Id}?w=960");
        Assert.Equal(HttpStatusCode.OK, fallback.StatusCode);
        Assert.Equal("image/png", fallback.Content.Headers.ContentType?.MediaType);
        Assert.Contains("max-age=300", string.Join(", ", fallback.Headers.GetValues("Cache-Control")), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/media/{media.Id}?w=100")).StatusCode);

        var draftPage = await admin.GetFromJsonAsync<HomepageDraftResponse>("/api/v1/admin/content/homepage");
        var upload = await CatalogHttp.UploadAsync(admin, "/api/v1/admin/content/media", WidePng(), "Hero");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var contentMedia = (await upload.Content.ReadFromJsonAsync<ContentMediaResponse>())!;
        Assert.Contains(contentMedia.Sources, source => source.Url.Contains("?w=480", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/content/media/{contentMedia.Id}")).StatusCode);

        var configuration = "{\"blocks\":[{\"id\":\"hero-5\",\"type\":\"hero\",\"visible\":true,\"config\":{\"heading\":\"Hero\",\"mediaId\":\""
            + contentMedia.Id + "\",\"destination\":\"/shop\"}}]}";
        var saved = await PutAsync(admin, "/api/v1/admin/content/homepage", new { configuration, expectedVersion = draftPage!.DraftVersion });
        var savedDraft = (await saved.Content.ReadFromJsonAsync<HomepageDraftResponse>())!;
        await CatalogHttp.PostAsync(admin, "/api/v1/admin/content/homepage/publish", new { expectedVersion = savedDraft.DraftVersion });
        var publishedImage = await anonymous.GetAsync($"/api/v1/content/media/{contentMedia.Id}?w=480");
        Assert.Equal(HttpStatusCode.OK, publishedImage.StatusCode);
        var homepage = await anonymous.GetFromJsonAsync<PublicHomepageResponse>("/api/v1/content/homepage");
        var hero = Assert.Single(homepage!.Blocks, block => block.Id == "hero-5");
        Assert.Contains("?w=480", hero.Config.GetProperty("sources")[0].GetProperty("url").GetString() is null
            ? hero.Config.GetRawText()
            : hero.Config.GetRawText(), StringComparison.Ordinal);

        var current = await admin.GetFromJsonAsync<HomepageDraftResponse>("/api/v1/admin/content/homepage");
        var cleared = await PutAsync(admin, "/api/v1/admin/content/homepage", new { configuration = "{\"blocks\":[]}", expectedVersion = current!.DraftVersion });
        var clearedDraft = (await cleared.Content.ReadFromJsonAsync<HomepageDraftResponse>())!;
        await CatalogHttp.PostAsync(admin, "/api/v1/admin/content/homepage/publish", new { expectedVersion = clearedDraft.DraftVersion });
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/content/media/{contentMedia.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/admin/content/media/{contentMedia.Id}")).StatusCode);

        var sitemap = await anonymous.GetStringAsync("/api/v1/sitemap.xml");
        Assert.Contains($"/product/{product.Slug}", sitemap, StringComparison.Ordinal);
        Assert.Contains($"/category/{category.Slug}", sitemap, StringComparison.Ordinal);
    }

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient admin, string name, string slug)
    {
        var response = await CatalogHttp.PostAsync(admin, "/api/v1/admin/categories", new { name, slug, isActive = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    private static async Task<CollectionResponse> CreateCollectionAsync(HttpClient admin, string name, string slug)
    {
        var response = await CatalogHttp.PostAsync(admin, "/api/v1/admin/collections", new { name, slug, isActive = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CollectionResponse>())!;
    }

    private static async Task<ProductResponse> CreatePricedProductAsync(
        HttpClient admin, Guid categoryId, string name, string slug, string lowPrice, string highPrice)
    {
        var product = await CreateShellAsync(admin, categoryId, name, slug, null);
        var color = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/options", new { name = "Color" });
        var colorBody = (await color.Content.ReadFromJsonAsync<OptionResponse>())!;
        var black = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/options/{colorBody.Id}/values", new { value = "Black" });
        var white = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/options/{colorBody.Id}/values", new { value = "White" });
        var blackBody = (await black.Content.ReadFromJsonAsync<OptionValueResponse>())!;
        var whiteBody = (await white.Content.ReadFromJsonAsync<OptionValueResponse>())!;
        var current = (await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}"))!;
        var variants = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants/bulk", new
        {
            expectedVersion = current.Version,
            variants = new object[]
            {
                new { sku = $"LOW-{slug}"[..16].ToUpperInvariant(), price = lowPrice, compareAtPrice = "150.00", optionValueIds = new[] { blackBody.Id }, initialQuantity = 2 },
                new { sku = $"HIGH-{slug}"[..16].ToUpperInvariant(), price = highPrice, optionValueIds = new[] { whiteBody.Id }, initialQuantity = 0 },
            },
        });
        Assert.Equal(HttpStatusCode.Created, variants.StatusCode);
        return await FinishAsync(admin, product.Id);
    }

    private static Task<ProductResponse> CreateSingleProductAsync(
        HttpClient admin, Guid categoryId, string name, string slug, string price, int stock, Guid? collectionId = null, byte[]? image = null) =>
        CreateAndPublishAsync(admin, categoryId, name, slug, price, stock, collectionId, image);

    private static async Task<ProductResponse> CreateAndPublishAsync(
        HttpClient admin, Guid categoryId, string name, string slug, string price, int stock, Guid? collectionId, byte[]? image)
    {
        var product = await CreateShellAsync(admin, categoryId, name, slug, collectionId);
        var current = (await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}"))!;
        var variants = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants/bulk", new
        {
            expectedVersion = current.Version,
            variants = new object[]
            {
                new { sku = $"SKU-{slug}"[..16].ToUpperInvariant(), price, optionValueIds = Array.Empty<Guid>(), initialQuantity = stock },
            },
        });
        Assert.Equal(HttpStatusCode.Created, variants.StatusCode);
        return await FinishAsync(admin, product.Id, image);
    }

    private static async Task<ProductResponse> CreateShellAsync(HttpClient admin, Guid categoryId, string name, string slug, Guid? collectionId)
    {
        var created = await CatalogHttp.PostAsync(admin, "/api/v1/admin/products", new
        {
            name,
            slug,
            shortDescription = $"{name} short",
            description = $"{name} description for search.",
            categoryIds = new[] { categoryId },
            collectionIds = collectionId is Guid id ? new[] { id } : null,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private static async Task<ProductResponse> FinishAsync(HttpClient admin, Guid productId, byte[]? image = null)
    {
        var upload = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{productId}/media", image ?? CatalogHttp.Png, "Front");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var current = (await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{productId}"))!;
        var published = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{productId}/publish", new { expectedVersion = current.Version });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        return (await published.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient admin, string path, object body)
    {
        await AuthHttp.PrepareAsync(admin);
        return await admin.PutAsJsonAsync(path, body);
    }

    private static byte[] WidePng()
    {
        const int width = 500;
        const int height = 8;
        var raw = new byte[(width * 3 + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * (width * 3 + 1);
            for (var x = 0; x < width; x++)
            {
                raw[row + 1 + (x * 3)] = 20;
                raw[row + 2 + (x * 3)] = 30;
                raw[row + 3 + (x * 3)] = 40;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        var data = compressed.ToArray();
        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        WriteChunk(png, "IHDR",
        [
            0, 0, 1, 244, 0, 0, 0, 8, 8, 2, 0, 0, 0,
        ]);
        WriteChunk(png, "IDAT", data);
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var header = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, header);
        Buffer.BlockCopy(data, 0, header, 4, data.Length);
        var length = new byte[] { (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length };
        stream.Write(length);
        stream.Write(header);
        var crc = Crc32(header);
        stream.Write([(byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc]);
    }

    private static uint Crc32(byte[] content)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in content)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                var mask = (crc & 1) == 1 ? 0xEDB88320u : 0;
                crc = (crc >> 1) ^ mask;
            }
        }

        return ~crc;
    }
}
