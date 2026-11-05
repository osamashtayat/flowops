import { http, HttpResponse } from 'msw'
import type {
  ChangePasswordRequest,
  CurrentUserDto,
  InboxItemDto,
  LoginRequest,
  PagedInboxItems,
  RequestDetailsDto,
  SubmitLeaveRequest,
  SubmitPurchaseRequest,
  ProblemDetails,
  UnreadCountDto,
  ValidationProblemDetails,
} from '@/lib/api/types'
import { currentActor } from './actor'
import { addMockLeave, addMockPurchase } from './requestStore'
import { getMockUserKey, hasChangedMockPassword, markMockPasswordChanged, setMockUser } from './state'
import {
  MOCK_LOCKED_OUT_EMAIL,
  MOCK_PASSWORD,
  MOCK_USERS,
  mockUserByEmail,
  type MockUserKey,
} from './users'

/**
 * Typed MSW handlers for the endpoints the app uses so far. Response bodies are the generated
 * schema types, so a contract change breaks compilation here. Nullable fields are always present
 * as `null`, like the real API. Anything not handled passes through to the real server (501 today).
 */

function problem(status: number, code: string): HttpResponse<ProblemDetails> {
  const body: ProblemDetails = {
    type: null,
    title: null,
    status,
    detail: null,
    instance: null,
    code,
    traceId: 'mock-trace-id',
  }
  return HttpResponse.json(body, { status })
}

function validationProblem(errors: Record<string, string[]>): HttpResponse<ValidationProblemDetails> {
  const body: ValidationProblemDetails = {
    type: null,
    title: null,
    status: 400,
    detail: null,
    instance: null,
    errors,
    code: 'validation',
    traceId: 'mock-trace-id',
  }
  return HttpResponse.json(body, { status: 400 })
}

/** The fake antiforgery cookie the real server sets on login and refreshes on /me. */
function setXsrfCookie() {
  document.cookie = 'XSRF-TOKEN=mock-xsrf-token; path=/; SameSite=Strict'
}

function currentUser(key: MockUserKey): CurrentUserDto {
  const user = MOCK_USERS[key]
  return { ...user, mustChangePassword: user.mustChangePassword && !hasChangedMockPassword(key) }
}

function inboxItems(user: CurrentUserDto): InboxItemDto[] {
  const finance = user.roles.includes('FinanceOfficer')
  const count = finance ? 5 : 3
  const stage: InboxItemDto['stage'] = finance ? 'FinanceApproval' : 'ManagerApproval'
  return Array.from({ length: count }, (_, index) => ({
    taskId: `7a1e0000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
    kind: 'Approval',
    stage,
    createdAt: new Date(Date.UTC(2026, 10, 2 + index)).toISOString(),
    version: `v${index + 1}`,
    status: 'Open',
    outcome: null,
    decidedAt: null,
    request: {
      id: `9b2f0000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
      number: `FO-2026-${String(100 + index).padStart(6, '0')}`,
      type: 'EquipmentPurchase',
      title: 'Dell XPS 15 laptop',
      requester: { id: MOCK_USERS.employee.id, displayName: MOCK_USERS.employee.displayName },
      department: { id: '3f0c5a2e-0000-4000-8000-000000000001', name: 'Engineering' },
      status: 'InReview',
      stage,
      submittedAt: new Date(Date.UTC(2026, 10, 2 + index)).toISOString(),
      closedAt: null,
      keyDetail: '1,250.500 JOD',
    },
  }))
}

/** Every handler may answer with its success body or a problem; the generics keep both typed. */
type Failure = ProblemDetails | ValidationProblemDetails

