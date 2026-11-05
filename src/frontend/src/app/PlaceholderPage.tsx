import { PageHeader } from '@/components/layout/PageHeader'
import { Card } from '@/components/ui/Card'

export interface PlaceholderPageProps {
  title: string
  /** The backlog task that builds this screen. */
  task: string
}

/**
 * Stand-in for a screen that a later task builds, so navigation and capability guards can be
 * checked end to end now. Each route using it is replaced by the real page (docs/PHASE1_TASKS.md).
 */
export function PlaceholderPage({ title, task }: PlaceholderPageProps) {
  return (
    <div className="space-y-6">
      <PageHeader title={title} />
      <Card>
        <p className="text-base text-ink-muted">This screen is built in {task}.</p>
      </Card>
    </div>
  )
}
