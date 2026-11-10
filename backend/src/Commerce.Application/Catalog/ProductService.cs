using Commerce.Domain.Catalog;

namespace Commerce.Application.Catalog;

public sealed class ProductService(ICatalogStore store, TimeProvider time)
{
    public async Task<CatalogResult<ProductResponse>> CreateAsync(
        string? name,
        string? slug,
        string? shortDescription,
        string? description,
        string? brand,
        bool isFeatured,
        IReadOnlyCollection<Guid>? categoryIds,
        IReadOnlyCollection<Guid>? collectionIds,
        CancellationToken cancellationToken)
    {
        var errors = ValidateText(name, shortDescription, description, brand, requireNarrative: false);
        if (errors.Count > 0)
        {
            return CatalogResult<ProductResponse>.Invalid(errors);
        }

        var resolved = await CatalogSupport.ResolveSlugAsync(store, SlugOwner.Product, slug, name!, null, cancellationToken);
        if (resolved is null)
        {
            return CatalogResult<ProductResponse>.Invalid(Field("slug", "Enter a valid slug."));
        }

        if (resolved.Length == 0)
        {
            return CatalogResult<ProductResponse>.Conflict("slug", "That slug is already in use.");
        }

        var categories = await LoadCategoriesAsync(categoryIds, cancellationToken);
        if (categories is null)
        {
            return CatalogResult<ProductResponse>.Invalid(Field("categoryIds", "One or more categories do not exist."));
        }

        var collections = await LoadCollectionsAsync(collectionIds, cancellationToken);
        if (collections is null)
        {
            return CatalogResult<ProductResponse>.Invalid(Field("collectionIds", "One or more collections do not exist."));
        }

        var now = time.GetUtcNow();
        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            Name = name!.Trim(),
            Slug = resolved,
            ShortDescription = shortDescription?.Trim() ?? string.Empty,
            Description = description?.Trim() ?? string.Empty,
            Brand = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim(),
            IsFeatured = isFeatured,
            Status = ProductStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.Add(product);
        store.ReplaceCategories(product, categories);
        store.ReplaceCollections(product, collections);
        return await CatalogSupport.SaveAsync(store, () => CatalogMapper.Product(product, includeStock: true), cancellationToken);
    }

    public async Task<CatalogResult<ProductResponse>> UpdateAsync(
        Guid id,
        string? name,
        string? slug,
        string? shortDescription,
        string? description,
        string? brand,
        bool? isFeatured,
        IReadOnlyCollection<Guid>? categoryIds,
        IReadOnlyCollection<Guid>? collectionIds,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var product = await store.FindProductGraphAsync(id, cancellationToken);
        if (product is null)
        {
            return CatalogResult<ProductResponse>.NotFound();
        }

        var errors = ValidateText(name, shortDescription, description, brand, requireNarrative: false, nameRequired: name is not null);
        if (name is null && shortDescription is null && description is null && brand is null)
        {
            errors.Clear();
        }

        if (name is not null)
        {
            foreach (var (key, value) in CatalogSupport.Text("name", name, PublishRules.NameMaxLength, required: true))
            {
                errors[key] = value;
            }
        }

        if (errors.Count > 0)
        {
            return CatalogResult<ProductResponse>.Invalid(errors);
        }

        if (slug is not null || name is not null)
        {
            var resolved = await CatalogSupport.ResolveSlugAsync(
                store, SlugOwner.Product, slug ?? product.Slug, name ?? product.Name, product.Id, cancellationToken);
            if (resolved is null)
            {
                return CatalogResult<ProductResponse>.Invalid(Field("slug", "Enter a valid slug."));
            }

            if (resolved.Length == 0)
            {
                return CatalogResult<ProductResponse>.Conflict("slug", "That slug is already in use.");
            }

            store.RetireSlug(SlugOwner.Product, product.Id, product.Slug, resolved, time.GetUtcNow());
            product.Slug = resolved;
        }

        if (name is not null)
        {
            product.Name = name.Trim();
        }

        if (shortDescription is not null)
        {
            product.ShortDescription = shortDescription.Trim();
        }

        if (description is not null)
        {
            product.Description = description.Trim();
        }

        if (brand is not null)
        {
            product.Brand = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim();
        }

        if (isFeatured is bool featured)
        {
            product.IsFeatured = featured;
        }

        if (categoryIds is not null)
        {
            var categories = await LoadCategoriesAsync(categoryIds, cancellationToken);
            if (categories is null)
            {
                store.Abandon();
                return CatalogResult<ProductResponse>.Invalid(Field("categoryIds", "One or more categories do not exist."));
            }

            store.ReplaceCategories(product, categories);
        }

        if (collectionIds is not null)
        {
            var collections = await LoadCollectionsAsync(collectionIds, cancellationToken);
            if (collections is null)
            {
                store.Abandon();
                return CatalogResult<ProductResponse>.Invalid(Field("collectionIds", "One or more collections do not exist."));
            }

            store.ReplaceCollections(product, collections);
        }

        if (!await KeepPublishedStateAsync(product))
        {
            return CatalogResult<ProductResponse>.Invalid(PublishRules.ForProduct(product));
        }

        return await CatalogSupport.SaveProductAsync(
            store, product, expectedVersion, time.GetUtcNow(), () => CatalogMapper.Product(product, includeStock: true), cancellationToken);
    }

