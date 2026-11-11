import { Check, Circle, CircleX, Clock, SkipForward, type LucideIcon } from 'lucide-react'
import { Card } from '@/components/ui/Card'
import { StatusBadge } from '@/components/ui/StatusBadge'
import type { RequestDetailsDto, RouteStepState, WorkTaskDto } from '@/lib/api/types'
import { formatDateTime } from '@/lib/format/date'
import { ROLE_LABELS } from '@/lib/format/labels'
import { taskStatusPresentation } from '@/lib/format/status'
import { useRequestActivity } from '../api'

type RouteEntry = RequestDetailsDto['route'][number]

const STATE_WORD: Record<RouteStepState, string> = { Done: 'done', Skipped: 'skipped', Current: 'current step', Planned: 'planned' }

const ICONS: Record<RouteStepState, { icon: LucideIcon; tone: string }> = {
  Done: { icon: Check, tone: 'text-success' },
  Current: { icon: Clock, tone: 'text-warning' },
  Planned: { icon: Circle, tone: 'text-ink-muted' },
  Skipped: { icon: SkipForward, tone: 'text-ink-muted' },
}

function assigneeText(assignee: WorkTaskDto['assignee']): string {
  if (assignee.user) return assignee.user.displayName
  return assignee.role ? `Any of ${ROLE_LABELS[assignee.role]}` : 'Unassigned'
}

function TaskDetail({ task }: { task: WorkTaskDto }) {
  return (
    <>
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
        <blockquote className="mt-2 whitespace-pre-wrap break-words rounded-sm bg-surface-muted p-3 text-sm text-ink">{task.comment}</blockquote>
      )}
    </>
  )
}

interface RouteRowProps {
  entry: RouteEntry
  task: WorkTaskDto | undefined
  /** The condition text from the matching `StepSkipped` activity, when it has loaded. */
  skipReason: string | null
  isNext: boolean
}

function RouteRow({ entry, task, skipReason, isNext }: RouteRowProps) {
  const { icon: Icon, tone } = ICONS[entry.state]
  const isMuted = entry.state === 'Planned' || entry.state === 'Skipped'
  const rejected = task?.outcome === 'Rejected'
  return (
    <li
      aria-current={entry.state === 'Current' ? 'step' : undefined}
      className={`flex flex-wrap items-start justify-between gap-x-4 gap-y-2 rounded-sm p-3 ${entry.state === 'Current' ? 'bg-primary-soft' : ''}`}
    >
      <div className="flex min-w-0 gap-3">
        {rejected ? <CircleX aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-danger" /> : <Icon aria-hidden="true" className={`mt-0.5 size-5 shrink-0 ${tone}`} />}
        <div className="min-w-0">
          <p className={`text-base font-medium ${isMuted ? 'text-ink-muted' : 'text-ink'}`}>
            {entry.step.name}
            {/* The state in words, so it never rests on colour or icon alone. */}
            <span className="sr-only">, {STATE_WORD[entry.state]}</span>
          </p>
          {entry.state === 'Skipped' && <p className="text-sm text-ink-muted">Skipped{skipReason ? `. ${skipReason}` : ''}</p>}
          {entry.state === 'Planned' && isNext && <p className="text-sm text-ink-muted">Next</p>}
          {task && (entry.state === 'Done' || entry.state === 'Current') && <TaskDetail task={task} />}
        </div>
      </div>
      {task && entry.state !== 'Skipped' && <StatusBadge status={taskStatusPresentation(task.outcome)} />}
    </li>
  )
}

/**
 * The request's route as the server planned it (`route`, docs/UI_SPEC.md §7.2): steps already decided or
 * skipped, the current one highlighted, and the ones still to come. Per-task detail comes from `tasks`.
 */
export function RouteCard({ request }: { request: RequestDetailsDto }) {
  const activity = useRequestActivity(request.id)
  const skipReasons = new Map(
    (activity.data ?? []).filter((entry) => entry.type === 'StepSkipped' && entry.step).map((entry) => [entry.step?.key, entry.body] as const),
  )
  const firstPlanned = request.route.findIndex((entry) => entry.state === 'Planned')
  return (
    <Card title="Route">
      {request.route.length === 0 ? (
        <p className="text-sm text-ink-muted">No steps yet.</p>
      ) : (
        <ol className="space-y-1">
          {request.route.map((entry, index) => (
            <RouteRow
              key={entry.step.key}
              entry={entry}
              task={request.tasks.findLast((task) => task.step.key === entry.step.key)}
              skipReason={skipReasons.get(entry.step.key) ?? null}
              isNext={index === firstPlanned}
            />
          ))}
        </ol>
      )}
    </Card>
  )
}
