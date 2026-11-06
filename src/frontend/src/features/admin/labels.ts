import type { Role, StalledTaskReason } from '@/lib/api/types'

/** One person's role as a chip or checkbox label. (`ROLE_LABELS` in requests/labels is the plural group name.) */
export const ROLE_NAMES: Record<Role, string> = {
  Administrator: 'Administrator',
  Employee: 'Employee',
  FinanceOfficer: 'Finance officer',
  HrOfficer: 'HR officer',
  ProcurementOfficer: 'Procurement officer',
}

export const ROLES = Object.keys(ROLE_NAMES) as Role[]

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
