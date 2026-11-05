/**
 * Single source for how request/task statuses are presented (label + tone + icon).
 * Values mirror docs/API_CONTRACT.md §1. Once FE-01 generates schema.d.ts, replace these
 * local unions with the generated types — do not add values that aren't in the contract.
 */
export type RequestStatus = 'InReview' | 'Approved' | 'Completed' | 'Rejected'
export type WorkTaskOutcome = 'Approved' | 'Rejected' | 'Fulfilled'

export type StatusTone = 'info' | 'success' | 'warning' | 'danger' | 'neutral'
export type StatusIcon = 'clock' | 'check' | 'check-circle' | 'x-circle'

export interface StatusPresentation {
  label: string
  tone: StatusTone
  icon: StatusIcon
}

export function requestStatusPresentation(status: RequestStatus): StatusPresentation {
  switch (status) {
    case 'InReview':
      return { label: 'Pending review', tone: 'warning', icon: 'clock' }
    case 'Approved':
      return { label: 'Approved', tone: 'info', icon: 'check' }
    case 'Completed':
      return { label: 'Completed', tone: 'success', icon: 'check-circle' }
    case 'Rejected':
      return { label: 'Rejected', tone: 'danger', icon: 'x-circle' }
    default:
      return status satisfies never
  }
}

export function taskStatusPresentation(outcome: WorkTaskOutcome | null): StatusPresentation {
  switch (outcome) {
    case null:
      return { label: 'Awaiting action', tone: 'warning', icon: 'clock' }
    case 'Approved':
      return { label: 'Approved', tone: 'success', icon: 'check' }
    case 'Fulfilled':
      return { label: 'Fulfilled', tone: 'success', icon: 'check-circle' }
    case 'Rejected':
      return { label: 'Rejected', tone: 'danger', icon: 'x-circle' }
    default:
      return outcome satisfies never
  }
}
