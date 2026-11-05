import { useQuery } from '@tanstack/react-query'
import { ApiError, UNKNOWN_ERROR_CODE } from '@/lib/api/apiError'
import { api } from '@/lib/api/client'
import { queryKeys } from '@/lib/api/queryKeys'

/** `GET /api/dashboard`: one call, sections the caller may not have come back as null. */
export function useDashboard() {
  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: async () => {
      const { data } = await api.GET('/api/dashboard')
      if (!data) throw new ApiError({ status: 200, code: UNKNOWN_ERROR_CODE })
      return data
    },
  })
}
