import { Menu } from 'lucide-react'
import { NotificationBell } from '@/features/notifications/components/NotificationBell'
import { UserMenu } from './UserMenu'

export interface TopBarProps {
  /** Opens the navigation drawer (the menu button only shows below 1024 px). */
  onOpenMenu: () => void
}

/** 56 px bar: menu button (mobile), notifications bell, user menu. */
export function TopBar({ onOpenMenu }: TopBarProps) {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between gap-3 border-b border-border bg-canvas px-4 sm:px-8">
      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={onOpenMenu}
          aria-label="Open menu"
          className="inline-flex size-9 items-center justify-center rounded-sm text-ink hover:bg-surface-muted lg:hidden"
        >
          <Menu aria-hidden="true" className="size-5" />
        </button>
        <span className="text-base font-semibold text-ink lg:hidden">FlowOps</span>
      </div>
      <div className="flex items-center gap-1">
        <NotificationBell />
        <UserMenu />
      </div>
    </header>
  )
}
