import type { LeaveType, RequestStatus, RequestType, Role } from '@/lib/api/types'

/** How request types and leave types are named for people. Exhaustive: a new contract value fails to compile here. */
export const REQUEST_TYPE_LABELS: Record<RequestType, string> = {
  EquipmentPurchase: 'Equipment purchase',
  Leave: 'Leave',
}

export const LEAVE_TYPE_LABELS: Record<LeaveType, string> = {
  Annual: 'Annual leave',
  Sick: 'Sick leave',
  Unpaid: 'Unpaid leave',
  Other: 'Other',
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
