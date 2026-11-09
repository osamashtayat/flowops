import type { LeaveType, RequestStatus, RequestType, Role } from '@/lib/api/types'

/**
 * People-facing names for the contract's enums, shared by every feature (CODE_STANDARDS §2.3).
 * Exhaustive records: a new contract value fails to compile here.
 */
export const REQUEST_TYPE_LABELS: Record<RequestType, string> = {
  EquipmentPurchase: 'Equipment purchase',
  Leave: 'Leave',
}

export const LEAVE_TYPE_LABELS: Record<LeaveType, string> = {
  Annual: 'Annual leave',
  Sick: 'Sick leave',
  Unpaid: 'Unpaid leave',
  // Same words as the server's request title (RequestTitle.cs).
  Other: 'Other leave',
}

export const LEAVE_TYPES = Object.keys(LEAVE_TYPE_LABELS) as LeaveType[]

// Runtime lists of the contract's enum values. Typed as records so a new value fails to compile
// here instead of silently missing from a filter.
const STATUS_VALUES: Record<RequestStatus, null> = { InReview: null, Approved: null, Completed: null, Rejected: null }
export const REQUEST_STATUSES = Object.keys(STATUS_VALUES) as RequestStatus[]
export const REQUEST_TYPES = Object.keys(REQUEST_TYPE_LABELS) as RequestType[]

export const ROLE_LABELS: Record<Role, string> = {
  Administrator: 'Administrators',
  Employee: 'Employees',
  FinanceOfficer: 'Finance officers',
  HrOfficer: 'HR officers',
  ProcurementOfficer: 'Procurement officers',
}

/** One person's role as a chip or checkbox label. (`ROLE_LABELS` above is the plural group name.) */
export const ROLE_NAMES: Record<Role, string> = {
  Administrator: 'Administrator',
  Employee: 'Employee',
  FinanceOfficer: 'Finance officer',
  HrOfficer: 'HR officer',
  ProcurementOfficer: 'Procurement officer',
}

export const ROLES = Object.keys(ROLE_NAMES) as Role[]
