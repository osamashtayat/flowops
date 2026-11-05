import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '@/lib/api/apiError'

const MAX_QUERY_RETRIES = 1

/** Retry once for transient failures, never for 4xx: the same request would fail the same way. */
export function shouldRetryQuery(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false
  return failureCount < MAX_QUERY_RETRIES
}

/** Query defaults from docs/UI_SPEC.md §6. */
export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 30_000, retry: shouldRetryQuery, refetchOnWindowFocus: true },
      mutations: { retry: 0 },
    },
  })
}
