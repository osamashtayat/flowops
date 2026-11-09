import type { Role, StalledTaskReason } from '@/lib/api/types'
import { ROLE_NAMES } from '@/lib/format/labels'

/** Why a task is stalled and what an administrator can do about it (docs/UI_SPEC.md §4.8). */
export const STALLED_REASONS: Record<StalledTaskReason, { label: string; guidance: (role: Role | null) => string }> = {
  NoEligibleRoleMember: {
    label: 'No eligible person',
    guidance: (role) => `Add an active ${role ? ROLE_NAMES[role] : 'officer'}.`,
  },
  AssigneeInactive: {
    label: 'Assignee inactive',
    guidance: () => 'Reactivate the assignee, or give the requester a new manager.',
  },
}

/** "Employee, Finance officer" for a role set. */
export const rolesText = (roles: readonly Role[]) => (roles.length ? roles.map((r) => ROLE_NAMES[r]).join(', ') : 'No roles')
