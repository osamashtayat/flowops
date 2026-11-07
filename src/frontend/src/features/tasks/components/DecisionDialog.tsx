import { useState } from 'react'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { useToast } from '@/components/ui/toastContext'
import { ApiError } from '@/lib/api/apiError'
import { toUserMessage } from '@/lib/api/problem'
import type { RequestStage, RequestType, TaskAction } from '@/lib/api/types'
import { useDecideTask } from '../api'
import { decisionPresentation } from '../decisions'

export interface DecisionDialogProps {
  /** The action being decided, or null while the dialog is closed. */
  action: TaskAction | null
  taskId: string
  version: string
  stage: RequestStage
  /** Leave requests get the "visible to everyone" note on the text field. */
  requestType: RequestType
  requestNumber: string
  onClose: () => void
  /** Called after the server accepted the decision. */
  onDecided?: () => void
}

/**
 * Confirm + reason/comment + the decision mutation (docs/UI_SPEC.md §4.5). Reject requires a
 * reason. A 409 means the screen was stale: say so in a toast, close, and let the refetch show the
 * current state. Any other failure stays in the dialog so the typed text is not lost.
 */
export function DecisionDialog({ action, taskId, version, stage, requestType, requestNumber, onClose, onDecided }: DecisionDialogProps) {
  const decide = useDecideTask()
  const toast = useToast()
  const [errorMessage, setErrorMessage] = useState<string | undefined>()
  if (!action) return null
  const text = decisionPresentation(action, stage, requestType)
  const { title, description } = text.describe(requestNumber)

  function close() {
    setErrorMessage(undefined)
    onClose()
  }

  function confirm(reason: string) {
    if (!action) return
    setErrorMessage(undefined)
    decide.mutate(
      { taskId, action, version, text: reason },
      {
        onSuccess: () => {
          toast.success(text.done(requestNumber))
          close()
          onDecided?.()
        },
        onError: (error) => {
          if (error instanceof ApiError && error.status === 409) {
            toast.error(toUserMessage(error))
            close()
          } else {
            setErrorMessage(toUserMessage(error))
          }
        },
      },
    )
  }

  return (
    <ConfirmDialog
      open
      title={title}
      description={description}
      confirmLabel={text.confirmLabel}
      tone={text.tone}
      reason={text.reason}
      loading={decide.isPending}
      error={errorMessage}
      onCancel={close}
      onConfirm={confirm}
    />
  )
}
