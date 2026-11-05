import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, UNKNOWN_ERROR_CODE } from '@/lib/api/apiError'
import { api } from '@/lib/api/client'
import { queryKeys } from '@/lib/api/queryKeys'
import type { RequestDetailsDto, RequestScope, RequestStatus, RequestType } from '@/lib/api/types'
import type { LeaveValues, PurchaseValues } from './schemas'

/** A submitted request changes the lists, the dashboard counts and (for the manager) their inbox. */
function useRefreshAfterSubmit() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.requests.all }),
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
    ])
}

async function created(promise: Promise<{ data?: RequestDetailsDto }>): Promise<RequestDetailsDto> {
  const { data } = await promise
  if (!data) throw new ApiError({ status: 201, code: UNKNOWN_ERROR_CODE })
  return data
}

export function useSubmitPurchase() {
  const refresh = useRefreshAfterSubmit()
  return useMutation({
    mutationFn: (values: PurchaseValues) =>
      created(
        api.POST('/api/requests/purchase', {
          body: {
            itemName: values.itemName,
            description: values.description,
            justification: values.justification,
            estimatedCost: { amount: values.estimatedCost, currency: 'JOD' },
          },
        }),
      ),
    onSuccess: refresh,
  })
}

export function useSubmitLeave() {
  const refresh = useRefreshAfterSubmit()
  return useMutation({
    mutationFn: (values: LeaveValues) =>
      created(
        api.POST('/api/requests/leave', {
          body: {
            startDate: values.startDate,
            endDate: values.endDate,
            leaveType: values.leaveType,
            // An empty optional reason is "no reason", not an empty string.
            reason: values.reason === '' ? null : values.reason,
          },
        }),
      ),
    onSuccess: refresh,
  })
}

export type RequestListParams = {
  scope: RequestScope
  status: RequestStatus | ''
  type: RequestType | ''
  search: string
  page: number
  pageSize: number
}

/** `GET /api/requests`. Empty filters are left out of the query; the previous page stays visible while the next loads. */
export function useRequests(params: RequestListParams) {
  return useQuery({
    queryKey: queryKeys.requests.list(params),
    queryFn: async () => {
      const { data } = await api.GET('/api/requests', {
        params: {
          query: {
            Scope: params.scope,
            Status: params.status || undefined,
            Type: params.type || undefined,
            Search: params.search || undefined,
            Page: params.page,
            PageSize: params.pageSize,
          },
        },
      })
      if (!data) throw new ApiError({ status: 200, code: UNKNOWN_ERROR_CODE })
      return data
    },
    placeholderData: keepPreviousData,
  })
}
