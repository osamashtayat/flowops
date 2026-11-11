import { Link } from 'react-router'
import { Check, CircleCheck, CircleX, Clock, MessageSquare, Send, SkipForward, type LucideIcon } from 'lucide-react'
import type { ActivityDto, ActivityType } from '@/lib/api/types'
import { activityAction } from '@/lib/format/activity'
import { formatDateTime, formatRelative } from '@/lib/format/date'

const ICONS: Record<ActivityType, { icon: LucideIcon; tone: string }> = {
  Submitted: { icon: Send, tone: 'text-info' },
  TaskAssigned: { icon: Clock, tone: 'text-warning' },
  Approved: { icon: Check, tone: 'text-success' },
  Rejected: { icon: CircleX, tone: 'text-danger' },
  Fulfilled: { icon: CircleCheck, tone: 'text-success' },
  Completed: { icon: CircleCheck, tone: 'text-success' },
  CommentAdded: { icon: MessageSquare, tone: 'text-ink-muted' },
  StepSkipped: { icon: SkipForward, tone: 'text-ink-muted' },
}

/** An activity entry; the dashboard's carry the request they belong to, which then becomes a link. */
export type ActivityTimelineItem = ActivityDto & { requestId?: string; requestNumber?: string }

export interface ActivityTimelineProps {
  items: readonly ActivityTimelineItem[]
  /** Shown when there is nothing yet. */
  emptyText?: string
}

/**
 * Who did what, when (docs/DESIGN_SYSTEM.md §5): oldest first, relative time with the exact time
 * on hover. Comment and decision text is rendered as plain text.
 */
export function ActivityTimeline({ items, emptyText = 'No activity yet.' }: ActivityTimelineProps) {
  if (items.length === 0) return <p className="text-sm text-ink-muted">{emptyText}</p>
  return (
    <ol className="space-y-4">
      {items.map((entry, index) => {
        const { icon: Icon, tone } = ICONS[entry.type]
        return (
          <li key={entry.id} className="relative flex gap-3">
            <div className="flex w-6 shrink-0 flex-col items-center">
              <Icon aria-hidden="true" className={`size-5 ${tone}`} />
              {index < items.length - 1 && <span aria-hidden="true" className="mt-1 w-px flex-1 bg-border" />}
            </div>
            <div className="min-w-0 flex-1 pb-1">
              <p className="text-sm text-ink">
                {entry.requestId && entry.requestNumber && (
                  <>
                    <Link to={`/requests/${entry.requestId}`} className="tabular-nums text-primary underline">
                      {entry.requestNumber}
                    </Link>
                    {' · '}
                  </>
                )}
                {entry.actor && <strong className="font-semibold">{entry.actor.displayName} </strong>}
                {activityAction(entry)}
              </p>
              <time dateTime={entry.occurredAt} title={formatDateTime(entry.occurredAt)} className="text-xs text-ink-muted">
                {formatRelative(entry.occurredAt)}
              </time>
              {entry.body && (
                <blockquote className="mt-2 whitespace-pre-wrap break-words rounded-sm bg-surface-muted p-3 text-sm text-ink">
                  {entry.body}
                </blockquote>
              )}
            </div>
          </li>
        )
      })}
    </ol>
  )
}
