using Commerce.Domain.Catalog;

namespace Commerce.Application.Catalog;

public sealed class MediaService(ICatalogStore store, IObjectStorage storage, TimeProvider time, DerivativePublisher derivatives)
{
    public async Task<CatalogResult<MediaResponse>> UploadAsync(
        Guid productId,
        ReadOnlyMemory<byte> content,
        string? altText,
        Guid? variantId,
        int? displayOrder,
        bool isPrimary,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var product = await store.FindProductGraphAsync(productId, cancellationToken);
        if (product is null)
        {
            return CatalogResult<MediaResponse>.NotFound();
        }

        var errors = CatalogSupport.Text("altText", altText, PublishRules.AltTextMaxLength, required: true);
        if (content.Length == 0 || content.Length > ImageSignature.MaximumBytes)
        {
            errors["file"] = ["Upload a JPEG, PNG or WebP image up to 5 MB."];
        }

        var headerLength = Math.Min(content.Length, 16);
        var contentType = headerLength == 0
            ? null
            : ImageSignature.DetectContentType(content.Span[..headerLength]);
        if (content.Length > 0 && contentType is null)
        {
            errors["file"] = ["Upload a JPEG, PNG or WebP image."];
        }

        if (variantId is Guid selected && product.Variants.All(variant => variant.Id != selected))
        {
            errors["variantId"] = ["That variant does not belong to this product."];
        }

        if (errors.Count > 0 || contentType is null)
        {
            return CatalogResult<MediaResponse>.Invalid(errors);
        }

        var now = time.GetUtcNow();
        var media = new ProductMedia
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            Product = product,
            VariantId = variantId,
            AltText = altText!.Trim(),
            DisplayOrder = displayOrder ?? (product.Media.Count == 0 ? 0 : product.Media.Max(item => item.DisplayOrder) + 1),
            IsPrimary = product.Media.Count == 0 || isPrimary,
            ContentType = contentType,
            FileSize = content.Length,
            Width = ImageHeader.Read(content.Span, contentType)?.Width,
            Height = ImageHeader.Read(content.Span, contentType)?.Height,
            CreatedAt = now,
            ObjectKey = $"catalog/{product.Id}/{Guid.Empty}.{ImageSignature.ExtensionFor(contentType)}",
        };
        media.ObjectKey = $"catalog/{product.Id}/{media.Id}.{ImageSignature.ExtensionFor(contentType)}";

