import { isApiError } from '@commerce/api-client';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import type { UserResponse } from '@commerce/shared-types';
import { auth } from '@/lib/api';

export const adminSessionQueryKey = ['admin-session'] as const;

export function useAdminSession() {
  return useQuery({
    queryKey: adminSessionQueryKey,
    queryFn: () => auth.adminCurrentUser(),
    retry: false,
  });
}

export function useAdminSessionCache() {
  const queryClient = useQueryClient();
  return {
    remember(user: UserResponse) {
      queryClient.setQueryData(adminSessionQueryKey, user);
    },
    async signOut() {
      await auth.logout();
      await queryClient.resetQueries({ queryKey: adminSessionQueryKey });
    },
  };
}

export function adminFailureStatus(error: unknown): number | undefined {
  return isApiError(error) ? error.status : undefined;
}
