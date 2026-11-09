import { useNavigate } from 'react-router'
import { useToast } from '@/components/ui/toastContext'
import type { RequestDetailsDto } from '@/lib/api/types'
import { setServerFieldErrors } from '@/lib/forms/useZodForm'
import { hasServerFieldErrors } from '@/lib/api/problem'

type SetServerError<TField extends string> = (field: TField, error: { type: 'server'; message: string }) => void

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
      setServerFieldErrors(error, fields, setError)
    },
  }
}

/** The banner text for a failed submit, or null when the server already answered per field. */
export function submitBannerMessage(error: unknown, toMessage: (error: unknown) => string): string | null {
  if (error === null) return null
  if (hasServerFieldErrors(error)) return null
  return toMessage(error)
}
