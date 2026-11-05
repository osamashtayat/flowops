import { CenteredPage } from '@/components/layout/CenteredPage'
import { ButtonLink } from '@/components/ui/ButtonLink'

/** Catch-all route (docs/UI_SPEC.md §4.9). */
export function NotFoundPage() {
  return (
    <CenteredPage title="Page not found">
      <div className="space-y-4 text-center">
        <p className="text-base text-ink-muted">This page doesn&apos;t exist, or you don&apos;t have access to it.</p>
        <ButtonLink to="/">Go to dashboard</ButtonLink>
      </div>
    </CenteredPage>
  )
}
