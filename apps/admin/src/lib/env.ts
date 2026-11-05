import { z } from 'zod';

/** Public, build-time configuration only. Secrets never belong in VITE_ variables. */
const envSchema = z.object({
  VITE_API_BASE_URL: z.url(),
});

export type Env = z.infer<typeof envSchema>;

function parseEnv(source: Record<string, unknown>): Env {
  const result = envSchema.safeParse(source);
  if (!result.success) {
    throw new Error(
      `Invalid admin environment configuration. Copy apps/admin/.env.example to .env.local.\n${z.prettifyError(result.error)}`,
    );
  }
  return result.data;
}

export const env = parseEnv(import.meta.env);