    public async Task<CatalogResult<ProductResponse>> PublishAsync(Guid id, CancellationToken cancellationToken, int? expectedVersion = null)
    {
        var product = await store.FindProductGraphAsync(id, cancellationToken);
        if (product is null)
        {
            return CatalogResult<ProductResponse>.NotFound();
        }

        var errors = PublishRules.ForProduct(product);
        if (errors.Count > 0)
        {
            return CatalogResult<ProductResponse>.Invalid(errors);
        }

        var now = time.GetUtcNow();
        product.Status = ProductStatus.Active;
        product.PublishedAt ??= now;
        return await CatalogSupport.SaveProductAsync(
            store, product, expectedVersion, now, () => CatalogMapper.Product(product, includeStock: true), cancellationToken);
    }

    public async Task<CatalogResult<ProductResponse>> ArchiveAsync(Guid id, CancellationToken cancellationToken, int? expectedVersion = null)
    {
        var product = await store.FindProductGraphAsync(id, cancellationToken);
        if (product is null)
        {
            return CatalogResult<ProductResponse>.NotFound();
        }

        product.Status = ProductStatus.Archived;
        return await CatalogSupport.SaveProductAsync(
            store, product, expectedVersion, time.GetUtcNow(), () => CatalogMapper.Product(product, includeStock: true), cancellationToken);
    }

