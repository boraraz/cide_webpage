using Microsoft.Extensions.Logging;

namespace Commerce.Application.Catalog;

public sealed partial class DerivativePublisher(IObjectStorage storage, IImageDerivatives derivatives, ILogger<DerivativePublisher> logger)
{
    public async Task PublishAsync(string objectKey, ReadOnlyMemory<byte> content, int? knownWidth, CancellationToken cancellationToken)
    {
        if (knownWidth is not int width || width <= 0)
        {
            return;
        }

        IReadOnlyList<ImageDerivative> images;
        try
        {
            images = derivatives.Create(content, width);
        }
        catch (Exception exception)
        {
            LogSkipped(logger, exception, objectKey, 0);
            return;
        }

        var stored = new List<int>();
        foreach (var image in images)
        {
            if (!MediaDerivativePaths.IsTarget(image.Width) || image.Width > width || image.Content.Length == 0)
            {
                continue;
            }

            try
            {
                await storage.PutAsync(
                    MediaDerivativePaths.SiblingKey(objectKey, image.Width),
                    image.Content,
                    "image/webp",
                    cancellationToken);
                stored.Add(image.Width);
            }
            catch (Exception exception)
            {
                LogSkipped(logger, exception, objectKey, image.Width);
            }
        }

        if (stored.Count == 0)
        {
            return;
        }

        try
        {
            await storage.PutAsync(
                MediaDerivativePaths.IndexKey(objectKey),
                DerivativeIndex.Write(stored),
                "application/json",
                cancellationToken);
        }
        catch (Exception exception)
        {
            LogSkipped(logger, exception, objectKey, 0);
        }
    }

    public async Task<IReadOnlyList<int>> ReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        var index = await storage.OpenAsync(MediaDerivativePaths.IndexKey(objectKey), cancellationToken);
        return index is null ? [] : DerivativeIndex.Read(index.Content);
    }

    public async Task<MediaDelivery?> OpenAsync(string objectKey, int? width, CancellationToken cancellationToken)
    {
        if (width is int requested)
        {
            if (!MediaDerivativePaths.IsTarget(requested))
            {
                return null;
            }

            var sibling = await storage.OpenAsync(MediaDerivativePaths.SiblingKey(objectKey, requested), cancellationToken);
            if (sibling is not null)
            {
                return new MediaDelivery(new StoredObject(sibling.Content, "image/webp"), MediaDerivativePaths.PublicCache);
            }
        }

        var original = await storage.OpenAsync(objectKey, cancellationToken);
        if (original is null)
        {
            return null;
        }

        var cache = width is null ? MediaDerivativePaths.PublicCache : MediaDerivativePaths.ShortCache;
        return new MediaDelivery(original, cache);
    }

    public async Task DeleteAllAsync(string objectKey, CancellationToken cancellationToken)
    {
        await storage.DeleteAsync(objectKey, cancellationToken);
        await storage.DeleteAsync(MediaDerivativePaths.IndexKey(objectKey), cancellationToken);
        foreach (var width in MediaDerivativePaths.Targets)
        {
            await storage.DeleteAsync(MediaDerivativePaths.SiblingKey(objectKey, width), cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image derivative {Width} for {ObjectKey} was not stored.")]
    private static partial void LogSkipped(ILogger logger, Exception exception, string objectKey, int width);
}
