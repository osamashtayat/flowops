import { Link } from 'react-router'
import {
  CircleCheck,
  CircleX,
  ClipboardList,
  MessageSquare,
  PackageCheck,
  UserCog,
  type LucideIcon,
} from 'lucide-react'
import { cn } from '@/lib/cn'
import type { NotificationDto, NotificationType } from '@/lib/api/types'
import { formatRelative } from '@/lib/format/date'
import { useMarkNotificationRead } from '../api'
import { notificationTarget } from '../targets'

function iconFor(type: NotificationType): { icon: LucideIcon; className: string } {
  switch (type) {
    case 'TaskAssigned':
      return { icon: ClipboardList, className: 'text-primary' }
    case 'StepApproved':
    case 'RequestApproved':
      return { icon: CircleCheck, className: 'text-success' }
    case 'RequestCompleted':
      return { icon: PackageCheck, className: 'text-success' }
    case 'RequestRejected':
      return { icon: CircleX, className: 'text-danger' }
    case 'CommentAdded':
      return { icon: MessageSquare, className: 'text-ink-muted' }
    case 'RoleChangePending':
    case 'RoleChangeApproved':
    case 'RoleChangeRejected':
      return { icon: UserCog, className: 'text-ink-muted' }
    default:
      return type satisfies never
  }
}

export interface NotificationListProps {
  items: readonly NotificationDto[]
  /** Called after an item was opened (the popover closes itself). */
  onOpen?: () => void
  className?: string
}

/**
 * Notifications as a list of links. Unread ones are tinted, carry a dot and are announced as
 * "Unread". Opening one marks it read at once and goes to its screen; `title` and `body` are
 * plain text from the server.
 */
export function NotificationList({ items, onOpen, className }: NotificationListProps) {
  const markRead = useMarkNotificationRead()
  const open = (item: NotificationDto) => {
    if (item.readAt === null) markRead.mutate(item.id)
    onOpen?.()
  }

  return (
    <ul className={cn('divide-y divide-border', className)}>
      {items.map((item) => (
        <li key={item.id}>
          <NotificationRow item={item} onOpen={() => open(item)} />
        </li>
      ))}
    </ul>
  )
}

const rowClass = 'flex w-full items-start gap-3 px-4 py-3 text-left hover:bg-surface-muted focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary'

function NotificationRow({ item, onOpen }: { item: NotificationDto; onOpen: () => void }) {
  const unread = item.readAt === null
  const { icon: Icon, className: iconClass } = iconFor(item.type)
  const target = notificationTarget(item)
  const content = (
    <>
      <Icon aria-hidden="true" className={cn('mt-0.5 size-5 shrink-0', iconClass)} />
      <span className="min-w-0 flex-1">
        {unread && <span className="sr-only">Unread: </span>}
        <span className={cn('block text-base text-ink', unread && 'font-semibold')}>{item.title}</span>
        <span className="block text-sm text-ink-muted">{item.body}</span>
        <span className="mt-1 block text-xs text-ink-muted">{formatRelative(item.createdAt)}</span>
      </span>
      {unread && <span aria-hidden="true" className="mt-2 size-2 shrink-0 rounded-full bg-primary" />}
    </>
  )
  const className = cn(rowClass, unread && 'bg-primary-soft')
  return target ? (
    <Link to={target} onClick={onOpen} className={className}>
      {content}
    </Link>
  ) : (
    <button type="button" onClick={onOpen} className={className}>
      {content}
    </button>
  )
}
