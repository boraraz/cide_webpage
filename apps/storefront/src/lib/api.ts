import { createApiClient } from '@commerce/api-client';
import { env } from './env';

export const api = createApiClient({ baseUrl: env.VITE_API_BASE_URL });
