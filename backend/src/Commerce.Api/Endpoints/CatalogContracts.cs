using Commerce.Application.Catalog;

namespace Commerce.Api.Endpoints;

internal static class CatalogHttp
{
    public static IResult From<T>(CatalogResult<T> result, Func<T, IResult> success) =>
        result.Failure is null
            ? success(result.Value!)
            : result.Failure.Errors is null
                ? Results.Problem(title: result.Failure.Title, detail: result.Failure.Detail, statusCode: result.Failure.Status)
                : Results.ValidationProblem(result.Failure.Errors, title: result.Failure.Title, statusCode: result.Failure.Status);
}

internal sealed record CategoryWriteRequest(
    string Name,
    string? Slug,
    string? Description,
    Guid? ParentCategoryId,
    int DisplayOrder = 0,
    bool IsActive = true);

internal sealed record CategoryUpdateRequest(
    string? Name,
    string? Slug,
    string? Description,
    Guid? ParentCategoryId,
    bool ClearParent = false,
    int? DisplayOrder = null,
    bool? IsActive = null);

internal sealed record CollectionWriteRequest(
    string Name,
    string? Slug,
    string? Description,
    int DisplayOrder = 0,
    bool IsActive = true);

internal sealed record CollectionUpdateRequest(
    string? Name,
    string? Slug,
    string? Description,
    int? DisplayOrder,
    bool? IsActive);

internal sealed record ProductWriteRequest(
    string Name,
    string? Slug,
    string? ShortDescription,
    string? Description,
    string? Brand,
    bool IsFeatured = false,
    Guid[]? CategoryIds = null,
    Guid[]? CollectionIds = null);

internal sealed record ProductUpdateRequest(
    string? Name,
    string? Slug,
    string? ShortDescription,
    string? Description,
    string? Brand,
    bool? IsFeatured,
    Guid[]? CategoryIds,
    Guid[]? CollectionIds);

internal sealed record OptionWriteRequest(string Name, int Position = 0);

internal sealed record OptionUpdateRequest(string? Name, int? Position);

internal sealed record OptionValueWriteRequest(string Value, string? Code, int Position = 0);

internal sealed record VariantWriteRequest(
    string Sku,
    string? Barcode,
    string Price,
    string? CompareAtPrice,
    bool IsActive = true,
    Guid[]? OptionValueIds = null,
    int InitialQuantity = 0);

internal sealed record VariantUpdateRequest(
    string? Sku,
    string? Barcode,
    string? Price,
    string? CompareAtPrice,
    bool ClearCompareAtPrice = false,
    bool? IsActive = null,
    Guid[]? OptionValueIds = null);

internal sealed record StockAdjustmentRequest(string MovementType, int Quantity, string? Note);

internal sealed record MediaUpdateRequest(
    string? AltText,
    int? DisplayOrder,
    bool? IsPrimary,
    Guid? VariantId,
    bool ClearVariant = false);
