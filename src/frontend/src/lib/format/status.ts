import type { RequestStatus, WorkTaskOutcome } from '@/lib/api/types'

/**
 * Single source for how request/task statuses are presented (label + tone + icon).
 * The status values come from the generated contract types; the exhaustive switches below
 * stop compiling if the contract gains a value that has no presentation.
 */
export type { RequestStatus, WorkTaskOutcome }

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
