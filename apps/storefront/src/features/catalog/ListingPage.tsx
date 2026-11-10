import { isApiError, type CategoryNode, type PublicCatalogFilters } from '@commerce/api-client';
import { Button, Container, Drawer } from '@commerce/ui';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { catalog } from '@/lib/api';
import { usePageMetadata } from '@/seo/usePageMetadata';
import { pageMetadata } from '@/seo/metadata';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { listingSearch, parseListing, toProductQuery, type ListingState } from './listing';
import { ProductCard } from './ProductCard';

export function ListingPage({
  title,
  description,
  locked = {},
  path,
  scope = false,
}: {
  title: string;
  description?: string | null;
  locked?: Partial<ListingState>;
  path: string;
  scope?: boolean;
}) {
  const [params, setParams] = useSearchParams();
  const [filtersOpen, setFiltersOpen] = useState(false);
  const state = parseListing(params, locked);
  useEffect(() => {
    if (params.get('priceBasis') === 'from') return;
    setParams(new URLSearchParams(listingSearch(state, locked).slice(1)), { replace: true });
  }, [locked, params, setParams, state]);
  const query = toProductQuery(state);
  const products = useQuery({
    queryKey: ['products', query],
    queryFn: () => catalog.products(query),
  });
  const filters = useQuery({
    queryKey: ['filters', state.q, state.category, state.collection],
    queryFn: () =>
      catalog.filters({
        q: state.q,
        category: state.category,
        collection: state.collection,
        priceBasis: 'from',
      }),
  });
  usePageMetadata(
    pageMetadata({
      title: `${title} · Sportswear`,
      description: description || title,
      canonicalPath: path,
    }),
  );

  if (isApiError(products.error) && products.error.status === 404) return <NotFoundPage />;

  const pages = Math.max(
    1,
    Math.ceil((products.data?.total ?? 0) / (products.data?.pageSize ?? 12)),
  );
  function update(next: Partial<ListingState>) {
    const merged = { ...state, ...next, page: next.page ?? 1 };
    setParams(new URLSearchParams(listingSearch(merged, locked).slice(1)));
  }

  return (
    <Container className="py-8">
      <header className="flex items-end justify-between gap-4 border-b border-border pb-6">
        <div>
          <h1 className="font-display text-4xl font-light">{title}</h1>
          {description ? (
            <p className="mt-3 max-w-2xl text-sm text-muted-foreground">{description}</p>
          ) : null}
          <p className="mt-3 text-xs tracking-editorial text-muted-foreground uppercase">
            {products.data ? `${products.data.total} products` : 'Loading'}
          </p>
        </div>
        <Button
          type="button"
          variant="secondary"
          className="lg:hidden"
          onClick={() => setFiltersOpen(true)}
        >
          Filter
        </Button>
      </header>
      <div className="mt-8 grid gap-10 lg:grid-cols-[220px_1fr]">
        <div className="hidden lg:block">
          <Filters
            state={state}
            filters={filters.data}
            onChange={update}
            scope={scope}
            locked={locked}
          />
        </div>
        <div>
          {products.isError ? <p role="alert">The products could not be loaded.</p> : null}
          {products.data?.items.length === 0 ? <p>No products match these filters.</p> : null}
          <ul className="grid grid-cols-2 gap-x-4 gap-y-10 md:grid-cols-3 xl:grid-cols-4">
            {products.data?.items.map((product) => (
              <li key={product.id}>
                <ProductCard product={product} />
              </li>
            ))}
          </ul>
          {pages > 1 ? (
            <nav aria-label="Pagination" className="mt-10 flex gap-4 text-sm">
              {state.page > 1 ? (
                <button
                  type="button"
                  className="min-h-11 underline-offset-4 hover:underline"
                  onClick={() => update({ page: state.page - 1 })}
                >
                  Previous
                </button>
              ) : null}
              <span className="inline-flex min-h-11 items-center">
                Page {state.page} of {pages}
              </span>
              {state.page < pages ? (
                <button
                  type="button"
                  className="min-h-11 underline-offset-4 hover:underline"
                  onClick={() => update({ page: state.page + 1 })}
                >
                  Next
                </button>
              ) : null}
            </nav>
          ) : null}
        </div>
      </div>
      <Drawer open={filtersOpen} onClose={() => setFiltersOpen(false)} title="Filter">
        <div className="p-4">
          <Filters
            state={state}
            filters={filters.data}
            onChange={update}
            scope={scope}
            locked={locked}
          />
        </div>
      </Drawer>
    </Container>
  );
}

