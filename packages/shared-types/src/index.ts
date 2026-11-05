/*
 * API contract types generated from the backend OpenAPI document.
 * Regenerate with `npm run api:types` while the API is running; never edit generated/ by hand.
 */
import type { components } from './generated/api';

export type { components, operations, paths } from './generated/api';

type Schemas = components['schemas'];

export type HealthResponse = Schemas['HealthResponse'];
export type ReadinessResponse = Schemas['ReadinessResponse'];
export type DependencyHealth = Schemas['DependencyHealth'];
