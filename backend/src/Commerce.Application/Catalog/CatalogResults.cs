namespace Commerce.Application.Catalog;

public sealed class CatalogConflictException(string field, string message, int status = 409) : Exception(message)
{
    public string Field { get; } = field;

    public int Status { get; } = status;
}

public sealed record CatalogFailure(
    int Status,
    string Title,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record CatalogResult<T>(T? Value, CatalogFailure? Failure)
{
    public bool Succeeded => Failure is null;

    public static CatalogResult<T> Ok(T value) => new(value, null);

    public static CatalogResult<T> Fail(CatalogFailure failure) => new(default, failure);

    public static CatalogResult<T> Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        Fail(new CatalogFailure(400, "One or more validation errors occurred.", Errors: errors));

    public static CatalogResult<T> NotFound() =>
        Fail(new CatalogFailure(404, "The resource was not found."));

    public static CatalogResult<T> Conflict(string field, string message, int status = 409) =>
        Fail(new CatalogFailure(status, message, Errors: new Dictionary<string, string[]> { [field] = [message] }));
}
