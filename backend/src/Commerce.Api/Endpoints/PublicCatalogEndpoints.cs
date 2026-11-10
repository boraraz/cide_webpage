using System.Net;
using System.Text;
using Commerce.Application.Catalog;
using Commerce.Domain.Catalog;
using Commerce.Infrastructure.Identity;
using Microsoft.Extensions.Options;

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

        group.MapGet("/products/filters", ListFiltersAsync)
            .WithName("ListPublicProductFilters")
            .Produces<PublicCatalogFilters>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/products/{slug}/related", async (string slug, int? limit, ICatalogReader reader, CancellationToken cancellationToken) =>
            {
                var resolved = limit ?? 8;
                if (resolved is < 1 or > 8)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["limit"] = ["Limit must be from 1 to 8."],
                    });
                }

                var related = await reader.RelatedProductsAsync(slug, resolved, cancellationToken);
                return related.Count == 0 && await reader.PublicProductAsync(slug, cancellationToken) is null
                    ? Results.NotFound()
                    : Results.Ok(related);
            })
            .WithName("ListRelatedProducts")
            .Produces<PublicProductCard[]>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapGet("/products/{slug}", async (string slug, ICatalogReader reader, CancellationToken cancellationToken) =>
            {
                var product = await reader.PublicProductAsync(slug, cancellationToken);
                return product is null ? Results.NotFound() : Results.Ok(product);
            })
            .WithName("GetPublicProduct")
            .Produces<PublicProductDetail>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/media/{id:guid}", async (Guid id, int? w, MediaService media, CancellationToken cancellationToken) =>
            {
                if (w is int requested && !MediaDerivativePaths.IsTarget(requested))
                {
                    return Results.NotFound();
                }

                var stored = await media.OpenPublicAsync(id, w, cancellationToken);
                return stored is null ? Results.NotFound() : MediaHttp.File(stored);
            })
            .WithName("GetPublicMedia");

        group.MapGet("/sitemap.xml", async (ICatalogReader reader, IOptions<PublicUrlOptions> urls, CancellationToken cancellationToken) =>
            {
                var sitemap = await reader.PublicSitemapAsync(cancellationToken);
                var origin = urls.Value.StorefrontUrl.TrimEnd('/');
                var body = new StringBuilder();
                body.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                body.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
                Append(body, origin, "/");
                Append(body, origin, "/shop");
                Append(body, origin, "/search");
                foreach (var slug in sitemap.Categories)
                {
                    Append(body, origin, $"/category/{Uri.EscapeDataString(slug)}");
                }

                foreach (var slug in sitemap.Collections)
                {
                    Append(body, origin, $"/collection/{Uri.EscapeDataString(slug)}");
                }

                foreach (var slug in sitemap.Products)
                {
                    Append(body, origin, $"/product/{Uri.EscapeDataString(slug)}");
                }

                body.Append("</urlset>");
                return Results.Text(body.ToString(), "application/xml", Encoding.UTF8);
            })
            .WithName("GetSitemap");

        return app;
    }

    private static void Append(StringBuilder body, string origin, string path) =>
        body.Append("<url><loc>").Append(WebUtility.HtmlEncode(origin + path)).Append("</loc></url>");

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
        string? priceBasis,
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
                "name" => ProductSort.Name,
                _ => (ProductSort)(-1),
            };
            if (!Enum.IsDefined(productSort))
            {
                errors["sort"] = ["Sort must be newest, price_asc, price_desc or name."];
            }
        }

        var basis = PriceBasis.Variant;
        if (!string.IsNullOrWhiteSpace(priceBasis))
        {
            basis = priceBasis switch
            {
                "variant" => PriceBasis.Variant,
                "from" => PriceBasis.From,
                _ => (PriceBasis)(-1),
            };
            if (!Enum.IsDefined(basis))
            {
                errors["priceBasis"] = ["Price basis must be variant or from."];
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
                productSort,
                basis),
            cancellationToken);
        if (result.CategoryMissing || result.CollectionMissing)
        {
            return Results.NotFound();
        }

        return Results.Ok(result.Page);
    }

    private static async Task<IResult> ListFiltersAsync(
        ICatalogReader reader,
        string? category,
        string? collection,
        string? q,
        string? priceBasis,
        CancellationToken cancellationToken)
    {
        var basis = PriceBasis.Variant;
        if (!string.IsNullOrWhiteSpace(priceBasis))
        {
            basis = priceBasis switch
            {
                "variant" => PriceBasis.Variant,
                "from" => PriceBasis.From,
                _ => (PriceBasis)(-1),
            };
            if (!Enum.IsDefined(basis))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["priceBasis"] = ["Price basis must be variant or from."],
                });
            }
        }

        var result = await reader.PublicFiltersAsync(
            new PublicProductQuery(1, 1, category, collection, null, null, null, null, false, q, ProductSort.Newest, basis),
            cancellationToken);
        if (result.CategoryMissing || result.CollectionMissing)
        {
            return Results.NotFound();
        }

        return Results.Ok(result.Filters);
    }
}
