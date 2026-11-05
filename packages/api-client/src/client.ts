import { ApiError, createHttpError } from './errors';

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

export type QueryValue = string | number | boolean | null | undefined;

export interface ApiClientOptions {
  /** Absolute API origin, for example `http://localhost:5080`. */
  baseUrl: string;
  /**
   * Defaults to `include` so future HttpOnly session cookies are sent cross-origin.
   * The client never reads, stores or attaches tokens itself.
   */
  credentials?: RequestCredentials;
  /** Hook for per-request headers such as a future CSRF token. */
  prepareHeaders?: (headers: Headers) => void | Promise<void>;
  /** Injectable for tests. */
  fetch?: typeof fetch;
}

export interface RequestOptions extends Omit<RequestInit, 'body' | 'method'> {
  method?: HttpMethod;
  /** Serialized as JSON unless it is already a `BodyInit` such as `FormData`. */
  body?: unknown;
  query?: Record<string, QueryValue>;
}

export type MethodOptions = Omit<RequestOptions, 'method'>;

export interface ApiClient {
  request<T>(path: string, options?: RequestOptions): Promise<T>;
  get<T>(path: string, options?: MethodOptions): Promise<T>;
  post<T>(path: string, body?: unknown, options?: MethodOptions): Promise<T>;
  put<T>(path: string, body?: unknown, options?: MethodOptions): Promise<T>;
  patch<T>(path: string, body?: unknown, options?: MethodOptions): Promise<T>;
  delete<T>(path: string, options?: MethodOptions): Promise<T>;
}

export function createApiClient(options: ApiClientOptions): ApiClient {
  const baseUrl = options.baseUrl.replace(/\/+$/, '');
  const credentials = options.credentials ?? 'include';
  const fetchImpl: typeof fetch = options.fetch ?? ((input, init) => globalThis.fetch(input, init));

  async function request<T>(path: string, init: RequestOptions = {}): Promise<T> {
    const { query, body, headers: initHeaders, ...rest } = init;
    const headers = new Headers(initHeaders);
    headers.set('Accept', 'application/json');

    let requestBody: BodyInit | undefined;
    if (body !== undefined) {
      if (isBodyInit(body)) {
        requestBody = body;
      } else {
        headers.set('Content-Type', 'application/json');
        requestBody = JSON.stringify(body);
      }
    }

    await options.prepareHeaders?.(headers);

    let response: Response;
    try {
      response = await fetchImpl(buildUrl(baseUrl, path, query), {
        ...rest,
        method: init.method ?? 'GET',
        headers,
        body: requestBody ?? null,
        credentials,
      });
    } catch (error) {
      if (isAbortError(error)) throw error;
      throw new ApiError({
        kind: 'network',
        status: 0,
        title: 'Network error',
        detail: 'The API could not be reached.',
        cause: error,
      });
    }

    const parsed = await parseBody(response);

    if (!response.ok) {
      throw createHttpError(response, parsed);
    }

    return parsed as T;
  }

  return {
    request,
    get: (path, opts) => request(path, { ...opts, method: 'GET' }),
    post: (path, body, opts) => request(path, { ...opts, method: 'POST', body }),
    put: (path, body, opts) => request(path, { ...opts, method: 'PUT', body }),
    patch: (path, body, opts) => request(path, { ...opts, method: 'PATCH', body }),
    delete: (path, opts) => request(path, { ...opts, method: 'DELETE' }),
  };
}

function buildUrl(baseUrl: string, path: string, query?: Record<string, QueryValue>): string {
  const url = new URL(`${baseUrl}/${path.replace(/^\/+/, '')}`);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null) url.searchParams.set(key, String(value));
    }
  }
  return url.toString();
}

async function parseBody(response: Response): Promise<unknown> {
  if (response.status === 204 || response.status === 205) return undefined;

  const text = await response.text();
  if (text.length === 0) return undefined;

  const contentType = response.headers.get('Content-Type') ?? '';
  if (!/[/+]json\b/i.test(contentType)) return text;

  try {
    return JSON.parse(text) as unknown;
  } catch (error) {
    throw new ApiError({
      kind: 'parse',
      status: response.status,
      title: 'Invalid response',
      detail: 'The API returned malformed JSON.',
      cause: error,
    });
  }
}

function isBodyInit(value: unknown): value is BodyInit {
  return (
    typeof value === 'string' ||
    value instanceof FormData ||
    value instanceof URLSearchParams ||
    value instanceof Blob ||
    value instanceof ArrayBuffer ||
    ArrayBuffer.isView(value) ||
    value instanceof ReadableStream
  );
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}
