import type { UserDto, UserRef } from './types'

/**
 * PROVISIONAL — remove when BE-08d merges (FE-13b).
 *
 * ADR-0012 §6 (accepted) fixes the role-change contract, but `contracts/openapi.json` does not
 * have it until BE-08d lands, and `schema.d.ts` must never be hand-edited. Until then the shapes
 * below are copied from the ADR, in one place, so nothing else in the app depends on them
 * directly (features import the aliases from `./types` after the swap).
 *
 * When BE-08d merges: `npm run api:types`, replace this file's types with aliases in `types.ts`,
 * move `PENDING_PROBLEM_MESSAGES` into `PROBLEM_MESSAGES`, switch `pendingApi` calls to `api`,
 * delete this file, and tick the box in docs/AGENT_HANDOFF.md §6 ("Contract request").
 */
export type RoleChangeStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled' | 'Expired'
export type RoleChangeAction = 'approve' | 'reject' | 'cancel'
type Role = UserDto['roles'][number]

export interface RoleChangeDto {
  id: string
  user: UserRef
  proposedRoles: Role[]
  previousRoles: Role[]
  status: RoleChangeStatus
  proposedAt: string
  expiresAt: string
  decidedBy: UserRef | null
  decidedAt: string | null
  rejectionReason: string | null
  version: string
  availableActions: RoleChangeAction[]
}

export interface PagedRoleChanges {
  items: RoleChangeDto[]
  page: number
  pageSize: number
  totalCount: number
}

type Json<T> = { content: { 'application/json': T } }
type Op<Query, Body, Responses> = {
  parameters: { query?: Query; header?: never; path?: { id: string }; cookie?: never }
  requestBody?: [Body] extends [never] ? never : { content: { 'application/json': Body } }
  responses: Responses
}

/** The endpoints of ADR-0012 §6 in the shape `openapi-fetch` expects. */
export interface PendingRoleChangePaths {
  '/api/admin/role-changes': {
    get: Op<{ status?: RoleChangeStatus; page?: number; pageSize?: number }, never, { 200: Json<PagedRoleChanges> }>
  }
  '/api/admin/role-changes/{id}/approve': { post: Op<never, { version: string }, { 200: Json<RoleChangeDto> }> }
  '/api/admin/role-changes/{id}/reject': { post: Op<never, { version: string; reason: string }, { 200: Json<RoleChangeDto> }> }
  '/api/admin/role-changes/{id}/cancel': { post: Op<never, { version: string }, { 200: Json<RoleChangeDto> }> }
  /** 200 `UserDto` for another user; 202 `RoleChangeDto` for the caller's own roles. */
  '/api/users/{id}/roles': { put: Op<never, { roles: Role[] }, { 200: Json<UserDto>; 202: Json<RoleChangeDto> }> }
}

/** Messages for the new error codes. They join `PROBLEM_MESSAGES` once the backend defines the codes. */
export const PENDING_PROBLEM_MESSAGES = {
  'user.admin_role_on_create_only': 'The Administrator role can only be given when an account is created.',
  'role_change.no_reviewer': 'There is no other active administrator to approve this. Create a second administrator account first.',
  'role_change.already_pending': 'You already have a role change waiting for approval. Cancel it before proposing another.',
  'role_change.self_approval': "You can't approve or reject your own role change.",
  'role_change.not_pending': 'This role change was already decided, cancelled or has expired. The list has been refreshed.',
  'role_change.stale': "This person's roles changed after the request was made, so it can't be applied. The list has been refreshed.",
  'role_change.not_proposer': 'Only the administrator who proposed this change can cancel it.',
} as const satisfies Record<string, string>
