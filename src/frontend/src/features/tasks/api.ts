import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api/client'
import { queryKeys } from '@/lib/api/queryKeys'

const OPEN_COUNT_PARAMS = { Status: 'Open', Page: 1, PageSize: 1 } as const

/**
 * How many tasks wait for the signed-in user (the sidebar's Inbox badge): `totalCount` of a
 * one-row page of `GET /api/tasks?status=Open`. Pass `enabled: false` without `tasks.view`.
 */
export function useOpenTaskCount(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.tasks.list(OPEN_COUNT_PARAMS),
    queryFn: async () => {
      const { data } = await api.GET('/api/tasks', { params: { query: OPEN_COUNT_PARAMS } })
      return data?.totalCount ?? 0
    },
    enabled,
  })
}
