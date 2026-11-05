import type { FormEventHandler, ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { Alert } from '@/components/ui/Alert'
import { Button } from '@/components/ui/Button'
import { useSession } from '@/features/session/api'
import { toUserMessage } from '@/lib/api/problem'
import { submitBannerMessage } from '../useSubmitOutcome'

export interface RequestFormFrameProps {
  onSubmit: FormEventHandler<HTMLFormElement>
  isSubmitting: boolean
  /** The failed mutation's error, if any. */
  submitError: unknown
  children: ReactNode
}

/**
 * What both request forms share (docs/UI_SPEC.md §4.4): who is asking, the "no manager or
 * department" block, the error banner and the Cancel / Submit buttons. The caller supplies the fields.
 */
export function RequestFormFrame({ onSubmit, isSubmitting, submitError, children }: RequestFormFrameProps) {
  const navigate = useNavigate()
  const { user } = useSession()
  const blocked = !user?.department || !user.manager
  const banner = submitBannerMessage(submitError ?? null, toUserMessage)

  return (
    <form noValidate onSubmit={onSubmit} className="grid gap-5">
      <dl className="grid gap-3 sm:grid-cols-2">
        <div>
          <dt className="text-sm font-medium text-ink-muted">Requested by</dt>
          <dd className="text-base text-ink">{user?.displayName}</dd>
        </div>
        <div>
          <dt className="text-sm font-medium text-ink-muted">Department</dt>
          <dd className="text-base text-ink">{user?.department?.name ?? 'Not assigned'}</dd>
        </div>
      </dl>
      {blocked && (
        <Alert tone="warning">
          You can't submit requests until an administrator assigns you to a department and a manager. Contact your administrator.
        </Alert>
      )}
      {banner && <Alert tone="danger">{banner}</Alert>}
      <fieldset disabled={blocked} className="m-0 grid min-w-0 gap-5 border-0 p-0">
        {children}
        <div className="flex flex-wrap justify-end gap-3">
          <Button variant="ghost" onClick={() => void navigate('/requests/new')}>
            Cancel
          </Button>
          <Button type="submit" loading={isSubmitting}>
            Submit request
          </Button>
        </div>
      </fieldset>
    </form>
  )
}
