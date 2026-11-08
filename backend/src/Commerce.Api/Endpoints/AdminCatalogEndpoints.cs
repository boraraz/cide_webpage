using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Commerce.Api.Auth;
using Commerce.Api.Catalog;
using Commerce.Application.Catalog;
using Commerce.Domain.Catalog;
using Commerce.Domain.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace Commerce.Api.Endpoints;

public static class AdminCatalogEndpoints
{
    public static IEndpointRouteBuilder MapAdminCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var categories = app.MapGroup("/api/v1/admin/categories").WithTags("Admin catalog").RequireAuthorization(RoleNames.Admin);
        categories.MapGet("/", async (ICatalogReader reader, CancellationToken cancellationToken) =>
            Results.Ok(await reader.CategoryTreeAsync(activeOnly: false, cancellationToken)))
            .WithName("ListAdminCategories")
            .Produces<CategoryNode[]>();
        categories.MapPost("/", CreateCategoryAsync).AddEndpointFilter<RequireAntiforgery>().WithName("CreateCategory")
            .Produces<CategoryResponse>(StatusCodes.Status201Created).ProducesValidationProblem();
        categories.MapPatch("/{id:guid}", UpdateCategoryAsync).AddEndpointFilter<RequireAntiforgery>().WithName("UpdateCategory")
            .Produces<CategoryResponse>().ProducesValidationProblem();
        categories.MapDelete("/{id:guid}", DeleteCategoryAsync).AddEndpointFilter<RequireAntiforgery>().WithName("DeleteCategory")
            .Produces(StatusCodes.Status204NoContent);

        var collections = app.MapGroup("/api/v1/admin/collections").WithTags("Admin catalog").RequireAuthorization(RoleNames.Admin);
        collections.MapGet("/", async (ICatalogReader reader, CancellationToken cancellationToken) =>
            Results.Ok(await reader.CollectionsAsync(activeOnly: false, cancellationToken)))
            .WithName("ListAdminCollections")
            .Produces<CollectionResponse[]>();
        collections.MapPost("/", CreateCollectionAsync).AddEndpointFilter<RequireAntiforgery>().WithName("CreateCollection")
            .Produces<CollectionResponse>(StatusCodes.Status201Created).ProducesValidationProblem();
        collections.MapPatch("/{id:guid}", UpdateCollectionAsync).AddEndpointFilter<RequireAntiforgery>().WithName("UpdateCollection")
            .Produces<CollectionResponse>().ProducesValidationProblem();
        collections.MapDelete("/{id:guid}", DeleteCollectionAsync).AddEndpointFilter<RequireAntiforgery>().WithName("DeleteCollection")
            .Produces(StatusCodes.Status204NoContent);

        var products = app.MapGroup("/api/v1/admin/products").WithTags("Admin catalog").RequireAuthorization(RoleNames.Admin);
        products.MapGet("/", ListProductsAsync).WithName("ListAdminProducts").Produces<AdminProductPage>();
        products.MapGet("/{id:guid}", async (Guid id, ICatalogReader reader, CancellationToken cancellationToken) =>
        {
            var product = await reader.AdminProductAsync(id, cancellationToken);
            return product is null ? Results.NotFound() : Results.Ok(product);
        }).WithName("GetAdminProduct").Produces<ProductResponse>().Produces(StatusCodes.Status404NotFound);
        products.MapPost("/", CreateProductAsync).AddEndpointFilter<RequireAntiforgery>().WithName("CreateProduct")
            .Produces<ProductResponse>(StatusCodes.Status201Created).ProducesValidationProblem();
        products.MapPatch("/{id:guid}", UpdateProductAsync).AddEndpointFilter<RequireAntiforgery>().WithName("UpdateProduct")
            .Produces<ProductResponse>().ProducesValidationProblem();
        products.MapPost("/{id:guid}/publish", PublishAsync).AddEndpointFilter<RequireAntiforgery>().WithName("PublishProduct")
            .Produces<ProductResponse>().ProducesValidationProblem();
        products.MapPost("/{id:guid}/archive", ArchiveAsync).AddEndpointFilter<RequireAntiforgery>().WithName("ArchiveProduct")
            .Produces<ProductResponse>();
        products.MapPost("/{id:guid}/options", AddOptionAsync).AddEndpointFilter<RequireAntiforgery>().WithName("CreateProductOption")
            .Produces<OptionResponse>(StatusCodes.Status201Created);
        products.MapPost("/{id:guid}/variants", AddVariantAsync).AddEndpointFilter<RequireAntiforgery>().WithName("CreateProductVariant")
            .Produces<VariantResponse>(StatusCodes.Status201Created).ProducesValidationProblem();
        products.MapPost("/{id:guid}/media", UploadMediaAsync)
            .AddEndpointFilter<RequireAntiforgery>()
            .RequireRateLimiting(CatalogRateLimits.MediaUpload)
            .WithName("UploadProductMedia")
            .Produces<MediaResponse>(StatusCodes.Status201Created)
            .DisableAntiforgery();

