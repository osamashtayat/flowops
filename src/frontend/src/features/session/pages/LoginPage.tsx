import { Navigate, useSearchParams } from 'react-router'
import { CenteredPage } from '@/components/layout/CenteredPage'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { toUserMessage } from '@/lib/api/problem'
import { fieldError, useZodForm } from '@/lib/forms/useZodForm'
import { useLogin, useSession } from '../api'
import { changePasswordPath, safeReturnTo } from '../returnTo'
import { loginSchema } from '../schemas'
import { SessionLoading } from '../components/SessionGate'

/** docs/UI_SPEC.md §4.1: centred card, generic credential errors, no register link. */
export function LoginPage() {
  const [params] = useSearchParams()
  const returnTo = safeReturnTo(params.get('returnTo'))
  const { status, user } = useSession()
  const login = useLogin()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useZodForm(loginSchema, { defaultValues: { email: '', password: '' } })

  if (status === 'loading') return <SessionLoading />
  // Already signed in (or just signed in): leave the sign-in page.
  if (user) return <Navigate to={user.mustChangePassword ? changePasswordPath(returnTo) : returnTo} replace />

  return (
    <CenteredPage title="Sign in">
      <Card>
        <form noValidate onSubmit={(event) => void handleSubmit((values) => login.mutate(values))(event)} className="grid gap-5">
          {login.error && (
            <p role="alert" className="rounded-sm bg-danger-soft px-3 py-2 text-sm text-danger">
              {toUserMessage(login.error)}
            </p>
          )}
          <FormField label="Email" required error={fieldError(errors, 'email')}>
            {(c) => <Input {...c} type="email" autoComplete="username" {...register('email')} />}
          </FormField>
          <FormField label="Password" required error={fieldError(errors, 'password')}>
            {(c) => <Input {...c} type="password" autoComplete="current-password" {...register('password')} />}
          </FormField>
          <Button type="submit" loading={login.isPending} className="w-full">
            Sign in
          </Button>
        </form>
      </Card>
    </CenteredPage>
  )
}
