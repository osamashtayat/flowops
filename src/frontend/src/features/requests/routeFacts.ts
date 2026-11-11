import { calendarDaysInclusive } from '@/lib/format/date'
import type { RouteFactsInput } from '@/lib/api/types'
import { leaveSchema, purchaseSchema } from './schemas'

/**
 * The facts the route preview needs (API_CONTRACT §4), or `null` while the form value is not valid yet.
 * Validity reuses the submit schemas, so the preview never asks about something the form would reject.
 */
export function purchaseFacts(estimatedCost: string): RouteFactsInput | null {
  return purchaseSchema.shape.estimatedCost.safeParse(estimatedCost).success ? { amount: { amount: estimatedCost, currency: 'JOD' } } : null
}

export function leaveFacts(startDate: string, endDate: string, leaveType: string): RouteFactsInput | null {
  const parsed = leaveSchema.safeParse({ startDate, endDate, leaveType, reason: '' })
  if (!parsed.success) return null
  const days = calendarDaysInclusive(startDate, endDate)
  return days === null ? null : { leaveType: parsed.data.leaveType, days }
}