        app.MapPatch("/api/v1/admin/options/{id:guid}", UpdateOptionAsync)
            .WithTags("Admin catalog").RequireAuthorization(RoleNames.Admin).AddEndpointFilter<RequireAntiforgery>().WithName("UpdateProductOption")
            .Produces<OptionResponse>();
        app.MapDelete("/api/v1/admin/options/{id:guid}", DeleteOptionAsync)
            .WithTags("Admin catalog").RequireAuthorization(RoleNames.Admin).AddEndpointFilter<RequireAntiforgery>().WithName("DeleteProductOption")
            .Produces(StatusCodes.Status204NoContent);
        app.MapPost("/api/v1/admin/options/{id:guid}/values", AddOptionValueAsync)
            .WithTags("Admin catalog").RequireAuthorization(RoleNames.Admin).AddEndpointFilter<RequireAntiforgery>().WithName("CreateOptionValue")
            .Produces<OptionValueResponse>(StatusCodes.Status201Created);
        app.MapDelete("/api/v1/admin/option-values/{id:guid}", DeleteOptionValueAsync)
            .WithTags("Admin catalog").RequireAuthorization(RoleNames.Admin).AddEndpointFilter<RequireAntiforgery>().WithName("DeleteOptionValue")
            .Produces(StatusCodes.Status204NoContent);
        app.MapPatch("/api/v1/admin/variants/{id:guid}", UpdateVariantAsync)
            .WithTags("Admin catalog").RequireAuthorization(RoleNames.Admin).AddEndpointFilter<RequireAntiforgery>().WithName("UpdateProductVariant")
            .Produces<VariantResponse>().ProducesValidationProblem();
        app.MapDelete("/api/v1/admin/variants/{id:guid}", DeleteVariantAsync)
            .WithTags("Admin catalog").RequireAuthorization(RoleNames.Admin).AddEndpointFilter<RequireAntiforgery>().WithName("DeleteProductVariant")
            .Produces(StatusCodes.Status204NoContent);

