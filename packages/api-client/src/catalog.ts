import type {
  AdminProductPage,
  CategoryNode,
  CategoryResponse,
  CategoryUpdateRequest,
  CategoryWriteRequest,
  CollectionResponse,
  CollectionUpdateRequest,
  CollectionWriteRequest,
  InventoryPage,
  MediaResponse,
  MediaUpdateRequest,
  MovementResponse,
  OptionResponse,
  OptionValueResponse,
  OptionValueWriteRequest,
  OptionWriteRequest,
  ProductPage,
  ProductResponse,
  ProductUpdateRequest,
  ProductWriteRequest,
  PublicProductDetail,
  StockAdjustmentRequest,
  VariantResponse,
  VariantUpdateRequest,
  VariantWriteRequest,
} from '@commerce/shared-types';
import type { ApiClient, QueryValue } from './client';
import type { CsrfMemory } from './csrf';
import { isApiError } from './errors';

export type ProductSort = 'newest' | 'price_asc' | 'price_desc';

/** Prices stay decimal strings such as `"1299.00"`. Callers must not pass numbers. */
export interface ProductListQuery {
  page?: number;
  pageSize?: number;
  category?: string;
  collection?: string;
  color?: string;
  size?: string;
  minPrice?: string;
  maxPrice?: string;
  available?: boolean;
  q?: string;
  sort?: ProductSort;
}

export interface AdminProductQuery {
  page?: number;
  pageSize?: number;
  status?: string;
  q?: string;
  categoryId?: string;
  collectionId?: string;
  sort?: 'updated' | 'name';
}

export interface InventoryQuery {
  page?: number;
  pageSize?: number;
  sku?: string;
  q?: string;
  stock?: 'all' | 'in_stock' | 'low' | 'out';
}

export interface MovementHistoryQuery {
  page?: number;
  pageSize?: number;
  sku?: string;
  variantId?: string;
}

export interface DashboardSummary {
  totalProducts: number;
  publishedProducts: number;
  draftProducts: number;
  archivedProducts: number;
  activeVariants: number;
  outOfStockVariants: number;
  lowStockVariants: number;
  categories: number;
  collections: number;
  lowStockThreshold: number;
}

export interface VariantBulkItem {
  sku?: string;
  barcode?: string;
  price: string;
  compareAtPrice?: string;
  isActive?: boolean;
  optionValueIds: string[];
  initialQuantity: number;
}

export interface MediaUpload {
  file: Blob;
  fileName: string;
  altText: string;
  variantId?: string;
  displayOrder?: number;
  isPrimary?: boolean;
}

export function adminProductQuery(query: AdminProductQuery): Record<string, QueryValue> {
  return {
    page: query.page,
    pageSize: query.pageSize,
    status: query.status,
    q: query.q,
    categoryId: query.categoryId,
    collectionId: query.collectionId,
    sort: query.sort,
  };
}

export function inventoryQuery(query: InventoryQuery): Record<string, QueryValue> {
  return {
    page: query.page,
    pageSize: query.pageSize,
    sku: query.sku,
    q: query.q,
    stock: query.stock,
  };
}

export function productListQuery(query: ProductListQuery): Record<string, QueryValue> {
  return {
    page: query.page,
    pageSize: query.pageSize,
    category: query.category,
    collection: query.collection,
    color: query.color,
    size: query.size,
    minPrice: query.minPrice,
    maxPrice: query.maxPrice,
    available: query.available,
    q: query.q,
    sort: query.sort,
  };
}

