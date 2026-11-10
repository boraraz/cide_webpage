import type { PriceBasis, ProductSort } from '@commerce/api-client';

export interface ListingState {
  q?: string;
  category?: string;
  collection?: string;
  color?: string;
  size?: string;
  minPrice?: string;
  maxPrice?: string;
  available: boolean;
  sort: ProductSort;
  page: number;
}

const sorts = new Set<ProductSort>(['newest', 'price_asc', 'price_desc', 'name']);

export function parseListing(
  params: URLSearchParams,
  locked: Partial<ListingState> = {},
): ListingState {
  const sort = params.get('sort');
  const page = Number(params.get('page') ?? '1');
  return {
    q: locked.q ?? (params.get('q') || undefined),
    category: locked.category ?? params.get('category') ?? undefined,
    collection: locked.collection ?? params.get('collection') ?? undefined,
    color: params.get('color') || undefined,
    size: params.get('size') || undefined,
    minPrice: params.get('minPrice') || undefined,
    maxPrice: params.get('maxPrice') || undefined,
    available: params.get('available') === 'true',
    sort: sort && sorts.has(sort as ProductSort) ? (sort as ProductSort) : 'newest',
    page: Number.isInteger(page) && page > 0 ? page : 1,
  };
}

export function listingSearch(state: ListingState, locked: Partial<ListingState> = {}): string {
  const params = new URLSearchParams();
  if (state.q) params.set('q', state.q);
  if (!locked.category && state.category) params.set('category', state.category);
  if (!locked.collection && state.collection) params.set('collection', state.collection);
  if (state.color) params.set('color', state.color);
  if (state.size) params.set('size', state.size);
  if (state.minPrice) params.set('minPrice', state.minPrice);
  if (state.maxPrice) params.set('maxPrice', state.maxPrice);
  if (state.available) params.set('available', 'true');
  params.set('priceBasis', 'from' satisfies PriceBasis);
  if (state.sort !== 'newest') params.set('sort', state.sort);
  if (state.page > 1) params.set('page', String(state.page));
  const text = params.toString();
  return text ? `?${text}` : '';
}

export function toProductQuery(state: ListingState) {
  return {
    q: state.q,
    category: state.category,
    collection: state.collection,
    color: state.color,
    size: state.size,
    minPrice: state.minPrice,
    maxPrice: state.maxPrice,
    available: state.available || undefined,
    sort: state.sort,
    page: state.page,
    pageSize: 12,
    priceBasis: 'from' as const,
  };
}
