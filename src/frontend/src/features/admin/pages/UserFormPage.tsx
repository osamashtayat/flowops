import { useState } from 'react'
import { useParams } from 'react-router'
import { PageHeader } from '@/components/layout/PageHeader'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ErrorState } from '@/components/ui/ErrorState'
import { Skeleton } from '@/components/ui/Skeleton'
import { ApiError } from '@/lib/api/apiError'
import { toUserMessage } from '@/lib/api/problem'
import { useUser } from '../api'
import { ResetPasswordDialog } from '../components/ResetPasswordDialog'
import { UserForm } from '../components/UserForm'

export function NewUserPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="New user" subtitle="They sign in with the email and initial password and must change it first." />
      <Card>
        <UserForm />
      </Card>
    </div>
  )
}

export function EditUserPage() {
  const { id } = useParams()
  const query = useUser(id)
  const [resetting, setResetting] = useState(false)

  if (query.isPending) {
    return (
      <div className="space-y-6">
        <PageHeader title="User" />
        <output className="sr-only">Loading…</output>
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }
  if (query.isError) {
    // Retrying a 404 cannot help; offer it only for failures that may pass.
    const retry = query.error instanceof ApiError && query.error.status === 404 ? undefined : () => void query.refetch()
    return <ErrorState message={toUserMessage(query.error)} onRetry={retry} />
  }
  const user = query.data

  return (
    <div className="space-y-6">
      <PageHeader title={user.displayName} subtitle={user.email} />
      <Card>
        {/* No `key` on the user's fields: a refetch after saving must not remount the form, or the
            mutation's success handler (navigate + toast) and a failed second step's error vanish with it. */}
        <UserForm user={user} />
      </Card>
      <Card title="Reset password" headingLevel={2}>
        <p className="mb-4 text-base text-ink-muted">Set a temporary password if they are locked out or forgot it. They must change it at next sign-in.</p>
        <Button variant="secondary" onClick={() => setResetting(true)}>
          Reset password…
        </Button>
        <ResetPasswordDialog open={resetting} userId={user.id} displayName={user.displayName} onClose={() => setResetting(false)} />
      </Card>
    </div>
  )
}
