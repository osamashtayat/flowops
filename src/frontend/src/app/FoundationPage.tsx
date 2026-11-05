import { Button } from '@/components/ui/Button'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { requestStatusPresentation, type RequestStatus } from '@/lib/format/status'

const statuses: RequestStatus[] = ['InReview', 'Approved', 'Completed', 'Rejected']

/**
 * Temporary landing page showing the design foundation. Replaced by the dashboard in FE-12;
 * kept until then so reviewers can check tokens and primitives in a real browser.
 */
export function FoundationPage() {
  return (
    <section className="space-y-6">
      <header>
        <h1 className="text-xl font-semibold">FlowOps foundation</h1>
        <p className="text-ink-muted">Design tokens and first shared components. Features arrive in FE-01 onwards.</p>
      </header>

      <div className="rounded-md border border-border bg-surface p-5 shadow-card">
        <h2 className="mb-4 text-lg font-semibold">Buttons</h2>
        <div className="flex flex-wrap gap-3">
          <Button>Submit request</Button>
          <Button variant="secondary">Cancel</Button>
          <Button variant="ghost">View details</Button>
          <Button variant="danger">Reject request</Button>
          <Button loading>Saving</Button>
        </div>
      </div>

      <div className="rounded-md border border-border bg-surface p-5 shadow-card">
        <h2 className="mb-4 text-lg font-semibold">Request statuses</h2>
        <ul className="flex flex-wrap gap-3">
          {statuses.map((s) => (
            <li key={s}>
              <StatusBadge status={requestStatusPresentation(s)} />
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}
