import { CenteredPage } from '@/components/layout/CenteredPage'
import { ButtonLink } from '@/components/ui/ButtonLink'

/** `/forbidden` (docs/UI_SPEC.md §4.9): the user is signed in but not allowed here. */
export function ForbiddenPage() {
  return (
    <CenteredPage title="You don't have access">
      <div className="space-y-4 text-center">
        <p className="text-base text-ink-muted">Your account doesn&apos;t have permission to open this page.</p>
        <ButtonLink to="/">Go to dashboard</ButtonLink>
      </div>
    </CenteredPage>
  )
}
