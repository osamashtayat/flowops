import type { ActivityDto, ActivityType } from '@/lib/api/types'

/**
 * How an activity entry reads in a timeline: "<actor> approved the Finance approval step".
 * Exhaustive over the contract's ActivityType, so a new type cannot ship without a sentence.
 * The comment text itself (`body`) is shown separately by the timeline, never part of this sentence.
 */
export function activityAction(entry: Pick<ActivityDto, 'type' | 'step'>): string {
  const step = entry.step ? ` the ${entry.step.name} step` : ''
  switch (entry.type) {
    case 'Submitted':
      return 'submitted the request'
    case 'TaskAssigned':
      return `A task was assigned${entry.step ? `: ${entry.step.name}` : ''}`
    case 'Approved':
      return `approved${step}`
    case 'Rejected':
      return `rejected${step}`
    case 'Fulfilled':
      return `fulfilled${step}`
    case 'Completed':
      return 'The request was completed'
    case 'CommentAdded':
      return 'commented'
    case 'StepSkipped':
      // Only the server-provided step name: skipped steps are admin-defined (ADR-0014), so there is no label table.
      return `The ${entry.step?.name ?? 'next'} step was skipped`
    default:
      return entry.type satisfies never
  }
}

export type { ActivityType }