export interface CatalogApi {
  categories: () => Promise<CategoryNode[]>;
  category: (slug: string) => Promise<CategoryNode>;
  collections: () => Promise<CollectionResponse[]>;
  collection: (slug: string) => Promise<CollectionResponse>;
  products: (query?: ProductListQuery) => Promise<ProductPage>;
  product: (slug: string) => Promise<PublicProductDetail>;
  adminCategories: () => Promise<CategoryNode[]>;
  createCategory: (body: CategoryWriteRequest) => Promise<CategoryResponse>;
  updateCategory: (id: string, body: CategoryUpdateRequest) => Promise<CategoryResponse>;
  deleteCategory: (id: string) => Promise<void>;
  adminCollections: () => Promise<CollectionResponse[]>;
  createCollection: (body: CollectionWriteRequest) => Promise<CollectionResponse>;
  updateCollection: (id: string, body: CollectionUpdateRequest) => Promise<CollectionResponse>;
  deleteCollection: (id: string) => Promise<void>;
  adminProducts: (query?: AdminProductQuery) => Promise<AdminProductPage>;
  adminProduct: (id: string) => Promise<ProductResponse>;
  createProduct: (body: ProductWriteRequest) => Promise<ProductResponse>;
  updateProduct: (
    id: string,
    body: ProductUpdateRequest & { expectedVersion?: number },
  ) => Promise<ProductResponse>;
  publishProduct: (id: string, expectedVersion?: number) => Promise<ProductResponse>;
  archiveProduct: (id: string, expectedVersion?: number) => Promise<ProductResponse>;
  createOption: (
    productId: string,
    body: OptionWriteRequest & { expectedVersion?: number },
  ) => Promise<OptionResponse>;
  createOptionValue: (
    optionId: string,
    body: OptionValueWriteRequest & { expectedVersion?: number },
  ) => Promise<OptionValueResponse>;
  createVariant: (productId: string, body: VariantWriteRequest) => Promise<VariantResponse>;
  updateVariant: (id: string, body: VariantUpdateRequest) => Promise<VariantResponse>;
  inventory: (query?: InventoryQuery) => Promise<InventoryPage>;
  movements: (variantId: string) => Promise<MovementResponse[]>;
  adjustInventory: (
    variantId: string,
    idempotencyKey: string,
    body: StockAdjustmentRequest,
  ) => Promise<MovementResponse>;
  uploadMedia: (productId: string, upload: MediaUpload) => Promise<MediaResponse>;
  updateMedia: (id: string, body: MediaUpdateRequest) => Promise<MediaResponse>;
  deleteMedia: (id: string, expectedVersion?: number) => Promise<void>;
  dashboard: () => Promise<DashboardSummary>;
  createVariants: (
    productId: string,
    body: { variants: VariantBulkItem[]; expectedVersion?: number },
  ) => Promise<VariantResponse[]>;
  orderMedia: (
    productId: string,
    body: { ids: string[]; expectedVersion?: number },
  ) => Promise<ProductResponse>;
  assignCollectionProduct: (
    collectionId: string,
    body: { productId: string; expectedVersion?: number },
  ) => Promise<void>;
  removeCollectionProduct: (
    collectionId: string,
    productId: string,
    expectedVersion?: number,
  ) => Promise<void>;
  movementHistory: (query?: MovementHistoryQuery) => Promise<{
    items: Array<
      MovementResponse & {
        productId: string;
        productName: string;
        sku: string;
        optionSummary: string;
        actorEmail: string | null;
      }
    >;
    page: number;
    pageSize: number;
    total: number;
  }>;
  adminMediaBlob: (id: string) => Promise<Blob>;
}

