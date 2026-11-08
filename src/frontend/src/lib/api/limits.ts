/**
 * The ONE place for request-related field limits. Values come from docs/API_CONTRACT.md §4–§5 and
 * match `contracts/openapi.json` (limits.test.ts fails if they drift). Zod schemas, helper text and
 * character counters import from here — never type a number like 150 into a component.
 */
export const REQUEST_LIMITS = {
  /** SubmitPurchaseRequest.itemName, 1–150 */
  itemNameMax: 150,
  /** SubmitPurchaseRequest.description, 1–2000 */
  descriptionMax: 2000,
  /** SubmitPurchaseRequest.justification, 1–2000 */
  justificationMax: 2000,
  /** SubmitPurchaseRequest.estimatedCost.amount: > 0 and ≤ 1,000,000, written as a string. */
  estimatedCostMax: '1000000',
  /** SubmitLeaveRequest.reason, optional, ≤ 1000 */
  leaveReasonMax: 1000,
  /** Leave length: end ≥ start and ≤ 90 days. */
  leaveMaxDays: 90,
  /** AddCommentRequest.body, 1–2000 */
  commentMax: 2000,
  /** ApproveTaskRequest.comment, ≤ 2000 */
  approveCommentMax: 2000,
  /** RejectTaskRequest.reason, 1–2000 */
  rejectReasonMax: 2000,
  /** FulfillTaskRequest.note, ≤ 2000 */
  fulfillNoteMax: 2000,
  /** GET /api/requests `search`, ≤ 100 */
  searchMax: 100,
} as const

/** Helper text such as "Up to 150 characters." so the wording lives next to the number. */
export function upToCharacters(max: number): string {
  return `Up to ${max} characters.`
}

/**
 * Administration field limits (docs/API_CONTRACT.md §8, `contracts/openapi.json`; limits.test.ts
 * fails if they drift).
 */
export const ADMIN_LIMITS = {
  /** CreateUserRequest.email, 1–256 */
  emailMax: 256,
  /** CreateUserRequest/UpdateUserRequest.displayName, 1–100 */
  displayNameMax: 100,
  /** CreateUserRequest.initialPassword and ResetPasswordRequest.newPassword, 12–256 */
  passwordMin: 12,
  passwordMax: 256,
  /** CreateDepartmentRequest.name, 1–100 */
  departmentNameMax: 100,
  /** CreateDepartmentRequest.code, 1–16, letters, digits and hyphens */
  departmentCodeMax: 16,
  /** RejectRoleChangeRequest.reason, 1–500 */
  roleChangeReasonMax: 500,
} as const
