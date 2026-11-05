import { ButtonLink } from '@/components/ui/ButtonLink'
import { Card } from '@/components/ui/Card'
import type { DashboardDto } from '@/lib/api/types'
import { formatRelative } from '@/lib/format/date'

type MyTasks = NonNullable<DashboardDto['myTasks']>

/** What waits for the viewer. Shown only when the server sent `myTasks` (people who work queues). */
export function TasksCard({ tasks }: { tasks: MyTasks }) {
  const waiting = tasks.open > 0
  return (
    <Card title="Your tasks" actions={<ButtonLink to="/inbox" variant={waiting ? 'primary' : 'secondary'}>Open inbox</ButtonLink>}>
      {waiting ? (
        <p className="text-base text-ink">
          <strong className="font-semibold tabular-nums">{tasks.open}</strong> {tasks.open === 1 ? 'task is' : 'tasks are'} waiting for you
          {tasks.oldestOpenCreatedAt && (
            <span className="text-ink-muted"> · oldest assigned {formatRelative(tasks.oldestOpenCreatedAt)}</span>
          )}
        </p>
      ) : (
        <p className="text-base text-ink-muted">Nothing is waiting for you.</p>
      )}
      <p className="mt-1 text-sm text-ink-muted">
        You decided <span className="tabular-nums">{tasks.decidedLast7Days}</span> in the last 7 days.
      </p>
    </Card>
  )
}
