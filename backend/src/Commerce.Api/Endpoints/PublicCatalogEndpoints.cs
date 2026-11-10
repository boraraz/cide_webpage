using Commerce.Application.Catalog;
using Commerce.Domain.Catalog;

namespace Commerce.Api.Endpoints;

public static class PublicCatalogEndpoints
{
    public static IEndpointRouteBuilder MapPublicCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Catalog").AllowAnonymous();

        group.MapGet("/categories", async (ICatalogReader reader, CancellationToken cancellationToken) =>
                Results.Ok(await reader.CategoryTreeAsync(activeOnly: true, cancellationToken)))
            .WithName("ListPublicCategories")
            .WithSummary("Lists active categories as a tree.")
            .Produces<CategoryNode[]>();

        group.MapGet("/categories/{slug}", async (string slug, ICatalogReader reader, CancellationToken cancellationToken) =>
            {
                var category = await reader.CategoryBySlugAsync(slug, activeOnly: true, cancellationToken);
                return category is null ? Results.NotFound() : Results.Ok(category);
            })
            .WithName("GetPublicCategory")
            .Produces<CategoryNode>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/collections", async (ICatalogReader reader, CancellationToken cancellationToken) =>
                Results.Ok(await reader.CollectionsAsync(activeOnly: true, cancellationToken)))
            .WithName("ListPublicCollections")
            .Produces<CollectionResponse[]>();

        group.MapGet("/collections/{slug}", async (string slug, ICatalogReader reader, CancellationToken cancellationToken) =>
            {
                var collection = await reader.CollectionBySlugAsync(slug, activeOnly: true, cancellationToken);
                return collection is null ? Results.NotFound() : Results.Ok(collection);
            })
            .WithName("GetPublicCollection")
            .Produces<CollectionResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/products", ListProductsAsync)
            .WithName("ListPublicProducts")
            .WithSummary("Lists active products with filtering, search and stable paging.")
            .Produces<ProductPage>()
            .ProducesValidationProblem();

        group.MapGet("/products/{slug}", async (string slug, ICatalogReader reader, CancellationToken cancellationToken) =>
            {
                var product = await reader.PublicProductAsync(slug, cancellationToken);
                return product is null ? Results.NotFound() : Results.Ok(product);
            })
            .WithName("GetPublicProduct")
            .Produces<PublicProductDetail>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/media/{id:guid}", async (Guid id, MediaService media, CancellationToken cancellationToken) =>
            {
                var stored = await media.OpenPublicAsync(id, cancellationToken);
                return stored is null
                    ? Results.NotFound()
                    : MediaHttp.File(stored, published: true);
            })
            .WithName("GetPublicMedia");

        return app;
    }

    private static async Task<IResult> ListProductsAsync(
        ICatalogReader reader,
        int? page,
        int? pageSize,
        string? category,
        string? collection,
        string? color,
        string? size,
        string? minPrice,
        string? maxPrice,
        bool? available,
        string? q,
        string? sort,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page ?? 1;
        var resolvedSize = pageSize ?? 24;
        var errors = new Dictionary<string, string[]>();
        if (resolvedPage < 1 || resolvedSize is < 1 or > 48)
        {
            errors["page"] = ["Page must be at least 1 and page size must be from 1 to 48."];
        }

        decimal? minimum = null;
        decimal? maximum = null;
        if (minPrice is not null && !Money.TryParse(minPrice, out var parsedMin))
        {
            errors["minPrice"] = ["Enter a valid minimum price."];
        }
        else if (minPrice is not null)
        {
            minimum = Money.TryParse(minPrice, out var value) ? value : null;
        }

        if (maxPrice is not null && !Money.TryParse(maxPrice, out var parsedMax))
        {
            errors["maxPrice"] = ["Enter a valid maximum price."];
        }
        else if (maxPrice is not null && Money.TryParse(maxPrice, out parsedMax))
        {
            maximum = parsedMax;
        }

        if (minimum is decimal low && maximum is decimal high && low > high)
        {
            errors["minPrice"] = ["The minimum price cannot exceed the maximum price."];
        }

        var productSort = ProductSort.Newest;
        if (!string.IsNullOrWhiteSpace(sort))
        {
            productSort = sort switch
            {
                "newest" => ProductSort.Newest,
                "price_asc" => ProductSort.PriceAscending,
                "price_desc" => ProductSort.PriceDescending,
                _ => (ProductSort)(-1),
            };
            if (!Enum.IsDefined(productSort))
            {
                errors["sort"] = ["Sort must be newest, price_asc or price_desc."];
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await reader.PublicProductsAsync(
            new PublicProductQuery(
                resolvedPage,
                resolvedSize,
                category,
                collection,
                color,
                size,
                minimum,
                maximum,
                available ?? false,
                q,
                productSort),
            cancellationToken);
        if (result.CategoryMissing || result.CollectionMissing)
        {
            return Results.NotFound();
        }

        return Results.Ok(result.Page);
    }
}
