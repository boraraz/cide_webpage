import { isApiError, type PublicProductDetail } from '@commerce/api-client';
import { Container } from '@commerce/ui';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useMemo } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { CatalogImage } from '@/features/catalog/CatalogImage';
import { ProductCard } from '@/features/catalog/ProductCard';
import {
  defaultSelection,
  resolveVariant,
  selectionFromSearch,
  writeSelection,
  type OptionGroup,
  type SellableVariant,
} from '@/features/catalog/variants';
import { catalog } from '@/lib/api';
import { env } from '@/lib/env';
import { absoluteMediaUrl } from '@/lib/media';
import { discountLabel, formatTry } from '@/lib/money';
import { pageMetadata, productJsonLd } from '@/seo/metadata';
import { usePageMetadata } from '@/seo/usePageMetadata';
import { NotFoundPage } from './NotFoundPage';

export default function ProductPage() {
  const { slug = '' } = useParams();
  const navigate = useNavigate();
  const product = useQuery({
    queryKey: ['product', slug],
    queryFn: () => catalog.product(slug),
    enabled: slug.length > 0,
  });
  useEffect(() => {
    if (product.data && product.data.slug !== slug) {
      navigate(`/product/${product.data.slug}${window.location.search}`, { replace: true });
    }
  }, [navigate, product.data, slug]);
  if (isApiError(product.error) && product.error.status === 404) return <NotFoundPage />;
  if (!product.data) return <Container className="py-16">Loading</Container>;
  return <ProductDetail product={product.data} />;
}