        var inventory = app.MapGroup("/api/v1/admin/inventory").WithTags("Admin inventory").RequireAuthorization(RoleNames.Admin);
        inventory.MapGet("/", ListInventoryAsync).WithName("ListInventory").Produces<InventoryPage>();
        inventory.MapGet("/{variantId:guid}/movements", MovementsAsync).WithName("ListInventoryMovements")
            .Produces<MovementResponse[]>();
        inventory.MapPost("/{variantId:guid}/adjustments", AdjustAsync).AddEndpointFilter<RequireAntiforgery>().WithName("AdjustInventory")
            .Produces<MovementResponse>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);

        var media = app.MapGroup("/api/v1/admin/media").WithTags("Admin catalog").RequireAuthorization(RoleNames.Admin);
        media.MapGet("/{id:guid}", OpenMediaAsync).WithName("GetAdminMedia");
        media.MapPatch("/{id:guid}", UpdateMediaAsync).AddEndpointFilter<RequireAntiforgery>().WithName("UpdateProductMedia")
            .Produces<MediaResponse>();
        media.MapDelete("/{id:guid}", DeleteMediaAsync).AddEndpointFilter<RequireAntiforgery>().WithName("DeleteProductMedia")
            .Produces(StatusCodes.Status204NoContent);
        return app;
    }

    private static async Task<IResult> CreateCategoryAsync(CategoryWriteRequest request, CategoryService categories, CancellationToken cancellationToken) =>
        CatalogHttp.From(
            await categories.CreateAsync(request.Name, request.Slug, request.Description, request.ParentCategoryId, request.DisplayOrder, request.IsActive, cancellationToken),
            value => Results.Created($"/api/v1/admin/categories/{value.Id}", value));

    private static async Task<IResult> UpdateCategoryAsync(Guid id, CategoryUpdateRequest request, CategoryService categories, CancellationToken cancellationToken) =>
        CatalogHttp.From(
            await categories.UpdateAsync(
                id,
                request.Name,
                request.Slug,
                request.Description,
                request.ClearParent ? null : request.ParentCategoryId,
                request.ClearParent || request.ParentCategoryId is not null,
                request.DisplayOrder,
                request.IsActive,
                cancellationToken),
            Results.Ok);

    private static Task<IResult> DeleteCategoryAsync(Guid id, CategoryService categories, CancellationToken cancellationToken) =>
        DeleteAsync(() => categories.DeleteAsync(id, cancellationToken));

    private static async Task<IResult> CreateCollectionAsync(CollectionWriteRequest request, CollectionService collections, CancellationToken cancellationToken) =>
        CatalogHttp.From(
            await collections.CreateAsync(request.Name, request.Slug, request.Description, request.DisplayOrder, request.IsActive, cancellationToken),
            value => Results.Created($"/api/v1/admin/collections/{value.Id}", value));

    private static async Task<IResult> UpdateCollectionAsync(Guid id, CollectionUpdateRequest request, CollectionService collections, CancellationToken cancellationToken) =>
        CatalogHttp.From(
            await collections.UpdateAsync(id, request.Name, request.Slug, request.Description, request.DisplayOrder, request.IsActive, cancellationToken),
            Results.Ok);

    private static Task<IResult> DeleteCollectionAsync(Guid id, CollectionService collections, CancellationToken cancellationToken) =>
        DeleteAsync(() => collections.DeleteAsync(id, cancellationToken));

    private static async Task<IResult> ListProductsAsync(ICatalogReader reader, int? page, int? pageSize, string? status, CancellationToken cancellationToken)
    {
        if (!TryPage(page, pageSize, out var resolvedPage, out var resolvedSize, out var error))
        {
            return error!;
        }

        return Results.Ok(await reader.AdminProductsAsync(resolvedPage, resolvedSize, status, cancellationToken));
    }

    private static async Task<IResult> CreateProductAsync(ProductWriteRequest request, ProductService products, CancellationToken cancellationToken) =>
        CatalogHttp.From(
            await products.CreateAsync(request.Name, request.Slug, request.ShortDescription, request.Description, request.Brand, request.IsFeatured, request.CategoryIds, request.CollectionIds, cancellationToken),
            value => Results.Created($"/api/v1/admin/products/{value.Id}", value));

    private static async Task<IResult> UpdateProductAsync(Guid id, ProductUpdateRequest request, ProductService products, CancellationToken cancellationToken) =>
        CatalogHttp.From(
            await products.UpdateAsync(id, request.Name, request.Slug, request.ShortDescription, request.Description, request.Brand, request.IsFeatured, request.CategoryIds, request.CollectionIds, cancellationToken),
            Results.Ok);

    private static async Task<IResult> PublishAsync(Guid id, ProductService products, CancellationToken cancellationToken) =>
        CatalogHttp.From(await products.PublishAsync(id, cancellationToken), Results.Ok);

    private static async Task<IResult> ArchiveAsync(Guid id, ProductService products, CancellationToken cancellationToken) =>
        CatalogHttp.From(await products.ArchiveAsync(id, cancellationToken), Results.Ok);

    private static async Task<IResult> AddOptionAsync(Guid id, OptionWriteRequest request, ProductService products, CancellationToken cancellationToken) =>
        CatalogHttp.From(await products.AddOptionAsync(id, request.Name, request.Position, cancellationToken), value => Results.Created($"/api/v1/admin/options/{value.Id}", value));

    private static async Task<IResult> UpdateOptionAsync(Guid id, OptionUpdateRequest request, ProductService products, CancellationToken cancellationToken) =>
        CatalogHttp.From(await products.UpdateOptionAsync(id, request.Name, request.Position, cancellationToken), Results.Ok);

    private static Task<IResult> DeleteOptionAsync(Guid id, ProductService products, CancellationToken cancellationToken) =>
        DeleteAsync(() => products.DeleteOptionAsync(id, cancellationToken));

    private static async Task<IResult> AddOptionValueAsync(Guid id, OptionValueWriteRequest request, ProductService products, CancellationToken cancellationToken) =>
        CatalogHttp.From(
            await products.AddOptionValueAsync(id, request.Value, request.Code, request.Position, cancellationToken),
            value => Results.Created($"/api/v1/admin/option-values/{value.Id}", value));

    private static Task<IResult> DeleteOptionValueAsync(Guid id, ProductService products, CancellationToken cancellationToken) =>
        DeleteAsync(() => products.DeleteOptionValueAsync(id, cancellationToken));

    private static async Task<IResult> AddVariantAsync(
        Guid id,
        VariantWriteRequest request,
        ProductService products,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!TryPrices(request.Price, request.CompareAtPrice, out var price, out var compareAt, out var error))
        {
            return error!;
        }

        return CatalogHttp.From(
            await products.AddVariantAsync(id, request.Sku, request.Barcode, price, compareAt, request.IsActive, request.OptionValueIds ?? [], request.InitialQuantity, Actor(user), cancellationToken),
            value => Results.Created($"/api/v1/admin/variants/{value.Id}", value));
    }

    private static async Task<IResult> UpdateVariantAsync(Guid id, VariantUpdateRequest request, ProductService products, CancellationToken cancellationToken)
    {
        decimal? price = null;
        if (request.Price is not null)
        {
            if (!Money.TryParse(request.Price, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["price"] = ["Enter a valid price."] });
            }

            price = parsed;
        }

        decimal? compareAt = null;
        var compareSpecified = request.ClearCompareAtPrice || request.CompareAtPrice is not null;
        if (request.CompareAtPrice is not null)
        {
            if (!Money.TryParse(request.CompareAtPrice, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["compareAtPrice"] = ["Enter a valid compare-at price."] });
            }

            compareAt = parsed;
        }

        return CatalogHttp.From(
            await products.UpdateVariantAsync(id, request.Sku, request.Barcode, price, compareAt, compareSpecified, request.IsActive, request.OptionValueIds, cancellationToken),
            Results.Ok);
    }

    private static Task<IResult> DeleteVariantAsync(Guid id, ProductService products, CancellationToken cancellationToken) =>
        DeleteAsync(() => products.DeleteVariantAsync(id, cancellationToken));

    private static async Task<IResult> ListInventoryAsync(ICatalogReader reader, int? page, int? pageSize, string? sku, CancellationToken cancellationToken)
    {
        if (!TryPage(page, pageSize, out var resolvedPage, out var resolvedSize, out var error))
        {
            return error!;
        }

        return Results.Ok(await reader.InventoryAsync(resolvedPage, resolvedSize, sku, cancellationToken));
    }

    private static async Task<IResult> MovementsAsync(Guid variantId, ICatalogReader reader, CancellationToken cancellationToken)
    {
        var movements = await reader.MovementsAsync(variantId, cancellationToken);
        return movements is null ? Results.NotFound() : Results.Ok(movements);
    }

    private static async Task<IResult> AdjustAsync(
        Guid variantId,
        StockAdjustmentRequest request,
        HttpRequest http,
        InventoryService inventory,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        http.Headers.TryGetValue("Idempotency-Key", out var key);
        return CatalogHttp.From(
            await inventory.AdjustAsync(variantId, Actor(user), key.FirstOrDefault(), request.MovementType, request.Quantity, request.Note, cancellationToken),
            Results.Ok);
    }

    private static async Task<IResult> UploadMediaAsync(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string altText,
        [FromForm] Guid? variantId,
        [FromForm] int? displayOrder,
        [FromForm] bool? isPrimary,
        MediaService media,
        CancellationToken cancellationToken)
    {
        if (file.Length > ImageSignature.MaximumBytes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Upload a JPEG, PNG or WebP image up to 5 MB."] });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return CatalogHttp.From(
            await media.UploadAsync(id, buffer.ToArray(), altText, variantId, displayOrder, isPrimary ?? false, cancellationToken),
            value => Results.Created(value.Path, value));
    }

    private static async Task<IResult> OpenMediaAsync(Guid id, MediaService media, CancellationToken cancellationToken)
    {
        var stored = await media.OpenAdminAsync(id, cancellationToken);
        return stored is null ? Results.NotFound() : Results.File(stored.Content, stored.ContentType);
    }

    private static async Task<IResult> UpdateMediaAsync(Guid id, MediaUpdateRequest request, MediaService media, CancellationToken cancellationToken) =>
        CatalogHttp.From(
            await media.UpdateAsync(id, request.AltText, request.DisplayOrder, request.IsPrimary, request.ClearVariant ? null : request.VariantId, request.ClearVariant || request.VariantId is not null, cancellationToken),
            Results.Ok);

    private static Task<IResult> DeleteMediaAsync(Guid id, MediaService media, CancellationToken cancellationToken) =>
        DeleteAsync(() => media.DeleteAsync(id, cancellationToken));

    private static async Task<IResult> DeleteAsync(Func<Task<CatalogResult<bool>>> action)
    {
        var result = await action();
        return result.Failure is null ? Results.NoContent() : CatalogHttp.From(result, _ => Results.NoContent());
    }

    private static bool TryPage(int? page, int? pageSize, out int resolvedPage, out int resolvedSize, out IResult? error)
    {
        resolvedPage = page ?? 1;
        resolvedSize = pageSize ?? 24;
        if (resolvedPage < 1 || resolvedSize is < 1 or > 48)
        {
            error = Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = ["Page must be at least 1 and page size must be from 1 to 48."],
            });
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryPrices(string priceText, string? compareAtText, out decimal price, out decimal? compareAt, out IResult? error)
    {
        compareAt = null;
        if (!Money.TryParse(priceText, out price))
        {
            error = Results.ValidationProblem(new Dictionary<string, string[]> { ["price"] = ["Enter a valid price."] });
            return false;
        }

        if (compareAtText is not null)
        {
            if (!Money.TryParse(compareAtText, out var parsed))
            {
                error = Results.ValidationProblem(new Dictionary<string, string[]> { ["compareAtPrice"] = ["Enter a valid compare-at price."] });
                return false;
            }

            compareAt = parsed;
        }

        error = null;
        return true;
    }

    private static Guid Actor(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
