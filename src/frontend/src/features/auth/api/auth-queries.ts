import { queryOptions, useQuery } from '@tanstack/react-query';
import { getCurrentUser } from './auth-api';

export const authKeys = {
  currentUser: ['auth', 'current-user'] as const,
};

export const currentUserOptions = queryOptions({
  queryKey: authKeys.currentUser,
  queryFn: ({ signal }) => getCurrentUser(signal),
  retry: false,
  staleTime: 60_000,
});

export function useCurrentUser() {
  return useQuery(currentUserOptions);
}
