import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { useToast } from '@/components/ui/toastContext'
import { ApiError } from '@/lib/api/apiError'
import { ADMIN_LIMITS } from '@/lib/api/limits'
import { serverFieldError, toUserMessage } from '@/lib/api/problem'
import { fieldError, useZodForm } from '@/lib/forms/useZodForm'
import { useResetPassword } from '../api'
import { resetPasswordSchema } from '../schemas'

export interface ResetPasswordDialogProps {
  open: boolean
  userId: string
  displayName: string
  onClose: () => void
}

/** Sets a new temporary password; the person must change it at next sign-in and is signed out everywhere. */
export function ResetPasswordDialog({ open, userId, displayName, onClose }: ResetPasswordDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={`Reset password for ${displayName}?`}
      description="They will be signed out everywhere and must choose a new password at next sign-in."
    >
      <ResetPasswordForm userId={userId} displayName={displayName} onClose={onClose} />
    </Dialog>
  )
}

function ResetPasswordForm({ userId, displayName, onClose }: Omit<ResetPasswordDialogProps, 'open'>) {
  const reset = useResetPassword()
  const toast = useToast()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useZodForm(resetPasswordSchema, { defaultValues: { newPassword: '' } })

  function submit(values: { newPassword: string }) {
    reset.mutate(
      { id: userId, newPassword: values.newPassword },
      {
        onSuccess: () => {
          toast.success(`Password reset for ${displayName}.`)
          onClose()
        },
        onError: (error) => {
          const message = serverFieldError(error, 'newPassword')
          if (message) setError('newPassword', { type: 'server', message })
        },
      },
    )
  }

  const bannerError = reset.error && !(reset.error instanceof ApiError && reset.error.fieldErrors.newPassword) ? toUserMessage(reset.error) : null
  return (
    <form noValidate onSubmit={(event) => void handleSubmit(submit)(event)} className="flex flex-col gap-4">
      {bannerError && (
        <p role="alert" className="rounded-sm bg-danger-soft px-3 py-2 text-sm text-danger">
          {bannerError}
        </p>
      )}
      <FormField
        label="New temporary password"
        required
        helperText={`At least ${ADMIN_LIMITS.passwordMin} characters.`}
        error={fieldError(errors, 'newPassword')}
      >
        {(c) => <Input {...c} type="password" autoComplete="new-password" data-autofocus {...register('newPassword')} />}
      </FormField>
      <div className="flex flex-wrap justify-end gap-3">
        <Button variant="secondary" onClick={onClose} disabled={reset.isPending}>
          Cancel
        </Button>
        <Button type="submit" loading={reset.isPending}>
          Reset password
        </Button>
      </div>
    </form>
  )
}
