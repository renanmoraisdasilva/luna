import { useQuery } from '@tanstack/react-query';
import { getSwaggerSpec } from '../api/swagger';

export const swaggerKeys = {
  all: ['swagger'] as const,
  spec: (selectedSpec: string) => ['swagger', 'spec', selectedSpec] as const,
};

export function useSwaggerQuery(selectedSpec: string) {
  return useQuery({
    queryKey: swaggerKeys.spec(selectedSpec),
    queryFn: () => getSwaggerSpec(selectedSpec),
    staleTime: 5 * 60 * 1000,
  });
}
