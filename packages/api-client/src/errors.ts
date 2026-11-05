/** RFC 9457 problem details, as returned by the ASP.NET Core API. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  [extension: string]: unknown;
}

export type ApiErrorKind = 'http' | 'network' | 'parse';

export interface ApiErrorInit {
  kind: ApiErrorKind;
  status: number;
  title: string;
  detail?: string | undefined;
  traceId?: string | undefined;
  validationErrors?: Record<string, string[]> | undefined;
  body?: unknown;
  cause?: unknown;
}

/** The single error type thrown by the API client, whatever went wrong. */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  /** HTTP status, or 0 when no response was received. */
  readonly status: number;
  readonly title: string;
  readonly detail: string | undefined;
  readonly traceId: string | undefined;
  readonly validationErrors: Record<string, string[]> | undefined;
  /** Parsed response body, when there was one. */
  readonly body: unknown;

  constructor(init: ApiErrorInit) {
    super(init.detail ?? init.title, { cause: init.cause });
    this.name = 'ApiError';
    this.kind = init.kind;
    this.status = init.status;
    this.title = init.title;
    this.detail = init.detail;
    this.traceId = init.traceId;
    this.validationErrors = init.validationErrors;
    this.body = init.body;
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== 'object' || value === null) return false;
  const candidate = value as Record<string, unknown>;
  return typeof candidate.title === 'string' || typeof candidate.status === 'number';
}

export function createHttpError(response: Response, body: unknown): ApiError {
  if (isProblemDetails(body)) {
    return new ApiError({
      kind: 'http',
      status: response.status,
      title: body.title ?? response.statusText,
      detail: body.detail,
      traceId: body.traceId,
      validationErrors: body.errors,
      body,
    });
  }

  return new ApiError({
    kind: 'http',
    status: response.status,
    title: response.statusText || `Request failed with status ${response.status}`,
    body,
  });
}
