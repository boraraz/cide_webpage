using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Storage;

/// <summary>Reports healthy when the configured bucket exists and is readable.</summary>
internal sealed class ObjectStorageHealthCheck(IAmazonS3 s3, IOptions<ObjectStorageOptions> options)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ListObjectsV2Request { BucketName = options.Value.BucketName, MaxKeys = 1 };
            await s3.ListObjectsV2Async(request, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "The object storage bucket is not reachable.",
                exception);
        }
    }
}
