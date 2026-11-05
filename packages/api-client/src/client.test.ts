import { describe, expect, it, vi } from 'vitest';
import { createApiClient } from './client';
import { ApiError } from './errors';

function jsonResponse(body: unknown, init: ResponseInit = {}, contentType = 'application/json') {
  return new Response(JSON.stringify(body), {
    ...init,
    headers: { 'Content-Type': contentType, ...init.headers },
  });
}

describe('createApiClient', () => {
  it('builds the URL, sends credentials and parses JSON', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ status: 'Healthy' }));
    const client = createApiClient({ baseUrl: 'http://api.test/', fetch: fetchMock });

    const result = await client.get<{ status: string }>('/api/v1/health', {
      query: { page: 2, empty: undefined },
    });

    expect(result).toEqual({ status: 'Healthy' });
    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('http://api.test/api/v1/health?page=2');
    expect(init?.credentials).toBe('include');
    expect(init?.method).toBe('GET');
  });

  it('serializes JSON bodies and applies prepareHeaders', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }));
    const client = createApiClient({
      baseUrl: 'http://api.test',
      fetch: fetchMock,
      prepareHeaders: (headers) => headers.set('X-Test', 'yes'),
    });

    const result = await client.post('/things', { name: 'a' });

    expect(result).toBeUndefined();
    const init = fetchMock.mock.calls[0]![1]!;
    const headers = new Headers(init.headers);
    expect(init.body).toBe('{"name":"a"}');
    expect(headers.get('Content-Type')).toBe('application/json');
    expect(headers.get('X-Test')).toBe('yes');
  });

  it('normalizes ProblemDetails responses into ApiError', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse(
        {
          title: 'Validation failed',
          status: 400,
          detail: 'Name is required.',
          traceId: 'abc',
          errors: { name: ['Required'] },
        },
        { status: 400 },
        'application/problem+json',
      ),
    );
    const client = createApiClient({ baseUrl: 'http://api.test', fetch: fetchMock });

    const error = await client.get('/x').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      kind: 'http',
      status: 400,
      title: 'Validation failed',
      detail: 'Name is required.',
      traceId: 'abc',
      validationErrors: { name: ['Required'] },
    });
  });

  it('normalizes network failures into ApiError', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockRejectedValue(new TypeError('Failed to fetch'));
    const client = createApiClient({ baseUrl: 'http://api.test', fetch: fetchMock });

    await expect(client.get('/x')).rejects.toMatchObject({ kind: 'network', status: 0 });
  });

  it('rethrows aborts untouched', async () => {
    const controller = new AbortController();
    // Mirrors fetch: rejects immediately for an already-aborted signal, or when it aborts later.
    const fetchMock = vi.fn<typeof fetch>((_url, init) => {
      return new Promise((_resolve, reject) => {
        const rejectAbort = () => reject(new DOMException('Aborted', 'AbortError'));
        if (init?.signal?.aborted) rejectAbort();
        init?.signal?.addEventListener('abort', rejectAbort);
      });
    });
    const client = createApiClient({ baseUrl: 'http://api.test', fetch: fetchMock });

    const pending = client.get('/slow', { signal: controller.signal });
    controller.abort();

    await expect(pending).rejects.toMatchObject({ name: 'AbortError' });
    await expect(pending).rejects.not.toBeInstanceOf(ApiError);
  });
});
