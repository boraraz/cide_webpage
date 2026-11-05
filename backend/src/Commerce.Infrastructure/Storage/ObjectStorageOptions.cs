using System.ComponentModel.DataAnnotations;

namespace Commerce.Infrastructure.Storage;

/// <summary>
/// S3-compatible storage settings. Works with MinIO locally and with any S3-compatible provider
/// in production. Leave the access keys empty to use the AWS default credential chain.
/// </summary>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>Custom endpoint for S3-compatible providers. Leave empty for AWS S3.</summary>
    [Url]
    public string? ServiceUrl { get; set; }

    [Required]
    public string Region { get; set; } = "us-east-1";

    [Required(ErrorMessage =
        "ObjectStorage:BucketName is not configured. For local development run `npm run api:secrets`.")]
    public string BucketName { get; set; } = string.Empty;

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>Required by MinIO and most self-hosted S3-compatible services.</summary>
    public bool ForcePathStyle { get; set; }
}
