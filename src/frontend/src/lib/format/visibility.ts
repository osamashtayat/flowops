import type { RequestType } from '@/lib/api/types'

/**
 * Free text on a leave request (comments, approval notes, rejection reasons) is visible to everyone
 * who can see the request, while the leave reason itself is masked (docs/AUTHORIZATION.md §5), so
 * authors must not repeat it. The one home for this wording; shown only for leave requests.
 */
export const LEAVE_TEXT_VISIBILITY_WARNING = "Visible to everyone who can see this request. Don't repeat the leave reason."

/** `helper` with the leave warning appended when the request is a leave request. */
export function withLeaveVisibilityWarning(helper: string | undefined, requestType: RequestType): string | undefined {
  if (requestType !== 'Leave') return helper
  return helper ? `${helper} ${LEAVE_TEXT_VISIBILITY_WARNING}` : LEAVE_TEXT_VISIBILITY_WARNING
}
