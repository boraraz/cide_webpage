import {
  createApiClient,
  createAuthApi,
  createCatalogApi,
  createContentApi,
  createCsrfMemory,
} from '@commerce/api-client';
import { env } from './env';

const csrf = createCsrfMemory();

export const api = createApiClient({
  baseUrl: env.VITE_API_BASE_URL,
  prepareHeaders: csrf.prepareHeaders,
});

export const auth = createAuthApi(api, csrf);
export const catalog = createCatalogApi(api, csrf);
export const content = createContentApi(api, csrf);
