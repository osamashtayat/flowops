import type { RequestType, TaskAction } from '@/lib/api/types'
import { withLeaveVisibilityWarning } from '@/lib/format/visibility'
import { REQUEST_LIMITS } from '@/lib/api/limits'

/** What a decision button, its dialog and its text field look like. The one home for these words. */
export interface DecisionPresentation {
  /** The button that opens the dialog. */
  buttonLabel: string
  /** The dialog's confirm button: names the action, never "OK". */
  confirmLabel: string
  tone: 'primary' | 'danger'
  describe: (requestNumber: string) => { title: string; description: string }
  /** The toast after the server accepted it. */
  done: (requestNumber: string) => string
  reason: { label: string; required: boolean; helperText?: string; maxLength: number }
}

export function decisionPresentation(action: TaskAction, requestType: RequestType): DecisionPresentation {
  const presentation = basePresentation(action)
  const helperText = withLeaveVisibilityWarning(presentation.reason.helperText, requestType)
  return { ...presentation, reason: { ...presentation.reason, helperText } }
}

function basePresentation(action: TaskAction): DecisionPresentation {
  switch (action) {
    case 'approve': {
      // One verb for every approval step: step names are admin-defined (ADR-0014), so the UI has no per-step wording.
      return {
        buttonLabel: 'Approve',
        confirmLabel: 'Approve request',
        tone: 'primary',
        describe: (number) => ({
          title: `Approve ${number}?`,
          // Says nothing about what comes next: the step order belongs to the server.
          description: 'Your decision is recorded in the request history. This cannot be undone.',
        }),
        done: (number) => `${number} approved.`,
        reason: { label: 'Comment', required: false, helperText: 'Optional. Visible in the request history.', maxLength: REQUEST_LIMITS.approveCommentMax },
      }
    }
    case 'reject':
      return {
        buttonLabel: 'Reject',
        confirmLabel: 'Reject request',
        tone: 'danger',
        describe: (number) => ({
          title: `Reject ${number}?`,
          description: 'The request is closed as rejected and the requester is told why. This cannot be undone.',
        }),
        done: (number) => `${number} rejected.`,
        reason: { label: 'Reason', required: true, helperText: 'Shown to the requester.', maxLength: REQUEST_LIMITS.rejectReasonMax },
      }
    case 'fulfill':
      return {
        buttonLabel: 'Fulfill',
        confirmLabel: 'Mark as fulfilled',
        tone: 'primary',
        describe: (number) => ({
          title: `Fulfill ${number}?`,
          description: 'The request is completed and the requester is notified. This cannot be undone.',
        }),
        done: (number) => `${number} marked as fulfilled.`,
        reason: { label: 'Note', required: false, helperText: 'Optional. Visible in the request history.', maxLength: REQUEST_LIMITS.fulfillNoteMax },
      }
    default:
      return action satisfies never
  }
}
