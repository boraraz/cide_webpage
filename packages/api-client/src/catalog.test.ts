import { describe, expect, it, vi } from 'vitest';
import { createCatalogApi } from './catalog';
import { createApiClient } from './client';
import { createCsrfMemory } from './csrf';

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('createCatalogApi', () => {
  it('keeps money as decimal strings in the product query and variant body', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse({ items: [], page: 1, pageSize: 24, total: 0 }))
      .mockResolvedValueOnce(jsonResponse({ token: 'csrf-token' }))
      .mockResolvedValueOnce(
        jsonResponse({ id: 'variant-1', price: '1299.00', currency: 'TRY' }, 201),
      );
    const csrf = createCsrfMemory();
    const client = createApiClient({
      baseUrl: 'http://api.test',
      fetch: fetchMock,
      prepareHeaders: csrf.prepareHeaders,
    });
    const catalog = createCatalogApi(client, csrf);

    await catalog.products({
      category: 'women',
      color: 'navy',
      size: 'm',
      minPrice: '1299.00',
      maxPrice: '2499.50',
      available: true,
      sort: 'price_asc',
      page: 2,
      pageSize: 24,
      q: 'tee',
    });

    const listUrl = String(fetchMock.mock.calls[0]![0]);
    const params = new URL(listUrl).searchParams;
    expect(params.get('minPrice')).toBe('1299.00');
    expect(params.get('maxPrice')).toBe('2499.50');
    expect(params.get('category')).toBe('women');
    expect(params.get('color')).toBe('navy');
    expect(params.get('size')).toBe('m');
    expect(params.get('available')).toBe('true');
    expect(params.get('sort')).toBe('price_asc');
    expect(params.get('page')).toBe('2');
    expect(params.get('q')).toBe('tee');
    expect(listUrl).toContain('minPrice=1299.00');

    await catalog.createVariant('product-1', {
      sku: 'TEE-NVY-M',
      barcode: null,
      price: '1299.00',
      compareAtPrice: '1599.00',
      isActive: true,
      optionValueIds: ['color-navy', 'size-m'],
      initialQuantity: 4,
    });

    const create = fetchMock.mock.calls[2]![1]!;
    const body = JSON.parse(String(create.body)) as { price: string; compareAtPrice: string };
    expect(body.price).toBe('1299.00');
    expect(body.compareAtPrice).toBe('1599.00');
    expect(typeof body.price).toBe('string');
    expect(new Headers(create.headers).get('X-CSRF-TOKEN')).toBe('csrf-token');
  });

  it('sends the inventory idempotency key as a header', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse({ token: 'csrf-token' }))
      .mockResolvedValueOnce(jsonResponse({ id: 'movement-1', quantityDelta: 2 }));
    const csrf = createCsrfMemory();
    const client = createApiClient({
      baseUrl: 'http://api.test',
      fetch: fetchMock,
      prepareHeaders: csrf.prepareHeaders,
    });

    await createCatalogApi(client, csrf).adjustInventory('variant-1', 'idempotency-key-01', {
      movementType: 'manualIncrease',
      quantity: 2,
      note: null,
    });

    const adjustment = fetchMock.mock.calls[1]![1]!;
    expect(String(fetchMock.mock.calls[1]![0])).toBe(
      'http://api.test/api/v1/admin/inventory/variant-1/adjustments',
    );
    expect(new Headers(adjustment.headers).get('Idempotency-Key')).toBe('idempotency-key-01');
  });
});
