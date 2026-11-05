import { useState, type FormEvent, type ReactNode } from 'react'
import { Button } from './Button'
import { Dialog } from './Dialog'
import { FormField } from './FormField'
import { Textarea } from './Textarea'

export interface ConfirmReason {
  label: string
  required?: boolean
  helperText?: string
  maxLength?: number
}

export interface ConfirmDialogProps {
  open: boolean
  onCancel: () => void
  /** Receives the trimmed reason ("" when there is no reason field or it was left empty). */
  onConfirm: (reason: string) => void
  /** States the action and object: "Reject FO-2026-000123?" */
  title: string
  /** The consequence, in a sentence. */
  description: ReactNode
  /** Names the action ("Reject request"), never "OK". */
  confirmLabel: string
  cancelLabel?: string
  tone?: 'primary' | 'danger'
  /** Adds a reason textarea; `required` blocks confirming while it is empty. */
  reason?: ConfirmReason
  /** The action is running: buttons disable, the dialog cannot be dismissed. */
  loading?: boolean
  /** Failure message from the last attempt, e.g. the mapped 409 text. */
  error?: string
}

/** Decision/confirmation dialog (DESIGN_SYSTEM §5). `DecisionDialog` builds the mutation on top of it. */
export function ConfirmDialog({ open, onCancel, loading = false, ...rest }: ConfirmDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={onCancel}
      title={rest.title}
      description={rest.description}
      size="confirm"
      dismissible={!loading}
    >
      <ConfirmBody onCancel={onCancel} loading={loading} {...rest} />
    </Dialog>
  )
}

function ConfirmBody({
  onCancel,
  onConfirm,
  confirmLabel,
  cancelLabel = 'Cancel',
  tone = 'primary',
  reason,
  loading,
  error,
}: Omit<ConfirmDialogProps, 'open' | 'title' | 'description'>) {
  const [text, setText] = useState('')
  const [showReasonError, setShowReasonError] = useState(false)
  const reasonMissing = Boolean(reason?.required) && text.trim() === ''

  function submit(event: FormEvent) {
    event.preventDefault()
    if (loading) return
    if (reasonMissing) {
      setShowReasonError(true)
      return
    }
    onConfirm(text.trim())
  }

  return (
    <form noValidate onSubmit={submit} className="flex flex-col gap-4">
      {error && (
        <p role="alert" className="rounded-sm bg-danger-soft px-3 py-2 text-sm text-danger">
          {error}
        </p>
      )}
      {reason && (
        <FormField
          label={reason.label}
          required={reason.required}
          helperText={reason.helperText}
          error={showReasonError && reasonMissing ? `Enter a ${reason.label.toLowerCase()}.` : undefined}
        >
          {(control) => (
            <Textarea
              {...control}
              data-autofocus
              value={text}
              maxLength={reason.maxLength}
              onChange={(event) => setText(event.target.value)}
            />
          )}
        </FormField>
      )}
      <div className="flex flex-wrap justify-end gap-3">
        <Button variant="secondary" onClick={onCancel} disabled={loading} data-autofocus={reason ? undefined : true}>
          {cancelLabel}
        </Button>
        <Button type="submit" variant={tone} loading={loading}>
          {confirmLabel}
        </Button>
      </div>
    </form>
  )
}
