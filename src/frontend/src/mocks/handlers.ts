import { http, HttpResponse } from 'msw'
import type {
  ChangePasswordRequest,
  CurrentUserDto,
  InboxItemDto,
  LoginRequest,
  PagedInboxItems,
  ProblemDetails,
  UnreadCountDto,
  ValidationProblemDetails,
} from '@/lib/api/types'
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

const OPEN_TASKS: Partial<Record<MockUserKey, { count: number; stage: InboxItemDto['stage'] }>> = {
  manager: { count: 3, stage: 'ManagerApproval' },
  finance: { count: 5, stage: 'FinanceApproval' },
}

function inboxItems(key: MockUserKey): InboxItemDto[] {
  const open = OPEN_TASKS[key]
  if (!open) return []
  return Array.from({ length: open.count }, (_, index) => ({
    taskId: `7a1e0000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
    kind: 'Approval',
    stage: open.stage,
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
      stage: open.stage,
      submittedAt: new Date(Date.UTC(2026, 10, 2 + index)).toISOString(),
      closedAt: null,
      keyDetail: '1,250.500 JOD',
    },
  }))
}

/** Every handler may answer with its success body or a problem; the generics keep both typed. */
type Failure = ProblemDetails | ValidationProblemDetails

export const handlers = [
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

  http.get<never, never, PagedInboxItems | Failure>('/api/tasks', ({ request }) => {
    const key = getMockUserKey()
    if (!key) return problem(401, 'auth.unauthenticated')
    if (!MOCK_USERS[key].capabilities.includes('tasks.view')) return problem(403, 'auth.forbidden')
    const url = new URL(request.url)
    // The generated client sends the names as openapi.json spells them (`Page`, `PageSize`,
    // `Status`); ASP.NET binds case-insensitively, so the mock does too.
    const query = (name: string) =>
      [...url.searchParams].find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1] ?? null
    const page = Number(query('page') ?? 1)
    const pageSize = Number(query('pageSize') ?? 20)
    const open = query('status') === 'Done' ? [] : inboxItems(key)
    const body: PagedInboxItems = {
      items: open.slice((page - 1) * pageSize, page * pageSize),
      page,
      pageSize,
      totalCount: open.length,
    }
    return HttpResponse.json(body)
  }),

  http.get<never, never, UnreadCountDto | Failure>('/api/notifications/unread-count', () => {
    const key = getMockUserKey()
    if (!key) return problem(401, 'auth.unauthenticated')
    return HttpResponse.json({ count: key === 'employee' ? 2 : 0 })
  }),
]
