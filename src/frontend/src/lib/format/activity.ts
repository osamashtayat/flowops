import type { ActivityDto, ActivityType } from '@/lib/api/types'
import { requestStageLabel } from './status'

/**
 * How an activity entry reads in a timeline: "<actor> approved the Finance approval step".
 * Exhaustive over the contract's ActivityType, so a new type cannot ship without a sentence.
 * The comment text itself (`body`) is shown separately by the timeline, never part of this sentence.
 */
export function activityAction(entry: Pick<ActivityDto, 'type' | 'stage'>): string {
  const step = entry.stage ? ` the ${requestStageLabel(entry.stage)} step` : ''
  switch (entry.type) {
    case 'Submitted':
      return 'submitted the request'
    case 'TaskAssigned':
      return `A task was assigned${entry.stage ? `: ${requestStageLabel(entry.stage)}` : ''}`
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
    default:
      return entry.type satisfies never
  }
}

export type { ActivityType }
