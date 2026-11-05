import { QueryClient } from '@tanstack/react-query'

/** Query defaults from docs/UI_SPEC.md §6. FE-01 adds "never retry 4xx" once ApiError exists. */
export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: true },
      mutations: { retry: 0 },
    },
  })
}