function ProductDetail({ product }: { product: PublicProductDetail }) {
  const [params, setParams] = useSearchParams();
  const options = product.options as unknown as OptionGroup[];
  const variants = product.variants as unknown as SellableVariant[];
  const selected = useMemo(
    () => selectionFromSearch(params, options) ?? defaultSelection(variants),
    [params, options, variants],
  );
  const variant =
    resolveVariant(variants, selected) ?? variants.find((item) => item.available) ?? variants[0];
  const gallery = useMemo(
    () => galleryFor(product.media, variant?.id),
    [product.media, variant?.id],
  );
  const related = useQuery({
    queryKey: ['related', product.slug],
    queryFn: () => catalog.relatedProducts(product.slug),
  });
  const price = variant?.price ?? product.fromPrice;
  const compareAt = variant?.compareAtPrice;
  usePageMetadata(
    pageMetadata({
      title: `${product.name} · Sportswear`,
      description: product.shortDescription,
      canonicalPath: `/product/${product.slug}`,
      image: absoluteMediaUrl(
        gallery[0]?.sources.find((source) => source.url)?.url ?? '',
        env.VITE_API_BASE_URL,
      ),
      jsonLd: productJsonLd({
        name: product.name,
        description: product.shortDescription,
        sku: variant?.sku ?? product.slug,
        price,
        currency: variant?.currency ?? product.currency,
        path: `/product/${product.slug}`,
        image: absoluteMediaUrl(
          gallery[0]?.sources.find((source) => source.url)?.url ?? '',
          env.VITE_API_BASE_URL,
        ),
        available: variant?.available ?? false,
      }),
    }),
  );

  function choose(name: string, value: string) {
    const next = { ...selected, [name]: value };
    const exact = resolveVariant(variants, next);
    const compatible =
      exact ??
      variants.find(
        (item) =>
          item.available &&
          item.options.some((option) => option.name === name && option.value === value),
      ) ??
      variants.find((item) =>
        item.options.some((option) => option.name === name && option.value === value),
      );
    if (!compatible) return;
    const query = new URLSearchParams(params);
    writeSelection(
      query,
      options,
      Object.fromEntries(compatible.options.map((option) => [option.name, option.value])),
    );
    setParams(query, { replace: true });
  }

  return (
    <Container className="py-8">
      <nav
        aria-label="Breadcrumb"
        className="mb-6 text-xs tracking-editorial text-muted-foreground uppercase"
      >
        <Link to="/shop">Shop</Link>
        {product.categories[0] ? (
          <span>
            {' / '}
            <Link to={`/category/${product.categories[0].slug}`}>{product.categories[0].name}</Link>
          </span>
        ) : null}
        <span>{` / ${product.name}`}</span>
      </nav>
      <div className="grid gap-10 lg:grid-cols-[minmax(0,1.2fr)_minmax(280px,0.8fr)]">
        <Gallery media={gallery} name={product.name} />
        <div className="lg:sticky lg:top-24 lg:self-start">
          <h1 className="font-display text-4xl font-light">{product.name}</h1>
          <p className="mt-4 text-sm">
            {formatTry(price)}
            {compareAt ? (
              <span className="ml-2 text-muted-foreground line-through">
                {formatTry(compareAt)}
              </span>
            ) : null}
            {discountLabel(price, compareAt) ? (
              <span className="ml-2">{discountLabel(price, compareAt)}</span>
            ) : null}
          </p>
          <p className="mt-6 max-w-prose text-sm leading-6">{product.description}</p>
          <div className="mt-8 flex flex-col gap-6">
            {options.map((option) => (
              <fieldset key={option.name}>
                <legend className="mb-2 text-xs tracking-editorial uppercase">{option.name}</legend>
                <div className="flex flex-wrap gap-2">
                  {option.values.map((value) => {
                    const exists = variants.some((item) =>
                      item.options.some(
                        (optionValue) =>
                          optionValue.name === option.name && optionValue.value === value.value,
                      ),
                    );
                    const pressed = selected[option.name] === value.value;
                    return (
                      <button
                        key={value.value}
                        type="button"
                        disabled={!exists}
                        aria-pressed={pressed}
                        className="min-h-11 border border-border px-3 disabled:text-muted-foreground disabled:line-through aria-pressed:bg-foreground aria-pressed:text-background"
                        onClick={() => choose(option.name, value.value)}
                      >
                        {value.value}
                      </button>
                    );
                  })}
                </div>
              </fieldset>
            ))}
          </div>
          <p className="mt-6 text-sm" role="status">
            {availabilityCopy(variant?.availability)}
            {variant?.sku ? (
              <span className="mt-1 block text-xs text-muted-foreground">SKU {variant.sku}</span>
            ) : null}
          </p>
          <p className="mt-4 text-sm text-muted-foreground">Purchasing is not available yet.</p>
          <button
            type="button"
            disabled
            className="mt-4 inline-flex min-h-11 items-center text-sm text-muted-foreground"
          >
            Bag (coming soon)
          </button>
        </div>
      </div>
      {related.data && related.data.length > 0 ? (
        <section className="mt-16" aria-labelledby="related-heading">
          <h2 id="related-heading" className="font-display text-3xl font-light">
            Related
          </h2>
          <ul className="mt-8 grid grid-cols-2 gap-x-4 gap-y-10 md:grid-cols-3 xl:grid-cols-4">
            {related.data.map((item) => (
              <li key={item.id}>
                <ProductCard product={item} />
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </Container>
  );
}

function Gallery({ media, name }: { media: PublicProductDetail['media']; name: string }) {
  const [params, setParams] = useSearchParams();
  const index = Math.min(
    Math.max(media.length - 1, 0),
    Math.max(0, Number(params.get('image') ?? '0') || 0),
  );
  const current = media[index] ?? media[0];
  function show(next: number) {
    const query = new URLSearchParams(params);
    if (next <= 0) query.delete('image');
    else query.set('image', String(next));
    setParams(query, { replace: true });
  }
  if (!current)
    return (
      <div
        className="aspect-[3/4] bg-surface"
        role="img"
        aria-label={`${name} image unavailable`}
      />
    );
  return (
    <div>
      <div
        className="aspect-[3/4] overflow-hidden bg-surface"
        tabIndex={0}
        aria-roledescription="carousel"
        onKeyDown={(event) => {
          if (event.key === 'ArrowRight') show(Math.min(media.length - 1, index + 1));
          if (event.key === 'ArrowLeft') show(Math.max(0, index - 1));
        }}
        onTouchStart={(event) => {
          event.currentTarget.dataset.x = String(event.changedTouches[0]?.clientX ?? 0);
        }}
        onTouchEnd={(event) => {
          const start = Number(event.currentTarget.dataset.x ?? 0);
          const delta = (event.changedTouches[0]?.clientX ?? start) - start;
          if (delta < -40) show(Math.min(media.length - 1, index + 1));
          if (delta > 40) show(Math.max(0, index - 1));
        }}
      >
        <CatalogImage
          sources={current.sources}
          alt={current.altText || name}
          priority
          sizes="(min-width: 1024px) 60vw, 100vw"
          className="h-full w-full object-cover"
        />
      </div>
      <div className="mt-3 flex items-center justify-between gap-3">
        <button
          type="button"
          className="min-h-11 px-2"
          onClick={() => show(Math.max(0, index - 1))}
          disabled={index === 0}
        >
          Previous
        </button>
        <span className="text-xs text-muted-foreground">
          {index + 1} / {media.length}
        </span>
        <button
          type="button"
          className="min-h-11 px-2"
          onClick={() => show(Math.min(media.length - 1, index + 1))}
          disabled={index >= media.length - 1}
        >
          Next
        </button>
      </div>
      <ul className="mt-3 flex gap-2 overflow-x-auto">
        {media.map((item, itemIndex) => (
          <li key={item.id}>
            <button
              type="button"
              aria-label={`Show image ${itemIndex + 1}`}
              aria-pressed={itemIndex === index}
              className="size-16 border border-border aria-pressed:border-foreground"
              onClick={() => show(itemIndex)}
            >
              <CatalogImage sources={item.sources} alt="" className="h-full w-full object-cover" />
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

function galleryFor(media: PublicProductDetail['media'], variantId?: string) {
  const shared = media.filter((item) => item.variantId == null);
  const specific = media.filter((item) => item.variantId === variantId);
  if (specific.length > 0) return [...specific, ...shared];
  return shared.length > 0 ? shared : media;
}

function availabilityCopy(value: string | undefined) {
  if (value === 'limited') return 'Limited availability';
  if (value === 'out_of_stock') return 'Out of stock';
  return 'In stock';
}
