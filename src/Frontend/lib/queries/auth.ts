'use client';

import { useQuery } from '@tanstack/react-query';
import { getCurrentUser } from '../api/auth';
import type { CurrentUser } from '../../types/auth';

export function useCurrentUser(initialUser?: CurrentUser) {
  return useQuery({
    queryKey: ['current-user'],
    queryFn: getCurrentUser,
    initialData: initialUser,
    retry: false,
    staleTime: 60_000,
  });
}