    public async Task<CatalogResult<OptionResponse>> AddOptionAsync(
        Guid productId,
        string? name,
        int position,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var product = await store.FindProductGraphAsync(productId, cancellationToken);
        if (product is null)
        {
            return CatalogResult<OptionResponse>.NotFound();
        }

        if (product.Variants.Count > 0)
        {
            return CatalogResult<OptionResponse>.Conflict("name", "Add options before variants.");
        }

        var normalized = Slug.Normalize(name);
        var errors = CatalogSupport.Text("name", name, PublishRules.OptionNameMaxLength, required: true);
        if (normalized is null && errors.Count == 0)
        {
            errors["name"] = ["Enter a valid option name."];
        }

        if (errors.Count > 0 || normalized is null)
        {
            return CatalogResult<OptionResponse>.Invalid(errors);
        }

        if (product.Options.Any(option => option.NormalizedName == normalized))
        {
            return CatalogResult<OptionResponse>.Conflict("name", "That option already exists on this product.");
        }

        var option = new ProductOption
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            Product = product,
            Name = name!.Trim(),
            NormalizedName = normalized,
            Position = position,
        };
        product.Options.Add(option);
        store.Add(option);
        return await CatalogSupport.SaveProductAsync(store, product, expectedVersion, time.GetUtcNow(), () => MapOption(option, product.Version), cancellationToken);
    }

    public async Task<CatalogResult<OptionResponse>> UpdateOptionAsync(
        Guid optionId,
        string? name,
        int? position,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var option = await store.FindOptionAsync(optionId, cancellationToken);
        if (option is null)
        {
            return CatalogResult<OptionResponse>.NotFound();
        }

        if (name is not null)
        {
            var normalized = Slug.Normalize(name);
            var errors = CatalogSupport.Text("name", name, PublishRules.OptionNameMaxLength, required: true);
            if (normalized is null)
            {
                errors["name"] = ["Enter a valid option name."];
                return CatalogResult<OptionResponse>.Invalid(errors);
            }

            var product = await store.FindProductGraphAsync(option.ProductId, cancellationToken);
            if (product!.Options.Any(existing => existing.Id != option.Id && existing.NormalizedName == normalized))
            {
                return CatalogResult<OptionResponse>.Conflict("name", "That option already exists on this product.");
            }

            option.Name = name.Trim();
            option.NormalizedName = normalized;
        }

        if (position is int order)
        {
            option.Position = order;
        }

        var owner = await store.FindProductGraphAsync(option.ProductId, cancellationToken);
        return owner is null
            ? CatalogResult<OptionResponse>.NotFound()
            : await CatalogSupport.SaveProductAsync(store, owner, expectedVersion, time.GetUtcNow(), () => MapOption(option, owner.Version), cancellationToken);
    }

    public async Task<CatalogResult<bool>> DeleteOptionAsync(Guid optionId, CancellationToken cancellationToken, int? expectedVersion = null)
    {
        var option = await store.FindOptionAsync(optionId, cancellationToken);
        if (option is null)
        {
            return CatalogResult<bool>.NotFound();
        }

        if (await store.OptionIsUsedAsync(optionId, cancellationToken))
        {
            return CatalogResult<bool>.Conflict("option", "Remove the variants that use this option first.");
        }

        var owner = await store.FindProductGraphAsync(option.ProductId, cancellationToken);
        if (owner is null)
        {
            return CatalogResult<bool>.NotFound();
        }

        store.Remove(option);
        return await CatalogSupport.SaveProductAsync(store, owner, expectedVersion, time.GetUtcNow(), () => true, cancellationToken);
    }

    public async Task<CatalogResult<OptionValueResponse>> AddOptionValueAsync(
        Guid optionId,
        string? value,
        string? code,
        int position,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var option = await store.FindOptionAsync(optionId, cancellationToken);
        if (option is null)
        {
            return CatalogResult<OptionValueResponse>.NotFound();
        }

        var normalized = Slug.Normalize(value);
        var errors = CatalogSupport.Text("value", value, PublishRules.OptionNameMaxLength, required: true);
        if (code is not null && code.Trim().Length > PublishRules.CodeMaxLength)
        {
            errors["code"] = [$"Use at most {PublishRules.CodeMaxLength} characters."];
        }

        if (normalized is null)
        {
            errors["value"] = ["Enter a valid option value."];
        }

        if (errors.Count > 0 || normalized is null)
        {
            return CatalogResult<OptionValueResponse>.Invalid(errors);
        }

        if (option.Values.Any(existing => existing.NormalizedValue == normalized))
        {
            return CatalogResult<OptionValueResponse>.Conflict("value", "That value already exists.");
        }

        var optionValue = new ProductOptionValue
        {
            Id = Guid.CreateVersion7(),
            OptionId = option.Id,
            Option = option,
            Value = value!.Trim(),
            NormalizedValue = normalized,
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim(),
            Position = position,
        };
        option.Values.Add(optionValue);
        store.Add(optionValue);
        var owner = await store.FindProductGraphAsync(option.ProductId, cancellationToken);
        return owner is null
            ? CatalogResult<OptionValueResponse>.NotFound()
            : await CatalogSupport.SaveProductAsync(
                store,
                owner,
                expectedVersion,
                time.GetUtcNow(),
                () => new OptionValueResponse(optionValue.Id, optionValue.Value, optionValue.Code, optionValue.Position),
                cancellationToken);
    }

    public async Task<CatalogResult<bool>> DeleteOptionValueAsync(Guid optionValueId, CancellationToken cancellationToken, int? expectedVersion = null)
    {
        var optionValue = await store.FindOptionValueAsync(optionValueId, cancellationToken);
        if (optionValue is null)
        {
            return CatalogResult<bool>.NotFound();
        }

        if (await store.OptionValueIsUsedAsync(optionValueId, cancellationToken))
        {
            return CatalogResult<bool>.Conflict("optionValue", "Remove the variants that use this value first.");
        }

        var owner = await store.FindProductGraphAsync(optionValue.Option.ProductId, cancellationToken);
        if (owner is null)
        {
            return CatalogResult<bool>.NotFound();
        }

        store.Remove(optionValue);
        return await CatalogSupport.SaveProductAsync(store, owner, expectedVersion, time.GetUtcNow(), () => true, cancellationToken);
    }

    public async Task<CatalogResult<VariantResponse>> AddVariantAsync(
        Guid productId,
        string? sku,
        string? barcode,
        decimal price,
        decimal? compareAtPrice,
        bool isActive,
        IReadOnlyCollection<Guid> optionValueIds,
        int initialQuantity,
        Guid actorUserId,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var product = await store.FindProductGraphAsync(productId, cancellationToken);
        if (product is null)
        {
            return CatalogResult<VariantResponse>.NotFound();
        }

        var prepared = await PrepareVariantAsync(product, null, sku, barcode, price, compareAtPrice, optionValueIds, cancellationToken);
        if (prepared.Failure is not null)
        {
            return CatalogResult<VariantResponse>.Fail(prepared.Failure);
        }

        if (initialQuantity < 0)
        {
            return CatalogResult<VariantResponse>.Invalid(Field("initialQuantity", "Stock cannot be negative."));
        }

        var now = time.GetUtcNow();
        var variant = new ProductVariant
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            Product = product,
            Sku = prepared.Sku!,
            Barcode = prepared.Barcode,
            Price = price,
            CompareAtPrice = compareAtPrice,
            CurrencyCode = Money.Currency,
            IsActive = isActive,
            CombinationKey = prepared.CombinationKey!,
            CreatedAt = now,
            UpdatedAt = now,
            Balance = new InventoryBalance { VariantId = Guid.Empty, OnHand = initialQuantity, Reserved = 0 },
        };
        variant.Balance.VariantId = variant.Id;
        variant.Balance.Variant = variant;
        foreach (var optionValue in prepared.Values)
        {
            variant.Selections.Add(new VariantOptionValue
            {
                VariantId = variant.Id,
                Variant = variant,
                OptionValueId = optionValue.Id,
                OptionValue = optionValue,
            });
        }

        product.Variants.Add(variant);
        if (product.Status == ProductStatus.Active && !PublishRules.CombinationIsComplete(product, variant))
        {
            store.Abandon();
            return CatalogResult<VariantResponse>.Invalid(Field("optionValueIds", "Each active variant must choose one value for every option."));
        }

        if (product.Status == ProductStatus.Active && isActive && price <= 0m)
        {
            store.Abandon();
            return CatalogResult<VariantResponse>.Invalid(Field("price", "Every active variant needs a price greater than zero."));
        }

        store.Add(variant);
        if (initialQuantity > 0)
        {
            store.Add(new InventoryMovement
            {
                Id = Guid.CreateVersion7(),
                VariantId = variant.Id,
                Variant = variant,
                MovementType = InventoryMovementType.InitialStock,
                QuantityDelta = initialQuantity,
                OnHandAfter = initialQuantity,
                ReservedAfter = 0,
                ActorUserId = actorUserId,
                IdempotencyKey = $"initial-{variant.Id:N}",
                RequestHash = CatalogSupport.HashAdjustment(InventoryMovementType.InitialStock, initialQuantity, null),
                CreatedAt = now,
            });
        }

        return await CatalogSupport.SaveProductAsync(store, product, expectedVersion, now, () => MapVariant(variant, product.Version), cancellationToken);
    }

    public async Task<CatalogResult<VariantResponse>> UpdateVariantAsync(
        Guid variantId,
        string? sku,
        string? barcode,
        decimal? price,
        decimal? compareAtPrice,
        bool compareAtSpecified,
        bool? isActive,
        IReadOnlyCollection<Guid>? optionValueIds,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var variant = await store.FindVariantGraphAsync(variantId, cancellationToken);
        if (variant is null)
        {
            return CatalogResult<VariantResponse>.NotFound();
        }

        var product = variant.Product;
        var nextPrice = price ?? variant.Price;
        var nextCompare = compareAtSpecified ? compareAtPrice : variant.CompareAtPrice;
        var nextBarcode = barcode ?? variant.Barcode;
        var prepared = await PrepareVariantAsync(
            product,
            variant.Id,
            sku ?? variant.Sku,
            nextBarcode,
            nextPrice,
            nextCompare,
            optionValueIds ?? variant.Selections.Select(selection => selection.OptionValueId).ToArray(),
            cancellationToken);
        if (prepared.Failure is not null)
        {
            return CatalogResult<VariantResponse>.Fail(prepared.Failure);
        }

        var nextActive = isActive ?? variant.IsActive;
        if (product.Status == ProductStatus.Active && variant.IsActive && !nextActive
            && product.Variants.Count(item => item.IsActive && item.Id != variant.Id) == 0)
        {
            return CatalogResult<VariantResponse>.Conflict("isActive", "An active product needs at least one active variant.");
        }

        variant.Sku = prepared.Sku!;
        variant.Barcode = prepared.Barcode;
        variant.Price = nextPrice;
        variant.CompareAtPrice = nextCompare;
        variant.IsActive = nextActive;
        variant.CombinationKey = prepared.CombinationKey!;
        variant.UpdatedAt = time.GetUtcNow();
        if (optionValueIds is not null)
        {
            foreach (var selection in variant.Selections.ToArray())
            {
                store.Remove(selection);
            }

            variant.Selections.Clear();
            foreach (var optionValue in prepared.Values)
            {
                var selection = new VariantOptionValue
                {
                    VariantId = variant.Id,
                    Variant = variant,
                    OptionValueId = optionValue.Id,
                    OptionValue = optionValue,
                };
                variant.Selections.Add(selection);
                store.Add(selection);
            }
        }

        if (product.Status == ProductStatus.Active)
        {
            var errors = PublishRules.ForProduct(product);
            if (errors.Count > 0)
            {
                store.Abandon();
                return CatalogResult<VariantResponse>.Invalid(errors);
            }
        }

        return await CatalogSupport.SaveProductAsync(store, product, expectedVersion, time.GetUtcNow(), () => MapVariant(variant, product.Version), cancellationToken);
    }

    public async Task<CatalogResult<bool>> DeleteVariantAsync(Guid variantId, CancellationToken cancellationToken, int? expectedVersion = null)
    {
        var variant = await store.FindVariantGraphAsync(variantId, cancellationToken);
        if (variant is null)
        {
            return CatalogResult<bool>.NotFound();
        }

        if (variant.Product.Status != ProductStatus.Draft)
        {
            return CatalogResult<bool>.Conflict("variant", "Only variants of a draft product can be deleted.");
        }

        if (await store.VariantHasMovementsAsync(variantId, cancellationToken))
        {
            return CatalogResult<bool>.Conflict("variant", "Variants with inventory history cannot be deleted.");
        }

        var owner = variant.Product;
        store.Remove(variant);
        return await CatalogSupport.SaveProductAsync(store, owner, expectedVersion, time.GetUtcNow(), () => true, cancellationToken);
    }

    public async Task<CatalogResult<IReadOnlyList<VariantResponse>>> AddVariantsAsync(
        Guid productId,
        IReadOnlyList<NewVariant> variants,
        Guid actorUserId,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        if (variants.Count is 0 or > 100)
        {
            return CatalogResult<IReadOnlyList<VariantResponse>>.Invalid(Field("variants", "Send from 1 to 100 variants."));
        }

        var product = await store.FindProductGraphAsync(productId, cancellationToken);
        if (product is null)
        {
            return CatalogResult<IReadOnlyList<VariantResponse>>.NotFound();
        }

        var now = time.GetUtcNow();
        var created = new List<ProductVariant>();
        foreach (var draft in variants)
        {
            var prepared = await PrepareVariantAsync(product, null, draft.Sku, draft.Barcode, draft.Price, draft.CompareAtPrice, draft.OptionValueIds, cancellationToken);
            if (prepared.Failure is not null)
            {
                store.Abandon();
                return CatalogResult<IReadOnlyList<VariantResponse>>.Fail(prepared.Failure);
            }

            if (draft.InitialQuantity < 0)
            {
                store.Abandon();
                return CatalogResult<IReadOnlyList<VariantResponse>>.Invalid(Field("initialQuantity", "Stock cannot be negative."));
            }

            var variant = new ProductVariant
            {
                Id = Guid.CreateVersion7(),
                ProductId = product.Id,
                Product = product,
                Sku = prepared.Sku!,
                Barcode = prepared.Barcode,
                Price = draft.Price,
                CompareAtPrice = draft.CompareAtPrice,
                CurrencyCode = Money.Currency,
                IsActive = draft.IsActive,
                CombinationKey = prepared.CombinationKey!,
                CreatedAt = now,
                UpdatedAt = now,
                Balance = new InventoryBalance { OnHand = draft.InitialQuantity, Reserved = 0 },
            };
            variant.Balance.VariantId = variant.Id;
            variant.Balance.Variant = variant;
            foreach (var optionValue in prepared.Values)
            {
                variant.Selections.Add(new VariantOptionValue
                {
                    VariantId = variant.Id,
                    Variant = variant,
                    OptionValueId = optionValue.Id,
                    OptionValue = optionValue,
                });
            }

            product.Variants.Add(variant);
            store.Add(variant);
            if (draft.InitialQuantity > 0)
            {
                store.Add(new InventoryMovement
                {
                    Id = Guid.CreateVersion7(),
                    VariantId = variant.Id,
                    Variant = variant,
                    MovementType = InventoryMovementType.InitialStock,
                    QuantityDelta = draft.InitialQuantity,
                    OnHandAfter = draft.InitialQuantity,
                    ReservedAfter = 0,
                    ActorUserId = actorUserId,
                    IdempotencyKey = $"initial-{variant.Id:N}",
                    RequestHash = CatalogSupport.HashAdjustment(InventoryMovementType.InitialStock, draft.InitialQuantity, null),
                    CreatedAt = now,
                });
            }

            created.Add(variant);
        }

        if (product.Status == ProductStatus.Active)
        {
            var errors = PublishRules.ForProduct(product);
            if (errors.Count > 0)
            {
                store.Abandon();
                return CatalogResult<IReadOnlyList<VariantResponse>>.Invalid(errors);
            }
        }

        return await CatalogSupport.SaveProductAsync(
            store,
            product,
            expectedVersion,
            now,
            () => (IReadOnlyList<VariantResponse>)created.Select(variant => MapVariant(variant, product.Version)).ToArray(),
            cancellationToken);
    }

    private async Task<bool> KeepPublishedStateAsync(Product product)
    {
        if (product.Status != ProductStatus.Active)
        {
            return true;
        }

        var errors = PublishRules.ForProduct(product);
        if (errors.Count == 0)
        {
            return true;
        }

        store.Abandon();
        return false;
    }

    private async Task<PreparedVariant> PrepareVariantAsync(
        Product product,
        Guid? exceptVariantId,
        string? sku,
        string? barcode,
        decimal price,
        decimal? compareAtPrice,
        IReadOnlyCollection<Guid> optionValueIds,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedSku = Sku.Normalize(sku);
        if (normalizedSku is null)
        {
            errors["sku"] = ["Enter a valid SKU."];
        }
        else if (await store.SkuTakenAsync(normalizedSku, exceptVariantId, cancellationToken)
            || product.Variants.Any(variant => variant.Id != exceptVariantId && variant.Sku == normalizedSku))
        {
            return new PreparedVariant(null, null, null, [], CatalogResult<VariantResponse>.Conflict("sku", "That SKU is already in use.").Failure);
        }

        string? normalizedBarcode = null;
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            normalizedBarcode = Sku.Normalize(barcode);
            if (normalizedBarcode is null)
            {
                errors["barcode"] = ["Enter a valid barcode."];
            }
            else if (await store.BarcodeTakenAsync(normalizedBarcode, exceptVariantId, cancellationToken))
            {
                return new PreparedVariant(null, null, null, [], CatalogResult<VariantResponse>.Conflict("barcode", "That barcode is already in use.").Failure);
            }
        }

        if (price < 0m || price > Money.Maximum || price != decimal.Round(price, 2))
        {
            errors["price"] = ["Enter a valid price."];
        }

        if (!Money.CompareAtIsValid(price < 0 ? 0 : price, compareAtPrice))
        {
            errors["compareAtPrice"] = ["The compare-at price must be greater than the price."];
        }

        var values = new List<ProductOptionValue>();
        var distinctIds = optionValueIds.Distinct().ToArray();
        if (product.Options.Count == 0)
        {
            if (distinctIds.Length > 0)
            {
                errors["optionValueIds"] = ["This product has no options."];
            }
        }
        else
        {
            foreach (var option in product.Options)
            {
                var matches = option.Values.Where(value => distinctIds.Contains(value.Id)).ToArray();
                if (matches.Length != 1)
                {
                    errors["optionValueIds"] = ["Choose one value for every option."];
                    break;
                }

                values.Add(matches[0]);
            }

            if (values.Count != distinctIds.Length)
            {
                errors["optionValueIds"] = ["Choose one value for every option."];
            }
        }

        var combination = CombinationKey.Build(values.Select(value => value.Id));
        if (product.Variants.Any(variant => variant.Id != exceptVariantId && variant.CombinationKey == combination))
        {
            return new PreparedVariant(null, null, null, [], CatalogResult<VariantResponse>.Conflict("optionValueIds", "That combination already exists.").Failure);
        }

        if (errors.Count > 0)
        {
            return new PreparedVariant(null, null, null, [], CatalogResult<VariantResponse>.Invalid(errors).Failure);
        }

        return new PreparedVariant(normalizedSku, normalizedBarcode, combination, values, null);
    }

    private async Task<IReadOnlyList<Category>?> LoadCategoriesAsync(IReadOnlyCollection<Guid>? ids, CancellationToken cancellationToken)
    {
        var requested = ids?.Distinct().ToArray() ?? [];
        var found = await store.FindCategoriesAsync(requested, cancellationToken);
        return found.Count == requested.Length ? found : null;
    }

    private async Task<IReadOnlyList<Collection>?> LoadCollectionsAsync(IReadOnlyCollection<Guid>? ids, CancellationToken cancellationToken)
    {
        var requested = ids?.Distinct().ToArray() ?? [];
        var found = await store.FindCollectionsAsync(requested, cancellationToken);
        return found.Count == requested.Length ? found : null;
    }

    private static Dictionary<string, string[]> ValidateText(
        string? name,
        string? shortDescription,
        string? description,
        string? brand,
        bool requireNarrative,
        bool nameRequired = true)
    {
        var errors = CatalogSupport.Text("name", name, PublishRules.NameMaxLength, nameRequired);
        foreach (var (key, value) in CatalogSupport.Text("shortDescription", shortDescription, PublishRules.ShortDescriptionMaxLength, requireNarrative))
        {
            errors[key] = value;
        }

        foreach (var (key, value) in CatalogSupport.Text("description", description, PublishRules.DescriptionMaxLength, requireNarrative))
        {
            errors[key] = value;
        }

        if (brand is not null && brand.Trim().Length > PublishRules.BrandMaxLength)
        {
            errors["brand"] = [$"Use at most {PublishRules.BrandMaxLength} characters."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> Field(string key, string message) =>
        new() { [key] = [message] };

    private static OptionResponse MapOption(ProductOption option, int productVersion) => new(
        option.Id,
        option.Name,
        option.Position,
        option.Values
            .OrderBy(value => value.Position)
            .Select(value => new OptionValueResponse(value.Id, value.Value, value.Code, value.Position))
            .ToArray(),
        productVersion);

    private static VariantResponse MapVariant(ProductVariant variant, int productVersion) => new(
        variant.Id,
        variant.Sku,
        variant.Barcode,
        variant.IsActive,
        Money.Format(variant.Price),
        variant.CompareAtPrice is decimal compareAt ? Money.Format(compareAt) : null,
        variant.CurrencyCode,
        variant.Balance?.OnHand ?? 0,
        variant.Balance?.Reserved ?? 0,
        variant.Balance?.Available ?? 0,
            variant.Selections
            .Select(selection => new SelectedOptionResponse(selection.OptionValue.Option.Name, selection.OptionValue.Value, selection.OptionValue.Code))
            .ToArray(),
        productVersion);

    private sealed record PreparedVariant(
        string? Sku,
        string? Barcode,
        string? CombinationKey,
        IReadOnlyList<ProductOptionValue> Values,
        CatalogFailure? Failure);
}
