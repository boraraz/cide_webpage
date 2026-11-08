using System.Net;
using System.Net.Http.Json;
using Commerce.Application.Catalog;
using Commerce.IntegrationTests.Identity;

namespace Commerce.IntegrationTests.Catalog;

[Collection(CatalogCollection.Name)]
public sealed class CatalogEndpointTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task Categories_reject_cycles_and_duplicate_slugs()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        var parent = await CreateCategoryAsync(admin, "Kadın Üst", "kadin-ust");
        var child = await CreateCategoryAsync(admin, "Tisort", null, parent.Id);

        var duplicate = await CatalogHttp.PostAsync(admin, "/api/v1/admin/categories", new { name = "Other", slug = "kadin-ust" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var cycle = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/categories/{parent.Id}", new { parentCategoryId = child.Id });
        Assert.Equal(HttpStatusCode.Conflict, cycle.StatusCode);

        var self = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/categories/{parent.Id}", new { parentCategoryId = parent.Id });
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);

        var blocked = await CatalogHttp.DeleteAsync(admin, $"/api/v1/admin/categories/{parent.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    }

    [Fact]
    public async Task Drafts_are_hidden_until_published_and_archived_products_disappear()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        using var anonymous = factory.CreateClient();
        var category = await CreateCategoryAsync(admin, "Men", null);
        var product = await CreateProductAsync(admin, "Draft Tee", category.Id);

        var hidden = await anonymous.GetAsync($"/api/v1/products/{product.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var premature = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/publish", new { });
        Assert.Equal(HttpStatusCode.BadRequest, premature.StatusCode);

        var color = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/options", new { name = "Color", position = 1 });
        var colorBody = await color.Content.ReadFromJsonAsync<OptionResponse>();
        Assert.Equal(HttpStatusCode.Created, color.StatusCode);
        var black = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/options/{colorBody!.Id}/values", new { value = "Black", code = "#111111", position = 1 });
        var blackBody = await black.Content.ReadFromJsonAsync<OptionValueResponse>();
        var size = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/options", new { name = "Size", position = 2 });
        var sizeBody = await size.Content.ReadFromJsonAsync<OptionResponse>();
        var small = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/options/{sizeBody!.Id}/values", new { value = "S", position = 1 });
        var smallBody = await small.Content.ReadFromJsonAsync<OptionValueResponse>();

        var variant = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants", new
        {
            sku = $"TEE-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            price = "899.00",
            compareAtPrice = "899.00",
            optionValueIds = new[] { blackBody!.Id, smallBody!.Id },
            initialQuantity = 3,
        });
        Assert.Equal(HttpStatusCode.BadRequest, variant.StatusCode);

        var sku = $"TEE-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        variant = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants", new
        {
            sku,
            price = "899.00",
            compareAtPrice = "1199.00",
            optionValueIds = new[] { blackBody.Id, smallBody.Id },
            initialQuantity = 3,
        });
        Assert.Equal(HttpStatusCode.Created, variant.StatusCode);
        var duplicateSku = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants", new
        {
            sku,
            price = "899.00",
            optionValueIds = new[] { blackBody.Id, smallBody.Id },
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateSku.StatusCode);

        var duplicateCombination = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants", new
        {
            sku = $"TEE-{Guid.NewGuid():N}"[..14].ToUpperInvariant(),
            price = "910.00",
            optionValueIds = new[] { smallBody.Id, blackBody.Id },
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateCombination.StatusCode);

        var invalidPrice = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants", new
        {
            sku = $"TEE-{Guid.NewGuid():N}"[..14].ToUpperInvariant(),
            price = "-1.00",
            optionValueIds = new[] { blackBody.Id },
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPrice.StatusCode);

        var upload = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{product.Id}/media", CatalogHttp.Png, "Black tee");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);

        var published = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var visible = await anonymous.GetAsync($"/api/v1/products/{product.Slug}");
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);

        var archived = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/archive", new { });
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        var gone = await anonymous.GetAsync($"/api/v1/products/{product.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Public_catalog_filters_sorts_and_pages_active_products()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        using var anonymous = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tops = await CreateCategoryAsync(admin, $"Tops {suffix}", $"tops-{suffix}");
        var child = await CreateCategoryAsync(admin, $"Tees {suffix}", $"tees-{suffix}", tops.Id);
        var collection = await CreateCollectionAsync(admin, $"Arrivals {suffix}", $"arrivals-{suffix}");

        var cheap = await PublishSimpleProductAsync(admin, child.Id, collection.Id, $"Alpha {suffix}", $"alpha-{suffix}", "100.00", "BLK", 5);
        var pricey = await PublishSimpleProductAsync(admin, child.Id, collection.Id, $"Zebra {suffix}", $"zebra-{suffix}", "500.00", "WHT", 0);

        var page = await anonymous.GetFromJsonAsync<ProductPage>(
            $"/api/v1/products?category=tees-{suffix}&collection=arrivals-{suffix}&sort=price_asc&pageSize=1&page=1&q={suffix}");
        Assert.NotNull(page);
        Assert.Equal(2, page.Total);
        Assert.Equal(cheap.Slug, page.Items[0].Slug);
        Assert.True(page.Items[0].Available);

        var second = await anonymous.GetFromJsonAsync<ProductPage>(
            $"/api/v1/products?category=tops-{suffix}&sort=price_desc&pageSize=1&page=1&q={suffix}");
        Assert.Equal(pricey.Slug, second!.Items[0].Slug);
        Assert.False(second.Items[0].Available);

        var colored = await anonymous.GetFromJsonAsync<ProductPage>($"/api/v1/products?color=black&q={suffix}");
        Assert.Contains(colored!.Items, item => item.Slug == cheap.Slug);

        var sized = await anonymous.GetFromJsonAsync<ProductPage>($"/api/v1/products?size=m&minPrice=400&maxPrice=600&q={suffix}");
        Assert.Contains(sized!.Items, item => item.Slug == pricey.Slug);

        var oldSlug = cheap.Slug;
        var renamed = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/products/{cheap.Id}", new { slug = $"renamed-{suffix}" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var historical = await anonymous.GetAsync($"/api/v1/products/{oldSlug}");
        var historicalBody = await historical.Content.ReadFromJsonAsync<PublicProductDetail>();
        Assert.Equal(HttpStatusCode.OK, historical.StatusCode);
        Assert.Equal($"renamed-{suffix}", historicalBody!.Slug);

        var blockedCategory = await CatalogHttp.DeleteAsync(admin, $"/api/v1/admin/categories/{child.Id}");
        var blockedCollection = await CatalogHttp.DeleteAsync(admin, $"/api/v1/admin/collections/{collection.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blockedCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, blockedCollection.StatusCode);
    }

    [Fact]
    public async Task Inventory_adjustments_are_atomic_and_idempotent()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        var category = await CreateCategoryAsync(admin, "Stock", null);
        var product = await PublishSimpleProductAsync(admin, category.Id, null, "Stock Tee", null, "250.00", "NVY", 1);
        var detail = await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}");
        var variantId = detail!.Variants[0].Id;

        var key = CatalogHttp.Token();
        var first = await AdjustAsync(admin, variantId, key, "manualIncrease", 2);
        var replay = await AdjustAsync(admin, variantId, key, "manualIncrease", 2);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var conflict = await AdjustAsync(admin, variantId, key, "manualDecrease", 1);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var after = await admin.GetFromJsonAsync<ProductResponse>($"/api/v1/admin/products/{product.Id}");
        Assert.Equal(3, after!.Variants[0].OnHand);

        var down = await AdjustAsync(admin, variantId, CatalogHttp.Token(), "manualDecrease", 2);
        Assert.Equal(HttpStatusCode.OK, down.StatusCode);

        using var other = await CatalogHttp.AdminClientAsync(factory);
        var left = AdjustAsync(admin, variantId, CatalogHttp.Token(), "manualDecrease", 1);
        var right = AdjustAsync(other, variantId, CatalogHttp.Token(), "manualDecrease", 1);
        var results = await Task.WhenAll(left, right);
        Assert.Equal(1, results.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(response => response.StatusCode == HttpStatusCode.Conflict));

        var tooMuch = await AdjustAsync(admin, variantId, CatalogHttp.Token(), "correction", 1);
        Assert.Equal(HttpStatusCode.Conflict, tooMuch.StatusCode);

        var movements = await admin.GetFromJsonAsync<MovementResponse[]>($"/api/v1/admin/inventory/{variantId}/movements");
        Assert.Contains(movements!, movement => movement.MovementType == "initialstock");
        Assert.Contains(movements!, movement => movement.MovementType == "manualincrease");
    }

    [Fact]
    public async Task Media_rejects_invalid_files_and_keeps_one_primary_image()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        var category = await CreateCategoryAsync(admin, "Media", null);
        var product = await CreateProductAsync(admin, "Media Tee", category.Id);

        var text = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{product.Id}/media", "not-an-image-file"u8.ToArray(), "Nope");
        Assert.Equal(HttpStatusCode.BadRequest, text.StatusCode);

        var oversized = new byte[(5 * 1024 * 1024) + 1];
        CatalogHttp.Png.CopyTo(oversized, 0);
        var huge = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{product.Id}/media", oversized, "Too big");
        Assert.Equal(HttpStatusCode.BadRequest, huge.StatusCode);

        var first = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{product.Id}/media", CatalogHttp.Png, "Front");
        var second = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{product.Id}/media", CatalogHttp.Png, "Back");
        Assert.True(first.StatusCode == HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<MediaResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<MediaResponse>();
        Assert.True(firstBody!.IsPrimary);
        Assert.False(secondBody!.IsPrimary);
        Assert.True(factory.Storage.Objects.ContainsKey($"catalog/{product.Id}/{firstBody.Id}.png"));

        var promoted = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/media/{secondBody.Id}", new { isPrimary = true, displayOrder = 0 });
        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        var image = await admin.GetAsync($"/api/v1/admin/media/{firstBody.Id}");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Customers_cannot_change_the_catalog()
    {
        using var admin = await CatalogHttp.AdminClientAsync(factory);
        using var customer = factory.CreateClient();
        var email = AuthHttp.EmailAddress();
        var registered = await AuthHttp.RegisterAsync(customer, email);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        var forbidden = await CatalogHttp.PostAsync(customer, "/api/v1/admin/categories", new { name = "Nope" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var anonymous = factory.CreateClient();
        var missing = await anonymous.PostAsJsonAsync("/api/v1/admin/categories", new { name = "Nope" });
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var category = await CreateCategoryAsync(admin, "Visible", null);
        var tree = await anonymous.GetAsync("/api/v1/categories");
        Assert.Equal(HttpStatusCode.OK, tree.StatusCode);
        Assert.NotEqual(Guid.Empty, category.Id);
    }

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient admin, string name, string? slug, Guid? parentId = null)
    {
        var response = await CatalogHttp.PostAsync(admin, "/api/v1/admin/categories", new { name, slug, parentCategoryId = parentId, displayOrder = 1, isActive = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    private static async Task<CollectionResponse> CreateCollectionAsync(HttpClient admin, string name, string slug)
    {
        var response = await CatalogHttp.PostAsync(admin, "/api/v1/admin/collections", new { name, slug, isActive = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CollectionResponse>())!;
    }

    private static async Task<ProductResponse> CreateProductAsync(HttpClient admin, string name, Guid categoryId)
    {
        var response = await CatalogHttp.PostAsync(admin, "/api/v1/admin/products", new
        {
            name,
            shortDescription = "Short",
            description = "A long enough description.",
            categoryIds = new[] { categoryId },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private static async Task<ProductResponse> PublishSimpleProductAsync(
        HttpClient admin,
        Guid categoryId,
        Guid? collectionId,
        string name,
        string? slug,
        string price,
        string color,
        int stock)
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
        var product = (await created.Content.ReadFromJsonAsync<ProductResponse>())!;
        var colorOption = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/options", new { name = "Color" });
        var colorBody = (await colorOption.Content.ReadFromJsonAsync<OptionResponse>())!;
        var colorValue = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/options/{colorBody.Id}/values", new { value = color == "BLK" ? "Black" : color == "WHT" ? "White" : "Navy" });
        var colorValueBody = (await colorValue.Content.ReadFromJsonAsync<OptionValueResponse>())!;
        var sizeOption = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/options", new { name = "Size", position = 2 });
        var sizeBody = (await sizeOption.Content.ReadFromJsonAsync<OptionResponse>())!;
        var sizeValue = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/options/{sizeBody.Id}/values", new { value = "M" });
        var sizeValueBody = (await sizeValue.Content.ReadFromJsonAsync<OptionValueResponse>())!;
        var sku = $"{color}-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var variant = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/variants", new
        {
            sku,
            price,
            optionValueIds = new[] { colorValueBody.Id, sizeValueBody.Id },
            initialQuantity = stock,
        });
        Assert.Equal(HttpStatusCode.Created, variant.StatusCode);
        var upload = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{product.Id}/media", CatalogHttp.Png, $"{name} photo");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var published = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{product.Id}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        return (await published.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private static Task<HttpResponseMessage> AdjustAsync(HttpClient admin, Guid variantId, string key, string movementType, int quantity)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/inventory/{variantId}/adjustments")
        {
            Content = JsonContent.Create(new { movementType, quantity }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return SendAsync(admin, request);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient admin, HttpRequestMessage request)
    {
        await AuthHttp.PrepareAsync(admin);
        return await admin.SendAsync(request);
    }
}
