import { isApiError } from '@commerce/api-client';

export function problemMessage(error: unknown): string {
  if (!isApiError(error)) return 'Something went wrong. Try again.';
  const first = error.validationErrors ? Object.values(error.validationErrors)[0]?.[0] : undefined;
  return first ?? error.detail ?? error.title;
}

export function currentVersion(error: unknown): number | null {
  if (!isApiError(error) || error.status !== 409) return null;
  const body = error.body;
  if (typeof body === 'object' && body && 'currentVersion' in body) {
    const version = body.currentVersion;
    return typeof version === 'number' ? version : null;
  }
  return null;
}
