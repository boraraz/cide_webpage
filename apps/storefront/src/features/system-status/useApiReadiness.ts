import { isApiError } from '@commerce/api-client';
import type { ReadinessResponse } from '@commerce/shared-types';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

async function fetchReadiness(signal: AbortSignal): Promise<ReadinessResponse> {
  try {
    return await api.get<ReadinessResponse>('/api/v1/health/ready', { signal });
  } catch (error) {
    // 503 still carries a readiness report describing which dependency is down.
    if (isApiError(error) && error.status === 503 && error.body) {
      return error.body as ReadinessResponse;
    }
    throw error;
  }
}

export function useApiReadiness() {
  return useQuery({
    queryKey: ['system', 'readiness'],
    queryFn: ({ signal }) => fetchReadiness(signal),
    refetchInterval: 30_000,
    retry: false,
  });
}
