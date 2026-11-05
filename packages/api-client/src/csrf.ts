import type { CsrfResponse } from '@commerce/shared-types';
import type { ApiClient } from './client';

const headerName = 'X-CSRF-TOKEN';

/**
 * CSRF request token kept in module memory for one API client.
 * It is never written to localStorage or sessionStorage.
 */
export interface CsrfMemory {
  prepareHeaders: (headers: Headers) => void;
  ensure: (client: ApiClient) => Promise<void>;
  clear: () => void;
}

export function createCsrfMemory(): CsrfMemory {
  let token: string | undefined;

  return {
    prepareHeaders(headers) {
      if (token) headers.set(headerName, token);
    },
    async ensure(client) {
      if (token) return;
      const response = await client.get<CsrfResponse>('/api/v1/auth/csrf');
      token = response.token;
    },
    clear() {
      token = undefined;
    },
  };
}
