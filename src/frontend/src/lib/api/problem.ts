import { ApiError, NETWORK_ERROR_CODE } from './apiError'

/**
 * User-facing text for every error `code` the API can return. The code list lives in
 * `FlowOps.Domain/Errors/ErrorCodes.cs` and docs/API_CONTRACT.md §2; problem.test.ts fails if a
 * code exists there without a message here. Say what happened and what to do next.
 */
export const PROBLEM_MESSAGES = {
  validation: 'Some of the information is not valid. Check the highlighted fields and try again.',
  not_found: "This item doesn't exist or you don't have access to it.",
  'server.error': 'Something went wrong on our side. Try again in a moment.',
  'concurrency.conflict': 'This item was changed by someone else. It has been refreshed; review it and try again.',

  'auth.unauthenticated': 'Your session has ended. Please sign in again.',
  'auth.forbidden': "You don't have permission to do that.",
  'auth.password_change_required': 'You must change your password before continuing.',
  'auth.invalid_credentials': 'The email or password is incorrect. After 5 failed attempts, sign-in is paused for 15 minutes.',
  'auth.rate_limited': 'Too many sign-in attempts. Please wait a few minutes and try again.',
  'request.comment_rate_limited': "You're adding comments too quickly. Please wait a moment and try again.",

  'task.not_eligible': "You can't decide this task.",
  'task.self_decision': "You can't decide your own request.",
  'task.already_decided': 'Someone else already handled this task. The inbox has been refreshed.',
  'task.not_current': 'This step is no longer the current one. The request has been refreshed.',
  'task.invalid_action': "That action isn't available for this task.",

  'request.closed': 'This request is already closed.',
  'request.manager_not_assigned': "You don't have a manager assigned yet. Ask an administrator to set one, then try again.",
  'request.department_not_assigned': "You don't belong to a department yet. Ask an administrator to assign one, then try again.",

  'department.duplicate': 'A department with this name or code already exists.',
  'department.inactive': 'That department is inactive. Choose an active one.',

  'user.duplicate_email': 'A user with this email already exists.',
  'user.manager_cycle': "A person can't report to themselves or to someone who reports to them.",
  'user.last_admin': 'At least one active administrator is required.',
  'user.self_administration': 'Another administrator has to make this change.',
} as const satisfies Record<string, string>

export type ProblemCode = keyof typeof PROBLEM_MESSAGES

const NETWORK_MESSAGE = "Can't reach the server. Check your connection and try again."
const GENERIC_MESSAGE = 'Something went wrong. Try again.'
const NOT_AVAILABLE_MESSAGE = "This feature isn't available yet."

function isKnownCode(code: string): code is ProblemCode {
  return Object.hasOwn(PROBLEM_MESSAGES, code)
}

/** The text to show a user for any thrown value. Mapped from `code`, with a status-based fallback. */
export function toUserMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return GENERIC_MESSAGE
  if (error.code === NETWORK_ERROR_CODE) return NETWORK_MESSAGE
  // 501: the endpoint exists in the contract but its backend task isn't done. Checked before the
  // code because the server may label it `server.error`; retrying won't help.
  if (error.status === 501) return NOT_AVAILABLE_MESSAGE
  if (isKnownCode(error.code)) return PROBLEM_MESSAGES[error.code]
  if (error.status === 401) return PROBLEM_MESSAGES['auth.unauthenticated']
  if (error.status === 403) return PROBLEM_MESSAGES['auth.forbidden']
  if (error.status === 404) return PROBLEM_MESSAGES.not_found
  if (error.status >= 500) return PROBLEM_MESSAGES['server.error']
  return GENERIC_MESSAGE
}

/**
 * The first server message for a field path (`newPassword`, `estimatedCost.amount`) from a 400
 * response, or undefined. Lets forms show server validation on the right field.
 */
export function serverFieldError(error: unknown, field: string): string | undefined {
  return error instanceof ApiError ? error.fieldErrors[field]?.[0] : undefined
}
