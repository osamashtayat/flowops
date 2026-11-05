import type { LeaveType, RequestType } from '@/lib/api/types'

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