export function createCatalogApi(client: ApiClient, csrf: CsrfMemory): CatalogApi {
  async function send<T>(
    method: 'post' | 'patch' | 'put' | 'delete',
    path: string,
    body?: unknown,
    headers?: HeadersInit,
  ): Promise<T> {
    await csrf.ensure(client);
    const run = () => {
      if (method === 'post') return client.post<T>(path, body, { headers });
      if (method === 'patch') return client.patch<T>(path, body, { headers });
      if (method === 'put') return client.put<T>(path, body, { headers });
      return client.delete<T>(path, { headers });
    };
    try {
      return await run();
    } catch (error) {
      if (!isStaleCsrf(error)) throw error;
      csrf.clear();
      await csrf.ensure(client);
      return run();
    }
  }

  return {
    categories: () => client.get<CategoryNode[]>('/api/v1/categories'),
    category: (slug) => client.get<CategoryNode>(`/api/v1/categories/${encodeURIComponent(slug)}`),
    collections: () => client.get<CollectionResponse[]>('/api/v1/collections'),
    collection: (slug) =>
      client.get<CollectionResponse>(`/api/v1/collections/${encodeURIComponent(slug)}`),
    products: (query = {}) =>
      client.get<ProductPage>('/api/v1/products', { query: productListQuery(query) }),
    product: (slug) =>
      client.get<PublicProductDetail>(`/api/v1/products/${encodeURIComponent(slug)}`),
    adminCategories: () => client.get<CategoryNode[]>('/api/v1/admin/categories'),
    createCategory: (body) => send('post', '/api/v1/admin/categories', body),
    updateCategory: (id, body) => send('patch', `/api/v1/admin/categories/${id}`, body),
    deleteCategory: (id) => send('delete', `/api/v1/admin/categories/${id}`),
    adminCollections: () => client.get<CollectionResponse[]>('/api/v1/admin/collections'),
    createCollection: (body) => send('post', '/api/v1/admin/collections', body),
    updateCollection: (id, body) => send('patch', `/api/v1/admin/collections/${id}`, body),
    deleteCollection: (id) => send('delete', `/api/v1/admin/collections/${id}`),
    adminProducts: (query = {}) =>
      client.get<AdminProductPage>('/api/v1/admin/products', { query: adminProductQuery(query) }),
    adminProduct: (id) => client.get<ProductResponse>(`/api/v1/admin/products/${id}`),
    createProduct: (body) => send('post', '/api/v1/admin/products', body),
    updateProduct: (id, body) => send('patch', `/api/v1/admin/products/${id}`, body),
    publishProduct: (id, expectedVersion) =>
      send('post', `/api/v1/admin/products/${id}/publish`, { expectedVersion }),
    archiveProduct: (id, expectedVersion) =>
      send('post', `/api/v1/admin/products/${id}/archive`, { expectedVersion }),
    createOption: (productId, body) =>
      send('post', `/api/v1/admin/products/${productId}/options`, body),
    createOptionValue: (optionId, body) =>
      send('post', `/api/v1/admin/options/${optionId}/values`, body),
    createVariant: (productId, body) =>
      send('post', `/api/v1/admin/products/${productId}/variants`, body),
    updateVariant: (id, body) => send('patch', `/api/v1/admin/variants/${id}`, body),
    inventory: (query = {}) =>
      client.get<InventoryPage>('/api/v1/admin/inventory', { query: inventoryQuery(query) }),
    movements: (variantId) =>
      client.get<MovementResponse[]>(`/api/v1/admin/inventory/${variantId}/movements`),
    adjustInventory: (variantId, idempotencyKey, body) =>
      send('post', `/api/v1/admin/inventory/${variantId}/adjustments`, body, {
        'Idempotency-Key': idempotencyKey,
      }),
    uploadMedia: (productId, upload) => {
      const form = new FormData();
      form.set('file', upload.file, upload.fileName);
      form.set('altText', upload.altText);
      if (upload.variantId) form.set('variantId', upload.variantId);
      if (upload.displayOrder !== undefined) form.set('displayOrder', String(upload.displayOrder));
      if (upload.isPrimary !== undefined) form.set('isPrimary', String(upload.isPrimary));
      return send('post', `/api/v1/admin/products/${productId}/media`, form);
    },
    updateMedia: (id, body) => send('patch', `/api/v1/admin/media/${id}`, body),
    deleteMedia: (id, expectedVersion) =>
      send(
        'delete',
        `/api/v1/admin/media/${id}${expectedVersion === undefined ? '' : `?expectedVersion=${expectedVersion}`}`,
      ),
    dashboard: () => client.get<DashboardSummary>('/api/v1/admin/dashboard/summary'),
    createVariants: (productId, body) =>
      send('post', `/api/v1/admin/products/${productId}/variants/bulk`, body),
    orderMedia: (productId, body) =>
      send('put', `/api/v1/admin/products/${productId}/media/order`, body),
    assignCollectionProduct: (collectionId, body) =>
      send('post', `/api/v1/admin/collections/${collectionId}/products`, body),
    removeCollectionProduct: (collectionId, productId, expectedVersion) =>
      send(
        'delete',
        `/api/v1/admin/collections/${collectionId}/products/${productId}${
          expectedVersion === undefined ? '' : `?expectedVersion=${expectedVersion}`
        }`,
      ),
    movementHistory: (query = {}) =>
      client.get('/api/v1/admin/inventory/movements', {
        query: {
          page: query.page,
          pageSize: query.pageSize,
          sku: query.sku,
          variantId: query.variantId,
        },
      }),
    adminMediaBlob: (id) => client.blob(`/api/v1/admin/media/${id}`),
  };
}

function isStaleCsrf(error: unknown): boolean {
  return (
    isApiError(error) &&
    error.status === 400 &&
    error.title === 'The request could not be validated.'
  );
}