        await storage.PutAsync(media.ObjectKey, content, contentType, cancellationToken);
        await derivatives.PublishAsync(media.ObjectKey, content, media.Width, cancellationToken);
        try
        {
            if (media.IsPrimary)
            {
                await store.ClearPrimaryAsync(product.Id, media.Id, cancellationToken);
            }

            product.Media.Add(media);
            store.Add(media);
            var widths = await derivatives.ReadAsync(media.ObjectKey, cancellationToken);
            var saved = await CatalogSupport.SaveProductAsync(
                store, product, expectedVersion, now, () => CatalogMapper.Media(media, widths), cancellationToken);
            if (!saved.Succeeded)
            {
                await derivatives.DeleteAllAsync(media.ObjectKey, cancellationToken);
            }

            return saved;
        }
        catch (CatalogConflictException conflict)
        {
            store.Abandon();
            await derivatives.DeleteAllAsync(media.ObjectKey, cancellationToken);
            return CatalogResult<MediaResponse>.Conflict(conflict.Field, conflict.Message, conflict.Status);
        }
        catch
        {
            store.Abandon();
            await derivatives.DeleteAllAsync(media.ObjectKey, cancellationToken);
            throw;
        }
    }

    public async Task<CatalogResult<MediaResponse>> UpdateAsync(
        Guid mediaId,
        string? altText,
        int? displayOrder,
        bool? isPrimary,
        Guid? variantId,
        bool variantSpecified,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var media = await store.FindMediaAsync(mediaId, cancellationToken);
        if (media is null)
        {
            return CatalogResult<MediaResponse>.NotFound();
        }

        var product = await store.FindProductGraphAsync(media.ProductId, cancellationToken);
        if (altText is not null)
        {
            var errors = CatalogSupport.Text("altText", altText, PublishRules.AltTextMaxLength, required: true);
            if (errors.Count > 0)
            {
                return CatalogResult<MediaResponse>.Invalid(errors);
            }

            media.AltText = altText.Trim();
        }

        if (displayOrder is int order)
        {
            media.DisplayOrder = order;
        }

        if (variantSpecified)
        {
            if (variantId is Guid selected && product!.Variants.All(variant => variant.Id != selected))
            {
                return CatalogResult<MediaResponse>.Invalid(new Dictionary<string, string[]>
                {
                    ["variantId"] = ["That variant does not belong to this product."],
                });
            }

            media.VariantId = variantId;
        }

        if (isPrimary is true)
        {
            await store.ClearPrimaryAsync(media.ProductId, media.Id, cancellationToken);
            media.IsPrimary = true;
        }

        if (product!.Status == ProductStatus.Active)
        {
            var errors = PublishRules.ForProduct(product);
            if (errors.Count > 0)
            {
                store.Abandon();
                return CatalogResult<MediaResponse>.Invalid(errors);
            }
        }

        return product is null
            ? CatalogResult<MediaResponse>.NotFound()
            : await CatalogSupport.SaveProductAsync(store, product, expectedVersion, time.GetUtcNow(), () => CatalogMapper.Media(media), cancellationToken);
    }

    public async Task<CatalogResult<ProductResponse>> OrderAsync(
        Guid productId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var product = await store.FindProductGraphAsync(productId, cancellationToken);
        if (product is null)
        {
            return CatalogResult<ProductResponse>.NotFound();
        }

        var existing = product.Media.Select(media => media.Id).ToHashSet();
        if (ids.Count != existing.Count || ids.Distinct().Count() != ids.Count || ids.Any(id => !existing.Contains(id)))
        {
            return CatalogResult<ProductResponse>.Invalid(new Dictionary<string, string[]>
            {
                ["ids"] = ["List every image on the product exactly once."],
            });
        }

        for (var index = 0; index < ids.Count; index++)
        {
            product.Media.First(media => media.Id == ids[index]).DisplayOrder = index;
        }

        if (product.Status == ProductStatus.Active)
        {
            var errors = PublishRules.ForProduct(product);
            if (errors.Count > 0)
            {
                store.Abandon();
                return CatalogResult<ProductResponse>.Invalid(errors);
            }
        }

        return await CatalogSupport.SaveProductAsync(
            store, product, expectedVersion, time.GetUtcNow(), () => CatalogMapper.Product(product, includeStock: true), cancellationToken);
    }

    public async Task<CatalogResult<bool>> DeleteAsync(Guid mediaId, CancellationToken cancellationToken, int? expectedVersion = null)
    {
        var media = await store.FindMediaAsync(mediaId, cancellationToken);
        if (media is null)
        {
            return CatalogResult<bool>.NotFound();
        }

        var product = await store.FindProductGraphAsync(media.ProductId, cancellationToken);
        var remaining = product!.Media.Where(item => item.Id != media.Id).ToArray();
        if (product.Status == ProductStatus.Active && remaining.Length == 0)
        {
            return CatalogResult<bool>.Conflict("media", "An active product needs an image. Archive the product or upload a replacement first.");
        }

        if (media.IsPrimary && remaining.Length > 0)
        {
            var successor = remaining.OrderBy(item => item.DisplayOrder).ThenBy(item => item.CreatedAt).First();
            successor.IsPrimary = true;
        }

        var key = media.ObjectKey;
        store.Remove(media);
        var saved = await CatalogSupport.SaveProductAsync(store, product, expectedVersion, time.GetUtcNow(), () => true, cancellationToken);
        if (saved.Succeeded)
        {
            await derivatives.DeleteAllAsync(key, cancellationToken);
        }

        return saved;
    }

    public Task<MediaDelivery?> OpenPublicAsync(Guid id, int? width, CancellationToken cancellationToken) =>
        OpenAsync(id, width, publishedOnly: true, cancellationToken);

    public async Task<MediaDelivery?> OpenAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await store.FindMediaAsync(id, cancellationToken);
        if (media is null)
        {
            return null;
        }

        var original = await storage.OpenAsync(media.ObjectKey, cancellationToken);
        return original is null ? null : new MediaDelivery(original, "private, no-store");
    }

    private async Task<MediaDelivery?> OpenAsync(Guid id, int? width, bool publishedOnly, CancellationToken cancellationToken)
    {
        var media = await store.FindMediaAsync(id, cancellationToken);
        if (media is null)
        {
            return null;
        }

        if (publishedOnly && (media.Product.Status != ProductStatus.Active || media.Variant is { IsActive: false }))
        {
            return null;
        }

        if (width is int requested && !MediaDerivativePaths.IsTarget(requested))
        {
            return null;
        }

        return await derivatives.OpenAsync(media.ObjectKey, width, cancellationToken);
    }
}
