import { useEffect, useId, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router'
import { Bell } from 'lucide-react'
import { useNotifications, useUnreadCount } from '../api'
import { NotificationFeed } from './NotificationFeed'

const POPOVER_SIZE = 8
const POPOVER_PARAMS = { unreadOnly: false, page: 1, pageSize: POPOVER_SIZE } as const
const MAX_SHOWN = 99

/** Top-bar bell: unread count badge and a popover with the latest notifications (docs/UI_SPEC.md §4.7). */
export function NotificationBell() {
  const { pathname } = useLocation()
  // Like the mobile drawer: the popover is open only on the page where it was opened.
  const [openedAt, setOpenedAt] = useState<string | null>(null)
  const open = openedAt === pathname
  const close = () => setOpenedAt(null)
  const popoverId = useId()
  const root = useRef<HTMLDivElement>(null)
  const unread = useUnreadCount()
  const latest = useNotifications(POPOVER_PARAMS, open)
  const count = unread.data ?? 0

  useEffect(() => {
    if (!open) return
    const onPointerDown = (event: PointerEvent) => {
      if (event.target instanceof Node && !root.current?.contains(event.target)) setOpenedAt(null)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      setOpenedAt(null)
      root.current?.querySelector('button')?.focus()
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  return (
    <div ref={root} className="relative">
      <button
        type="button"
        onClick={() => setOpenedAt(open ? null : pathname)}
        aria-label={count > 0 ? `Notifications, ${count} unread` : 'Notifications'}
        aria-expanded={open}
        aria-controls={popoverId}
        className="relative inline-flex size-9 items-center justify-center rounded-sm text-ink hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
      >
        <Bell aria-hidden="true" className="size-5" />
        {count > 0 && (
          <span
            aria-hidden="true"
            className="absolute right-0 top-0 inline-flex min-w-4 items-center justify-center rounded-full bg-primary px-1 text-xs font-semibold leading-4 text-on-primary tabular-nums"
          >
            {count > MAX_SHOWN ? `${MAX_SHOWN}+` : count}
          </span>
        )}
      </button>
      {open && (
        <section
          id={popoverId}
          aria-label="Latest notifications"
          className="absolute right-0 top-full z-40 mt-2 w-96 max-w-[calc(100vw-2rem)] overflow-hidden rounded-md border border-border bg-surface shadow-card"
        >
          <div className="max-h-[70dvh] overflow-y-auto">
            <NotificationFeed query={latest} emptyMessage="You have no notifications yet." onOpen={close} />
          </div>
          <div className="border-t border-border px-4 py-3 text-center">
            <Link to="/notifications" onClick={close} className="text-sm font-medium text-primary underline">
              View all
            </Link>
          </div>
        </section>
      )}
    </div>
  )
}
