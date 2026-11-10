import { isApiError } from '@commerce/api-client';
import { Button, Checkbox, Dialog, Textarea, TextField } from '@commerce/ui';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useBlocker, useNavigate, useParams } from 'react-router';
import { PrivateImage } from '@/components/PrivateImage';
import { catalog } from '@/lib/api';
import { discountPreview, formatTry, toApiMoney } from '@/lib/money';
import { currentVersion, problemMessage } from '@/lib/problem';
import { matrixPreview } from '@/lib/variants';

export default function ProductEditorPage() {
  const { id = 'new' } = useParams();
  const isNew = id === 'new';
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const product = useQuery({
    queryKey: ['product', id],
    queryFn: () => catalog.adminProduct(id),
    enabled: !isNew,
  });
  const categories = useQuery({
    queryKey: ['categories'],
    queryFn: () => catalog.adminCategories(),
  });
  const collections = useQuery({
    queryKey: ['collections'],
    queryFn: () => catalog.adminCollections(),
  });
  const [dirty, setDirty] = useState(false);
  const [message, setMessage] = useState<string>();
  const [price, setPrice] = useState('1299.00');
  const [compareAt, setCompareAt] = useState('');
  const [nextPath, setNextPath] = useState<string>();
  const blocker = useBlocker(dirty);

  const loaded = useMemo(
    () =>
      product.data
        ? { ...product.data, version: 'version' in product.data ? Number(product.data.version) : 1 }
        : undefined,
    [product.data],
  );
  const version = loaded?.version ?? 1;
  const axes = useMemo(
    () =>
      (loaded?.options ?? []).map((option) =>
        option.values.map((value) => ({ id: value.id, label: value.value })),
      ),
    [loaded],
  );
  const preview = matrixPreview(axes);
  const formReady = isNew || loaded !== undefined;

  useEffect(() => {
    if (nextPath && !dirty) navigate(nextPath);
  }, [dirty, navigate, nextPath]);

  const save = useMutation({
    mutationFn: async (form: FormData) => {
      const body = {
        name: String(form.get('name') ?? ''),
        slug: String(form.get('slug') ?? '') || null,
        shortDescription: String(form.get('shortDescription') ?? ''),
        description: String(form.get('description') ?? ''),
        brand: String(form.get('brand') ?? '') || null,
        isFeatured: form.get('isFeatured') === 'on',
        categoryIds: form.getAll('categoryIds').map(String),
        collectionIds: form.getAll('collectionIds').map(String),
        expectedVersion: isNew ? undefined : version,
      };
      if (!body.name.trim()) throw new Error('Enter a product name.');
      return isNew ? catalog.createProduct(body) : catalog.updateProduct(id, body);
    },
    onSuccess: async (saved) => {
      setDirty(false);
      setMessage('Saved.');
      await queryClient.invalidateQueries({ queryKey: ['products'] });
      if (isNew) setNextPath(`/products/${saved.id}`);
      else await queryClient.invalidateQueries({ queryKey: ['product', id] });
    },
    onError: (error) => {
      setMessage(
        error instanceof Error && !isApiError(error) ? error.message : problemMessage(error),
      );
      if (currentVersion(error) !== null) void product.refetch();
    },
  });

  const publish = useMutation({
    mutationFn: () => catalog.publishProduct(id, version),
    onSuccess: async () => {
      setMessage('Published.');
      await queryClient.invalidateQueries({ queryKey: ['product', id] });
    },
    onError: (error) => {
      setMessage(problemMessage(error));
      if (currentVersion(error) !== null) void product.refetch();
    },
  });

  const upload = useMutation({
    mutationFn: (file: File) =>
      catalog.uploadMedia(id, {
        file,
        fileName: file.name,
        altText: file.name,
        isPrimary: (loaded?.media.length ?? 0) === 0,
      }),
    onSuccess: async () => {
      setMessage('Image uploaded.');
      await queryClient.invalidateQueries({ queryKey: ['product', id] });
    },
    onError: (error) => setMessage(problemMessage(error)),
  });

  const updateAlt = useMutation({
    mutationFn: (input: { mediaId: string; altText: string }) =>
      catalog.updateMedia(input.mediaId, {
        altText: input.altText,
        displayOrder: null,
        isPrimary: null,
        variantId: null,
        clearVariant: false,
        expectedVersion: version,
      }),
    onSuccess: async () => {
      setMessage('Alt text saved.');
      await queryClient.invalidateQueries({ queryKey: ['product', id] });
    },
    onError: (error) => setMessage(problemMessage(error)),
  });

  const moveMedia = useMutation({
    mutationFn: (ids: string[]) => catalog.orderMedia(id, { ids, expectedVersion: version }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['product', id] });
    },
    onError: (error) => setMessage(problemMessage(error)),
  });

  const parsedPrice = toApiMoney(price);
  const parsedCompare = compareAt ? toApiMoney(compareAt) : null;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold sm:text-2xl">
          {isNew ? 'New product' : (loaded?.name ?? 'Product')}
        </h1>
        <p className="text-sm text-muted-foreground">Version {isNew ? 1 : version}</p>
      </div>
      {message ? <p role="status">{message}</p> : null}
      {formReady ? (
        <form
          className="grid max-w-3xl gap-4"
          onChange={() => setDirty(true)}
          onSubmit={(event) => {
            event.preventDefault();
            save.mutate(new FormData(event.currentTarget));
          }}
        >
          <TextField name="name" label="Name" defaultValue={loaded?.name ?? ''} required />
          <TextField
            name="slug"
            label="Slug"
            defaultValue={loaded?.slug ?? ''}
            hint="Leave blank to generate one."
          />
          <TextField
            name="shortDescription"
            label="Short description"
            defaultValue={loaded?.shortDescription ?? ''}
            required
          />
          <Textarea
            name="description"
            label="Description"
            defaultValue={loaded?.description ?? ''}
            required
          />
          <TextField name="brand" label="Brand" defaultValue={loaded?.brand ?? ''} />
          <Checkbox
            name="isFeatured"
            label="Featured"
            defaultChecked={loaded?.isFeatured ?? false}
          />
          <fieldset>
            <legend className="text-sm font-medium">Categories</legend>
            {categories.data?.map((category) => (
              <Checkbox
                key={category.id}
                name="categoryIds"
                value={category.id}
                label={category.name}
                defaultChecked={loaded?.categories.some((item) => item.id === category.id)}
              />
            ))}
          </fieldset>
          <fieldset>
            <legend className="text-sm font-medium">Collections</legend>
            {collections.data?.map((collection) => (
              <Checkbox
                key={collection.id}
                name="collectionIds"
                value={collection.id}
                label={collection.name}
                defaultChecked={loaded?.collections.some((item) => item.id === collection.id)}
              />
            ))}
          </fieldset>
          <Button type="submit" loading={save.isPending}>
            Save product
          </Button>
        </form>
      ) : (
        <p>Loading product.</p>
      )}

      {!isNew && loaded ? (
        <section className="flex flex-col gap-4">
          <h2 className="text-lg font-semibold">Options</h2>
          <form
            className="flex flex-wrap items-end gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              const data = new FormData(event.currentTarget);
              void catalog
                .createOption(id, {
                  name: String(data.get('optionName') ?? ''),
                  position: loaded.options.length + 1,
                  expectedVersion: version,
                })
                .then(() => queryClient.invalidateQueries({ queryKey: ['product', id] }))
                .catch((error: unknown) => setMessage(problemMessage(error)));
              event.currentTarget.reset();
            }}
          >
            <TextField name="optionName" label="Option" required />
            <Button type="submit">Add option</Button>
          </form>
          {loaded.options.map((option) => (
            <form
              key={option.id}
              className="flex flex-wrap items-end gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                const data = new FormData(event.currentTarget);
                void catalog
                  .createOptionValue(option.id, {
                    value: String(data.get('value') ?? ''),
                    code: null,
                    position: option.values.length + 1,
                    expectedVersion: version,
                  })
                  .then(() => queryClient.invalidateQueries({ queryKey: ['product', id] }))
                  .catch((error: unknown) => setMessage(problemMessage(error)));
                event.currentTarget.reset();
              }}
            >
              <TextField name="value" label={`${option.name} value`} required />
              <Button type="submit">Add value</Button>
            </form>
          ))}
          <h2 className="text-lg font-semibold">Variants</h2>
          <p data-testid="matrix-preview">
            {preview.length === 0
              ? 'Add options before variants.'
              : `${preview.length} combinations: ${preview.map((row) => row.label).join(', ')}`}
          </p>
          <div className="flex flex-wrap gap-2">
            <TextField
              label="Price"
              value={price}
              onChange={(event) => setPrice(event.target.value)}
            />
            <TextField
              label="Compare-at price"
              value={compareAt}
              onChange={(event) => setCompareAt(event.target.value)}
            />
          </div>
          {parsedPrice ? (
            <p>Display price {formatTry(parsedPrice)}</p>
          ) : (
            <p role="alert">Enter a valid price.</p>
          )}
          {parsedPrice && parsedCompare ? (
            <p>{discountPreview(parsedPrice, parsedCompare)}</p>
          ) : null}
          <Button
            type="button"
            onClick={() => {
              if (!parsedPrice || preview.length === 0) return;
              void catalog
                .createVariants(id, {
                  expectedVersion: version,
                  variants: preview.map((row, index) => ({
                    sku: `VAR-${index + 1}-${id.slice(0, 4)}`.toUpperCase(),
                    price: parsedPrice,
                    compareAtPrice: parsedCompare ?? undefined,
                    optionValueIds: row.optionValueIds,
                    initialQuantity: 0,
                    isActive: true,
                  })),
                })
                .then(async () => {
                  setMessage('Variants created.');
                  await queryClient.invalidateQueries({ queryKey: ['product', id] });
                })
                .catch((error: unknown) => setMessage(problemMessage(error)));
            }}
          >
            Generate variants
          </Button>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[36rem] text-left text-sm">
              <thead>
                <tr>
                  <th className="p-2">SKU</th>
                  <th className="p-2">Price</th>
                  <th className="p-2">Available</th>
                </tr>
              </thead>
              <tbody>
                {loaded.variants.map((variant) => (
                  <tr key={variant.id}>
                    <td className="p-2">{variant.sku}</td>
                    <td className="p-2">{formatTry(variant.price)}</td>
                    <td className="p-2">{variant.available}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex gap-2">
            <Button type="button" onClick={() => publish.mutate()} loading={publish.isPending}>
              Publish
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => void catalog.archiveProduct(id, version)}
            >
              Archive
            </Button>
          </div>
          <h2 className="text-lg font-semibold">Images</h2>
          <input
            aria-label="Product image"
            type="file"
            accept="image/jpeg,image/png,image/webp"
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (file) upload.mutate(file);
            }}
          />
          <ul className="flex flex-col gap-3">
            {loaded.media.map((media, index) => (
              <li key={media.id} className="flex items-center gap-3">
                <PrivateImage
                  mediaId={media.id}
                  load={catalog.adminMediaBlob}
                  alt={media.altText}
                />
                <form
                  key={`${media.id}:${media.altText}`}
                  className="flex flex-wrap items-end gap-2"
                  onSubmit={(event) => {
                    event.preventDefault();
                    const altText = String(new FormData(event.currentTarget).get('altText') ?? '');
                    updateAlt.mutate({ mediaId: media.id, altText });
                  }}
                >
                  <TextField
                    name="altText"
                    label="Alt text"
                    defaultValue={media.altText}
                    required
                    maxLength={200}
                  />
                  <Button type="submit" variant="secondary" loading={updateAlt.isPending}>
                    Save alt text
                  </Button>
                </form>
                <Button
                  type="button"
                  variant="secondary"
                  aria-label={`Move ${media.altText} up`}
                  disabled={index === 0}
                  onClick={() => {
                    const ids = loaded.media.map((item) => item.id);
                    const swap = ids[index - 1];
                    if (!swap) return;
                    ids[index - 1] = ids[index]!;
                    ids[index] = swap;
                    moveMedia.mutate(ids);
                  }}
                >
                  Up
                </Button>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
      <Dialog
        open={blocker.state === 'blocked'}
        title="Leave without saving?"
        confirmLabel="Leave"
        onClose={() => blocker.reset?.()}
        onConfirm={() => blocker.proceed?.()}
      >
        This product has unsaved changes.
      </Dialog>
    </div>
  );
}
