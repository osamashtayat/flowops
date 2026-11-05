import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ApiError, UNKNOWN_ERROR_CODE } from '@/lib/api/apiError'
import { api } from '@/lib/api/client'
import { queryKeys } from '@/lib/api/queryKeys'
import type { RequestDetailsDto } from '@/lib/api/types'
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
