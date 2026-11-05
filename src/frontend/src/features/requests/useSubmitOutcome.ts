import { useNavigate } from 'react-router'
import { useToast } from '@/components/ui/toastContext'
import { ApiError } from '@/lib/api/apiError'
import type { RequestDetailsDto } from '@/lib/api/types'

type SetServerError<TField extends string> = (field: TField, error: { type: 'server'; message: string }) => void

/** The first server message for a form field; `estimatedCost.amount` counts for `estimatedCost`. */
function messageFor(error: ApiError, field: string): string | undefined {
  const key = Object.keys(error.fieldErrors).find((name) => name === field || name.startsWith(`${field}.`))
  return key ? error.fieldErrors[key]?.[0] : undefined
}

/**
 * What both submit forms do with the result: put a server 400 on the matching fields (RHF then
 * focuses the first one), and on success announce it and open the new request.
 * Anything without a field (422, 5xx, network) is left to the form's error banner.
 */
export function useSubmitOutcome<TField extends string>(setError: SetServerError<TField>, fields: readonly TField[]) {
  const navigate = useNavigate()
  const toast = useToast()
  return {
    onSuccess(request: RequestDetailsDto) {
      toast.success(`Request ${request.number} submitted. Your manager has been notified.`)
      void navigate(`/requests/${request.id}`)
    },
    onError(error: unknown) {
      if (!(error instanceof ApiError)) return
      for (const field of fields) {
        const message = messageFor(error, field)
        if (message) setError(field, { type: 'server', message })
      }
    },
  }
}

/** The banner text for a failed submit, or null when the server already answered per field. */
export function submitBannerMessage(error: unknown, toMessage: (error: unknown) => string): string | null {
  if (error === null) return null
  if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) return null
  return toMessage(error)
}
