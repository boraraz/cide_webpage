using Amazon.S3;
using Amazon.S3.Model;
using Commerce.Application.Catalog;
using Commerce.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Catalog;

internal sealed partial class S3ObjectStorage(
    IAmazonS3 client,
    IOptions<ObjectStorageOptions> options,
    ILogger<S3ObjectStorage> logger) : IObjectStorage
{
    public async Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
        }, cancellationToken);
    }

    public async Task<StoredObject?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetObjectAsync(options.Value.BucketName, key, cancellationToken);
            await using var body = response.ResponseStream;
            using var buffer = new MemoryStream();
            await body.CopyToAsync(buffer, cancellationToken);
            return new StoredObject(buffer.ToArray(), response.Headers.ContentType ?? "application/octet-stream");
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteObjectAsync(options.Value.BucketName, key, cancellationToken);
        }
        catch (Exception exception)
        {
            LogDeleteFailed(logger, exception, key);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Object {ObjectKey} could not be deleted.")]
    private static partial void LogDeleteFailed(ILogger logger, Exception exception, string objectKey);
}
