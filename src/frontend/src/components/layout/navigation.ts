import { Bell, Building2, FileText, Hourglass, Inbox, ShieldCheck, LayoutDashboard, Users, UsersRound, type LucideIcon } from 'lucide-react'
import type { Capability } from '@/lib/api/types'

export interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  /** Match this exact path only (so "My requests" is not active on /requests/team). */
  end?: boolean
  /** Shown only when the server granted this capability. No capability = everyone signed in. */
  capability?: Capability
  /** Shows the open-task count next to the label. */
  showsOpenTaskCount?: boolean
}

/** docs/UI_SPEC.md §3, in display order. */
const PRIMARY: readonly NavItem[] = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/requests', label: 'My requests', icon: FileText, end: true },
  { to: '/requests/team', label: 'Team requests', icon: UsersRound, capability: 'tasks.view' },
  { to: '/inbox', label: 'Inbox', icon: Inbox, capability: 'tasks.view', showsOpenTaskCount: true },
  { to: '/notifications', label: 'Notifications', icon: Bell },
]

const ADMIN: readonly NavItem[] = [
  { to: '/admin/users', label: 'Users', icon: Users, capability: 'admin.manage' },
  { to: '/admin/departments', label: 'Departments', icon: Building2, capability: 'admin.manage' },
  { to: '/admin/role-changes', label: 'Role changes', icon: ShieldCheck, capability: 'admin.manage' },
  { to: '/admin/stalled-tasks', label: 'Stalled tasks', icon: Hourglass, capability: 'admin.manage' },
]

export interface Navigation {
  /** The prominent "New request" button (`requests.submit`). */
  canSubmitRequests: boolean
  primary: readonly NavItem[]
  /** Items under "Administration"; empty when the user is not an administrator. */
  admin: readonly NavItem[]
}

/** What the sidebar shows for a capability list from the server — never derived from role names. */
export function navigationFor(capabilities: readonly Capability[]): Navigation {
  const allowed = (item: NavItem) => !item.capability || capabilities.includes(item.capability)
  return {
    canSubmitRequests: capabilities.includes('requests.submit'),
    primary: PRIMARY.filter(allowed),
    admin: ADMIN.filter(allowed),
  }
}
