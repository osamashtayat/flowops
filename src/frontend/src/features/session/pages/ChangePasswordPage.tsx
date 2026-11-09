import { useNavigate, useSearchParams } from 'react-router'
import { CenteredPage } from '@/components/layout/CenteredPage'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { useToast } from '@/components/ui/toastContext'
import { hasServerFieldErrors, toUserMessage } from '@/lib/api/problem'
import { fieldError, setServerFieldErrors, useZodForm } from '@/lib/forms/useZodForm'
import { useChangePassword, useLogout, useSession } from '../api'
import { safeReturnTo } from '../returnTo'
import { SESSION_LIMITS } from '../limits'
import { changePasswordSchema } from '../schemas'

/** docs/UI_SPEC.md §1: reachable any time; the only route allowed while `mustChangePassword`. */
export function ChangePasswordPage() {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const returnTo = safeReturnTo(params.get('returnTo'))
  const toast = useToast()
  const { user } = useSession()
  const forced = user?.mustChangePassword ?? false
  const changePassword = useChangePassword()
  const logout = useLogout()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useZodForm(changePasswordSchema, { defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' } })

  function submit(values: { currentPassword: string; newPassword: string }) {
    changePassword.mutate(
      { currentPassword: values.currentPassword, newPassword: values.newPassword },
      {
        onSuccess: () => {
          toast.success('Password changed.')
          void navigate(returnTo, { replace: true })
        },
        // Server rules (e.g. Identity password policy) land on the field they are about.
        onError: (error) => setServerFieldErrors(error, ['currentPassword', 'newPassword'], setError),
      },
    )
  }

  const serverMessage = changePassword.error && !hasServerFieldErrors(changePassword.error)
    ? toUserMessage(changePassword.error)
    : null

  return (
    <CenteredPage title="Change password">
      <Card>
        <form noValidate onSubmit={(event) => void handleSubmit(submit)(event)} className="grid gap-5">
          {forced && (
            <p className="rounded-sm bg-info-soft px-3 py-2 text-sm text-info">
              You must choose a new password before you can continue.
            </p>
          )}
          {serverMessage && (
            <p role="alert" className="rounded-sm bg-danger-soft px-3 py-2 text-sm text-danger">
              {serverMessage}
            </p>
          )}
          <FormField label="Current password" required error={fieldError(errors, 'currentPassword')}>
            {(c) => <Input {...c} type="password" autoComplete="current-password" {...register('currentPassword')} />}
          </FormField>
          <FormField
            label="New password"
            required
            helperText={`At least ${SESSION_LIMITS.newPasswordMin} characters.`}
            error={fieldError(errors, 'newPassword')}
          >
            {(c) => <Input {...c} type="password" autoComplete="new-password" {...register('newPassword')} />}
          </FormField>
          <FormField label="Confirm new password" required error={fieldError(errors, 'confirmPassword')}>
            {(c) => <Input {...c} type="password" autoComplete="new-password" {...register('confirmPassword')} />}
          </FormField>
          <div className="flex flex-wrap justify-end gap-3">
            {forced ? (
              <Button variant="ghost" onClick={() => logout.mutate()} loading={logout.isPending}>
                Sign out
              </Button>
            ) : (
              <Button variant="ghost" onClick={() => void navigate(-1)}>
                Cancel
              </Button>
            )}
            <Button type="submit" loading={changePassword.isPending}>
              Change password
            </Button>
          </div>
        </form>
      </Card>
    </CenteredPage>
  )
}
