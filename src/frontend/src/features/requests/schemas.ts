import { z } from 'zod'
import { calendarDaysInclusive, todayInCompanyZone } from '@/lib/format/date'
import { moneyAmountSchema } from '@/lib/forms/money'
import { LEAVE_TYPES } from './labels'
import { REQUEST_LIMITS, upToCharacters } from './limits'

/**
 * Client-side checks mirroring docs/API_CONTRACT.md §4 for fast feedback. The server validates
 * again and stays the authority; its 400 `errors` are shown on the matching field.
 */
const requiredText = (label: string, max: number) =>
  z.string().trim().min(1, `Enter ${label}.`).max(max, `Use ${max} characters or fewer.`)

export const purchaseSchema = z.object({
  itemName: requiredText('the item name', REQUEST_LIMITS.itemNameMax),
  description: requiredText('a description', REQUEST_LIMITS.descriptionMax),
  // Kept as a string end to end; Number() is only used to compare against the range.
  estimatedCost: moneyAmountSchema.refine((amount) => Number(amount) > 0, 'The cost must be greater than 0.').refine(
    (amount) => Number(amount) <= Number(REQUEST_LIMITS.estimatedCostMax),
    'The cost can be at most 1,000,000.000 JOD.',
  ),
  justification: requiredText('a business justification', REQUEST_LIMITS.justificationMax),
})
export type PurchaseValues = z.infer<typeof purchaseSchema>

export const leaveSchema = z
  .object({
    startDate: z.string().min(1, 'Choose a start date.'),
    endDate: z.string().min(1, 'Choose an end date.'),
    leaveType: z.enum(LEAVE_TYPES, 'Choose a leave type.'),
    reason: z.string().trim().max(REQUEST_LIMITS.leaveReasonMax, upToCharacters(REQUEST_LIMITS.leaveReasonMax)),
  })
  .superRefine((values, context) => {
    const issue = (path: 'startDate' | 'endDate', message: string) => context.addIssue({ code: 'custom', path: [path], message })
    if (values.startDate && values.startDate < todayInCompanyZone()) {
      issue('startDate', 'The start date cannot be in the past.')
    }
    if (!values.startDate || !values.endDate) return
    const days = calendarDaysInclusive(values.startDate, values.endDate)
    if (days === null) issue('endDate', 'The end date cannot be before the start date.')
    else if (days > REQUEST_LIMITS.leaveMaxDays) issue('endDate', `Leave can be at most ${REQUEST_LIMITS.leaveMaxDays} days.`)
  })
export type LeaveValues = z.infer<typeof leaveSchema>
