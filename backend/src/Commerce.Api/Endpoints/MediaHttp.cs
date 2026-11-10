using Commerce.Application.Catalog;

namespace Commerce.Api.Endpoints;

internal static class MediaHttp
{
    public const string PublicCache = "public, max-age=86400, immutable";

    public const string PrivateCache = "private, no-store";

    public static IResult File(StoredObject stored, bool published) =>
        new CachedFileResult(stored.Content, stored.ContentType, published ? PublicCache : PrivateCache);

    public static IResult File(MediaDelivery delivery) =>
        new CachedFileResult(delivery.Object.Content, delivery.Object.ContentType, delivery.CacheControl);

    private sealed class CachedFileResult(byte[] content, string contentType, string cacheControl) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = contentType;
            httpContext.Response.ContentLength = content.Length;
            httpContext.Response.Headers["Cache-Control"] = cacheControl;
            return httpContext.Response.Body.WriteAsync(content).AsTask();
        }
    }
}
