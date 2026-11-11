import { Card } from '@/components/ui/Card'
import { StatusBadge } from '@/components/ui/StatusBadge'
import type { WorkTaskDto } from '@/lib/api/types'
import { formatDateTime } from '@/lib/format/date'
import { stepLabel, taskStatusPresentation } from '@/lib/format/status'
import { ROLE_LABELS } from '@/lib/format/labels'

function assigneeText(assignee: WorkTaskDto['assignee']): string {
  if (assignee.user) return assignee.user.displayName
  return assignee.role ? `Any of ${ROLE_LABELS[assignee.role]}` : 'Unassigned'
}

/** The request's tasks in order, as a list of steps: who it waits for, or who decided and what they said. */
export function ApprovalSteps({ tasks }: { tasks: readonly WorkTaskDto[] }) {
  return (
    <Card title="Approval steps">
      {tasks.length === 0 ? (
        <p className="text-sm text-ink-muted">No steps yet.</p>
      ) : (
        <ol className="space-y-4">
          {tasks.map((task) => (
            <li key={task.id} className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2 border-b border-border pb-4 last:border-b-0 last:pb-0">
              <div className="min-w-0">
                <p className="text-base font-medium text-ink">{stepLabel(task.step)}</p>
                <p className="text-sm text-ink-muted">
                  {task.decidedBy && task.decidedAt ? (
                    <>
                      {task.decidedBy.displayName} · <time dateTime={task.decidedAt} className="tabular-nums">{formatDateTime(task.decidedAt)}</time>
                    </>
                  ) : (
                    <>Waiting for {assigneeText(task.assignee)}</>
                  )}
                </p>
                {task.comment && (
                  <blockquote className="mt-2 whitespace-pre-wrap break-words rounded-sm bg-surface-muted p-3 text-sm text-ink">
                    {task.comment}
                  </blockquote>
                )}
              </div>
              <StatusBadge status={taskStatusPresentation(task.outcome)} />
            </li>
          ))}
        </ol>
      )}
    </Card>
  )
}