/** Sign-in endpoints. Tests use these; the dev browser talks to the real API for them instead. */
export const authHandlers = [
  http.get<never, never, CurrentUserDto | Failure>('/api/auth/me', () => {
    const key = getMockUserKey()
    if (!key) return problem(401, 'auth.unauthenticated')
    setXsrfCookie()
    return HttpResponse.json(currentUser(key))
  }),

  http.post<never, Partial<LoginRequest>, CurrentUserDto | Failure>('/api/auth/login', async ({ request }) => {
    const body = await request.json()
    const email = body.email ?? ''
    if (!email || !body.password) {
      return validationProblem({ [email ? 'password' : 'email']: ['This field is required.'] })
    }
    if (email.trim().toLowerCase() === MOCK_LOCKED_OUT_EMAIL) return problem(423, 'auth.locked_out')
    const key = mockUserByEmail(email)
    if (!key || body.password !== MOCK_PASSWORD) return problem(401, 'auth.invalid_credentials')
    setMockUser(key)
    setXsrfCookie()
    return HttpResponse.json(currentUser(key))
  }),

  http.post<never, never, Failure | null>('/api/auth/logout', () => {
    if (!getMockUserKey()) return problem(401, 'auth.unauthenticated')
    setMockUser(null)
    return new HttpResponse(null, { status: 204 })
  }),

  http.post<never, Partial<ChangePasswordRequest>, Failure | null>('/api/auth/change-password', async ({ request }) => {
    const key = getMockUserKey()
    if (!key) return problem(401, 'auth.unauthenticated')
    const body = await request.json()
    if (body.currentPassword !== MOCK_PASSWORD) {
      return validationProblem({ currentPassword: ['The current password is incorrect.'] })
    }
    if ((body.newPassword ?? '').length < 12) {
      return validationProblem({ newPassword: ['The password must be at least 12 characters long.'] })
    }
    markMockPasswordChanged(key)
    return new HttpResponse(null, { status: 204 })
  }),

]

/** Endpoints whose backend task isn't merged yet. They stay mocked in dev. */
/** Submitting needs a department and a manager (API_CONTRACT §4: 422 otherwise). */
function submitBlockedBy(user: CurrentUserDto): HttpResponse<ProblemDetails> | null {
  if (!user.capabilities.includes('requests.submit')) return problem(403, 'auth.forbidden')
  if (!user.department) return problem(422, 'request.department_not_assigned')
  if (!user.manager) return problem(422, 'request.manager_not_assigned')
  return null
}

export const dataHandlers = [
  http.post<never, SubmitPurchaseRequest, RequestDetailsDto | Failure>('/api/requests/purchase', async ({ request }) => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    const blocked = submitBlockedBy(user)
    if (blocked) return blocked
    return HttpResponse.json(addMockPurchase(user, await request.json()), { status: 201 })
  }),

  http.post<never, SubmitLeaveRequest, RequestDetailsDto | Failure>('/api/requests/leave', async ({ request }) => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    const blocked = submitBlockedBy(user)
    if (blocked) return blocked
    return HttpResponse.json(addMockLeave(user, await request.json()), { status: 201 })
  }),

  http.get<never, never, PagedInboxItems | Failure>('/api/tasks', async ({ request }) => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    if (!user.capabilities.includes('tasks.view')) return problem(403, 'auth.forbidden')
    const url = new URL(request.url)
    // The generated client sends the names as openapi.json spells them (`Page`, `PageSize`,
    // `Status`); ASP.NET binds case-insensitively, so the mock does too.
    const query = (name: string) =>
      [...url.searchParams].find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1] ?? null
    const page = Number(query('page') ?? 1)
    const pageSize = Number(query('pageSize') ?? 20)
    const open = query('status') === 'Done' ? [] : inboxItems(user)
    const body: PagedInboxItems = {
      items: open.slice((page - 1) * pageSize, page * pageSize),
      page,
      pageSize,
      totalCount: open.length,
    }
    return HttpResponse.json(body)
  }),

  http.get<never, never, UnreadCountDto | Failure>('/api/notifications/unread-count', async () => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    return HttpResponse.json({ count: user.roles.includes('Employee') && !user.capabilities.includes('tasks.view') ? 2 : 0 })
  }),
]

export const handlers = [...authHandlers, ...dataHandlers]