function Filters({
  state,
  filters,
  onChange,
  scope = false,
  locked = {},
}: {
  state: ListingState;
  filters?: PublicCatalogFilters;
  onChange: (next: Partial<ListingState>) => void;
  scope?: boolean;
  locked?: Partial<ListingState>;
}) {
  const categories = useQuery({
    queryKey: ['categories'],
    queryFn: () => catalog.categories(),
    enabled: scope,
  });
  const collections = useQuery({
    queryKey: ['collections'],
    queryFn: () => catalog.collections(),
    enabled: scope,
  });
  const active = [
    state.color,
    state.size,
    state.minPrice,
    state.maxPrice,
    state.available ? 'available' : null,
  ].filter(Boolean);
  return (
    <form
      key={`${state.minPrice ?? ''}:${state.maxPrice ?? ''}`}
      className="flex flex-col gap-6 text-sm"
      onSubmit={(event) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        onChange({
          minPrice: String(data.get('minPrice') || '') || undefined,
          maxPrice: String(data.get('maxPrice') || '') || undefined,
        });
      }}
    >
      {scope ? (
        <>
          <label className="flex flex-col gap-2">
            Category
            <select
              className="min-h-11 border border-border bg-background px-3"
              value={state.category ?? ''}
              onChange={(event) => onChange({ category: event.target.value || undefined })}
            >
              <option value="">All categories</option>
              {flatten(categories.data ?? []).map((category) => (
                <option key={category.slug} value={category.slug}>
                  {category.name}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-2">
            Collection
            <select
              className="min-h-11 border border-border bg-background px-3"
              value={state.collection ?? ''}
              onChange={(event) => onChange({ collection: event.target.value || undefined })}
            >
              <option value="">All collections</option>
              {(collections.data ?? []).map((collection) => (
                <option key={collection.slug} value={collection.slug}>
                  {collection.name}
                </option>
              ))}
            </select>
          </label>
        </>
      ) : null}
      <label className="flex flex-col gap-2">
        Sort
        <select
          className="min-h-11 border border-border bg-background px-3"
          value={state.sort}
          onChange={(event) => onChange({ sort: event.target.value as ListingState['sort'] })}
        >
          <option value="newest">Newest</option>
          <option value="price_asc">Price, low to high</option>
          <option value="price_desc">Price, high to low</option>
          <option value="name">Name</option>
        </select>
      </label>
      <ChoiceGroup
        label="Color"
        choices={filters?.colors ?? []}
        selected={state.color}
        onSelect={(color) => onChange({ color })}
      />
      <ChoiceGroup
        label="Size"
        choices={filters?.sizes ?? []}
        selected={state.size}
        onSelect={(size) => onChange({ size })}
      />
      <fieldset className="flex gap-2">
        <legend className="mb-2">Price</legend>
        <input
          name="minPrice"
          defaultValue={state.minPrice ?? ''}
          inputMode="decimal"
          aria-label="Minimum price"
          placeholder={filters?.minPrice ?? 'Min'}
          className="min-h-11 w-full border border-border px-3"
        />
        <input
          name="maxPrice"
          defaultValue={state.maxPrice ?? ''}
          inputMode="decimal"
          aria-label="Maximum price"
          placeholder={filters?.maxPrice ?? 'Max'}
          className="min-h-11 w-full border border-border px-3"
        />
      </fieldset>
      <Button type="submit" variant="secondary">
        Apply price
      </Button>
      <label className="flex min-h-11 items-center gap-2">
        <input
          type="checkbox"
          checked={state.available}
          onChange={(event) => onChange({ available: event.target.checked })}
        />
        In stock
      </label>
      {active.length > 0 ? (
        <button
          type="button"
          className="min-h-11 text-left underline-offset-4 hover:underline"
          onClick={() =>
            onChange({
              color: undefined,
              size: undefined,
              minPrice: undefined,
              maxPrice: undefined,
              available: false,
              category: locked.category,
              collection: locked.collection,
            })
          }
        >
          Clear filters
        </button>
      ) : null}
    </form>
  );
}

function ChoiceGroup({
  label,
  choices,
  selected,
  onSelect,
}: {
  label: string;
  choices: PublicCatalogFilters['colors'];
  selected?: string;
  onSelect: (value: string | undefined) => void;
}) {
  if (choices.length === 0) return null;
  return (
    <fieldset>
      <legend className="mb-2">{label}</legend>
      <div className="flex flex-wrap gap-2">
        {choices.map((choice) => (
          <button
            key={choice.value}
            type="button"
            aria-pressed={selected === choice.value}
            className="min-h-11 border border-border px-3 aria-pressed:bg-foreground aria-pressed:text-background"
            onClick={() => onSelect(selected === choice.value ? undefined : choice.value)}
          >
            {choice.label}
          </button>
        ))}
      </div>
    </fieldset>
  );
}

export function CategoryListing({ slug }: { slug: string }) {
  const navigate = useNavigate();
  const category = useQuery({
    queryKey: ['category', slug],
    queryFn: () => catalog.category(slug),
  });
  useEffect(() => {
    if (category.data && category.data.slug !== slug) {
      navigate(`/category/${category.data.slug}${window.location.search}`, { replace: true });
    }
  }, [category.data, navigate, slug]);
  const tree = useQuery({ queryKey: ['categories'], queryFn: () => catalog.categories() });
  const crumbs = useMemo(
    () => breadcrumbs(tree.data ?? [], category.data?.slug),
    [tree.data, category.data?.slug],
  );
  if (isApiError(category.error) && category.error.status === 404) return <NotFoundPage />;
  if (!category.data) return <Container className="py-16">Loading</Container>;
  return (
    <>
      <Container className="pt-6">
        <nav
          aria-label="Breadcrumb"
          className="text-xs tracking-editorial text-muted-foreground uppercase"
        >
          <Link to="/shop">Shop</Link>
          {crumbs.map((crumb) => (
            <span key={crumb.slug}>
              {' / '}
              <Link to={`/category/${crumb.slug}`}>{crumb.name}</Link>
            </span>
          ))}
        </nav>
      </Container>
      <ListingPage
        title={category.data.name}
        description={category.data.description}
        locked={{ category: category.data.slug }}
        path={`/category/${category.data.slug}`}
      />
    </>
  );
}

function flatten(nodes: CategoryNode[], depth = 0): Array<{ slug: string; name: string }> {
  return nodes.flatMap((node) => [
    { slug: node.slug, name: `${'· '.repeat(depth)}${node.name}` },
    ...flatten(node.children, depth + 1),
  ]);
}

function breadcrumbs(nodes: CategoryNode[], slug?: string): CategoryNode[] {
  if (!slug) return [];
  const path: CategoryNode[] = [];
  function walk(items: CategoryNode[], trail: CategoryNode[]): boolean {
    for (const item of items) {
      const next = [...trail, item];
      if (item.slug === slug) {
        path.push(...next);
        return true;
      }
      if (walk(item.children, next)) return true;
    }
    return false;
  }
  walk(nodes, []);
  return path;
}
