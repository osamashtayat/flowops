import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { requireData } from '@/lib/api/apiError'
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
  return requireData(data, 201)
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
            scope: params.scope,
            status: params.status || undefined,
            type: params.type || undefined,
            search: params.search || undefined,
            page: params.page,
            pageSize: params.pageSize,
          },
        },
      })
      return requireData(data)
    },
    placeholderData: keepPreviousData,
  })
}

/** `GET /api/requests/{id}`. A 404 (not visible or not there) is a normal outcome the page shows as "not found". */
export function useRequest(id: string) {
  return useQuery({
    queryKey: queryKeys.requests.detail(id),
    queryFn: async () => {
      const { data } = await api.GET('/api/requests/{id}', { params: { path: { id } } })
      return requireData(data)
    },
  })
}

export function useRequestActivity(id: string) {
  return useQuery({
    queryKey: queryKeys.requests.activity(id),
    queryFn: async () => {
      const { data } = await api.GET('/api/requests/{id}/activity', { params: { path: { id } } })
      return data ?? []
    },
  })
}

export function useAddComment(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: string) => {
      await api.POST('/api/requests/{id}/comments', { params: { path: { id } }, body: { body } })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.requests.activity(id) }),
  })
}
