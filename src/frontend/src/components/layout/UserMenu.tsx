import { useEffect, useId, useRef, useState } from 'react'
import { Link } from 'react-router'
import { ChevronDown, KeyRound, LogOut } from 'lucide-react'
import { useLogout, useSession } from '@/features/session/api'
import { CHANGE_PASSWORD_PATH } from '@/features/session/returnTo'

/**
 * The signed-in user's name with a small panel: who they are, change password, sign out.
 * A disclosure (button + `aria-expanded`), not an ARIA menu, so Tab/Shift+Tab work as usual;
 * Esc or a click outside closes it and focus returns to the button.
 */
export function UserMenu() {
  const { user } = useSession()
  const logout = useLogout()
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const panelId = useId()

  useEffect(() => {
    if (!open) return
    function onPointerDown(event: MouseEvent) {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) setOpen(false)
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setOpen(false)
        buttonRef.current?.focus()
      }
    }
    document.addEventListener('mousedown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('mousedown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  if (!user) return null

  return (
    <div ref={rootRef} className="relative">
      <button
        ref={buttonRef}
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((value) => !value)}
        className="flex h-9 items-center gap-2 rounded-sm px-2 text-sm font-medium text-ink hover:bg-surface-muted"
      >
        <span className="max-w-40 truncate">{user.displayName}</span>
        <ChevronDown aria-hidden="true" className="size-4 text-ink-muted" />
      </button>
      {open && (
        <div
          id={panelId}
          className="absolute right-0 top-full z-30 mt-1 w-64 rounded-md border border-border bg-surface p-2 shadow-overlay"
        >
          <div className="px-3 py-2">
            <p className="truncate text-sm font-medium text-ink">{user.displayName}</p>
            <p className="truncate text-xs text-ink-muted">{user.email}</p>
            {user.department && <p className="truncate text-xs text-ink-muted">{user.department.name}</p>}
          </div>
          <hr className="my-1 border-border" />
          <Link
            to={CHANGE_PASSWORD_PATH}
            onClick={() => setOpen(false)}
            className="flex h-9 items-center gap-2 rounded-sm px-3 text-sm text-ink hover:bg-surface-muted"
          >
            <KeyRound aria-hidden="true" className="size-4 text-ink-muted" />
            Change password
          </Link>
          <button
            type="button"
            onClick={() => logout.mutate()}
            disabled={logout.isPending}
            className="flex h-9 w-full items-center gap-2 rounded-sm px-3 text-left text-sm text-ink hover:bg-surface-muted disabled:cursor-not-allowed disabled:opacity-50"
          >
            <LogOut aria-hidden="true" className="size-4 text-ink-muted" />
            Sign out
          </button>
        </div>
      )}
    </div>
  )
}
