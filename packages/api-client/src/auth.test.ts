import { describe, expect, it, vi } from 'vitest';
import { createAuthApi } from './auth';
import { createApiClient } from './client';
import { createCsrfMemory } from './csrf';

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('createAuthApi', () => {
  it('keeps the CSRF token in memory and sends it on unsafe requests', async () => {
    const storage = { setItem: vi.fn(), getItem: vi.fn() };
    vi.stubGlobal('localStorage', storage);
    vi.stubGlobal('sessionStorage', storage);

    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse({ token: 'csrf-token' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    const csrf = createCsrfMemory();
    const client = createApiClient({
      baseUrl: 'http://api.test',
      fetch: fetchMock,
      prepareHeaders: csrf.prepareHeaders,
    });

    await createAuthApi(client, csrf).logout();

    const csrfCall = fetchMock.mock.calls[0]!;
    expect(csrfCall[0]).toBe('http://api.test/api/v1/auth/csrf');
    const logout = fetchMock.mock.calls[1]![1]!;
    expect(new Headers(logout.headers).get('X-CSRF-TOKEN')).toBe('csrf-token');
    expect(storage.setItem).not.toHaveBeenCalled();
    expect(storage.getItem).not.toHaveBeenCalled();
  });

  it('treats 401 from the session endpoint as signed out', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(jsonResponse({ title: 'Unauthorized', status: 401 }, 401));
    const client = createApiClient({ baseUrl: 'http://api.test', fetch: fetchMock });

    await expect(createAuthApi(client, createCsrfMemory()).currentUser()).resolves.toBeNull();
  });

  it('requests a fresh CSRF token once after the server rejects the previous one', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse({ token: 'old' }))
      .mockResolvedValueOnce(
        jsonResponse({ title: 'The request could not be validated.', status: 400 }, 400),
      )
      .mockResolvedValueOnce(jsonResponse({ token: 'new' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    const csrf = createCsrfMemory();
    const client = createApiClient({
      baseUrl: 'http://api.test',
      fetch: fetchMock,
      prepareHeaders: csrf.prepareHeaders,
    });

    await createAuthApi(client, csrf).logout();

    expect(new Headers(fetchMock.mock.calls[3]![1]!.headers).get('X-CSRF-TOKEN')).toBe('new');
  });
});
