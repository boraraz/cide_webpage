import { useQuery, useQueryClient } from '@tanstack/react-query';
import type { UserResponse } from '@commerce/shared-types';
import { auth } from '@/lib/api';

export const sessionQueryKey = ['session'] as const;

export function useSession() {
  return useQuery({
    queryKey: sessionQueryKey,
    queryFn: () => auth.currentUser(),
  });
}

export function useSessionCache() {
  const queryClient = useQueryClient();
  return {
    remember(user: UserResponse) {
      queryClient.setQueryData(sessionQueryKey, user);
    },
    async signOut() {
      await auth.logout();
      queryClient.setQueryData(sessionQueryKey, null);
    },
    clear() {
      queryClient.setQueryData(sessionQueryKey, null);
    },
  };
}
