import { NavLink } from 'react-router'
import { Plus } from 'lucide-react'
import { useOpenTaskCount } from '@/features/tasks/api'
import { useSession } from '@/features/session/api'
import { useCapability } from '@/features/session/useCapability'
import { cn } from '@/lib/cn'
import { ButtonLink } from '@/components/ui/ButtonLink'
import { navigationFor, type NavItem } from './navigation'

export interface SidebarProps {
  /** Accessible name of the nav landmark ("Main" on desktop, "Menu" in the drawer). */
  label: string
  /** Called after a link is followed, so the mobile drawer can close. */
  onNavigate?: () => void
}

/** Navigation content: "New request", the main items, and Administration — by capability. */
export function Sidebar({ label, onNavigate }: SidebarProps) {
  const { user } = useSession()
  const navigation = navigationFor(user?.capabilities ?? [])
  const canSeeInbox = useCapability('tasks.view')
  const openTasks = useOpenTaskCount(canSeeInbox)
  const countFor = (item: NavItem) => (item.showsOpenTaskCount ? openTasks.data : undefined)

  return (
    <nav aria-label={label} className="flex flex-col gap-6 px-3 pb-6">
      {navigation.canSubmitRequests && (
        <ButtonLink to="/requests/new" onClick={onNavigate} className="w-full">
          <Plus aria-hidden="true" className="size-4" />
          New request
        </ButtonLink>
      )}
      <ul className="flex flex-col gap-1">
        {navigation.primary.map((item) => (
          <NavItemLink key={item.to} item={item} count={countFor(item)} onNavigate={onNavigate} />
        ))}
      </ul>
      {navigation.admin.length > 0 && (
        <div>
          <h2 className="mb-1 px-3 text-xs font-medium uppercase tracking-wide text-ink-muted">Administration</h2>
          <ul className="flex flex-col gap-1">
            {navigation.admin.map((item) => (
              <NavItemLink key={item.to} item={item} onNavigate={onNavigate} />
            ))}
          </ul>
        </div>
      )}
    </nav>
  )
}

function NavItemLink({ item, count, onNavigate }: { item: NavItem; count?: number; onNavigate?: () => void }) {
  const Icon = item.icon
  const hasCount = count !== undefined && count > 0
  return (
    <li>
      <NavLink
        to={item.to}
        end={item.end}
        onClick={onNavigate}
        // The visible badge is a bare number; the link's name says what it counts.
        aria-label={hasCount ? `${item.label}, ${count} open` : undefined}
        // NavLink sets aria-current="page" on the active item.
        className={({ isActive }) =>
          cn(
            'flex h-10 items-center gap-3 rounded-sm px-3 text-sm font-medium text-ink hover:bg-sidebar-active',
            isActive && 'bg-sidebar-active',
          )
        }
      >
        <Icon aria-hidden="true" className="size-4 shrink-0 text-ink-muted" />
        <span className="flex-1">{item.label}</span>
        {hasCount && (
          <span aria-hidden="true" className="rounded-full bg-primary px-2 text-xs font-medium tabular-nums text-on-primary">
            {count}
          </span>
        )}
      </NavLink>
    </li>
  )
}
