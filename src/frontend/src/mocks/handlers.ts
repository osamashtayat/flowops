import { http, HttpResponse } from 'msw'
import type {
  ChangePasswordRequest,
  CurrentUserDto,
  LoginRequest,
  PagedInboxItems,
  ActivityDto,
  AddCommentRequest,
  DashboardDto,
  PagedRequestSummaries,
  RequestDetailsDto,
  SubmitLeaveRequest,
  SubmitPurchaseRequest,
  ProblemDetails,
  UnreadCountDto,
  ValidationProblemDetails,
  CreateDepartmentRequest,
  CreateUserRequest,
  DepartmentDto,
  PagedStalledTasks,
  PagedUsers,
  Role,
  UpdateDepartmentRequest,
  UpdateUserRequest,
  UserDto,
} from '@/lib/api/types'
import type { RoleChangeStatus } from '@/lib/api/pendingRoleChangeContract'
import { currentActor } from './actor'
import {
  createMockDepartment,
  createMockUser,
  decideMockRoleChange,
  getMockUser,
  isAdminFailure,
  listMockDepartments,
  listMockRoleChanges,
  listMockStalledTasks,
  listMockUsers,
  resetMockPassword,
  updateMockDepartment,
  updateMockRoles,
  updateMockUser,
  type AdminFailure,
} from './adminStore'
import {
  addMockComment,
  addMockLeave,
  addMockPurchase,
  buildMockDashboard,
  getMockRequestFor,
  listMockInbox,
  listMockRequests,
  mockActivities,
} from './requestStore'
import { decideMockTask } from './requestWorkflow'
import { getMockUserKey, hasChangedMockPassword, markMockPasswordChanged, setMockUser } from './state'
import {
  MOCK_RATE_LIMITED_EMAIL,
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
    if (email.trim().toLowerCase() === MOCK_RATE_LIMITED_EMAIL) return problem(429, 'auth.rate_limited')
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
/**
 * A query parameter by name, ignoring case: the generated client sends the names as openapi.json
 * spells them (camelCase since BE-11) and ASP.NET binds case-insensitively, so the mock does too.
 */
function queryParam(request: Request) {
  const params = [...new URL(request.url).searchParams]
  return (name: string) => params.find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1] ?? null
}

/** Submitting needs a department and a manager (API_CONTRACT §4: 422 otherwise). */
function submitBlockedBy(user: CurrentUserDto): HttpResponse<ProblemDetails> | null {
  if (!user.capabilities.includes('requests.submit')) return problem(403, 'auth.forbidden')
  if (!user.department) return problem(422, 'request.department_not_assigned')
  if (!user.manager) return problem(422, 'request.manager_not_assigned')
  return null
}

/** The shared preamble of the request and decision endpoints: who is asking. */
const NOT_FOUND = () => problem(404, 'not_found')
/** Request submit, list, details, activity and comments — real in the backend since BE-10/BE-11, so only tests use these. */
export const requestHandlers = [
  http.get<{ id: string }, never, RequestDetailsDto | Failure>('/api/requests/:id', async ({ params }) => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    const request = getMockRequestFor(user, params.id)
    return request ? HttpResponse.json(request) : NOT_FOUND()
  }),

  http.get<{ id: string }, never, ActivityDto[] | Failure>('/api/requests/:id/activity', async ({ params }) => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    const request = getMockRequestFor(user, params.id)
    return request ? HttpResponse.json(mockActivities(request)) : NOT_FOUND()
  }),

  http.post<{ id: string }, AddCommentRequest, ActivityDto | Failure>('/api/requests/:id/comments', async ({ params, request }) => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    const target = getMockRequestFor(user, params.id)
    if (!target) return NOT_FOUND()
    const { body } = await request.json()
    if (!body.trim()) return validationProblem({ body: ['The comment cannot be empty.'] })
    return HttpResponse.json(addMockComment(user, target, body.trim()), { status: 201 })
  }),

  http.get<never, never, PagedRequestSummaries | Failure>('/api/requests', async ({ request }) => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    const query = queryParam(request)
    const scope = query('scope') === 'visible' ? 'visible' : 'mine'
    // `visible` is for managers and officers (the Team requests page); everyone else sees their own.
    if (scope === 'visible' && !user.capabilities.includes('tasks.view')) return problem(403, 'auth.forbidden')
    return HttpResponse.json(
      listMockRequests(user, {
        scope,
        status: query('status'),
        type: query('type'),
        search: query('search'),
        page: Number(query('page') ?? 1),
        pageSize: Number(query('pageSize') ?? 20),
      }),
    )
  }),

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
]

/** A mock admin call: 401 when signed out, 403 for anyone without `admin.manage`, else `run`'s result. */
async function asAdmin<T>(run: (admin: CurrentUserDto) => T | AdminFailure | Promise<T | AdminFailure>, success: (value: T) => Response): Promise<Response> {
  const user = await currentActor()
  if (!user) return problem(401, 'auth.unauthenticated')
  if (!user.capabilities.includes('admin.manage')) return problem(403, 'auth.forbidden')
  const result = await run(user)
  if (isAdminFailure(result)) {
    return result.field ? validationProblem({ [result.field]: ['Not valid.'] }) : problem(result.status, result.code)
  }
  return success(result)
}

/** Departments and users — real in the backend since BE-08, so only tests use these. */
export const adminHandlers = [
  http.get<never, never, DepartmentDto[] | Failure>('/api/departments', async ({ request }) => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    const includeInactive = queryParam(request)('includeInactive') === 'true' && user.capabilities.includes('admin.manage')
    return HttpResponse.json(listMockDepartments(includeInactive))
  }),
  http.post<never, CreateDepartmentRequest>('/api/departments', async ({ request }) => {
    const body = await request.json()
    return asAdmin(() => createMockDepartment(body), (d) => HttpResponse.json(d, { status: 201 }))
  }),
  http.put<{ id: string }, UpdateDepartmentRequest>('/api/departments/:id', async ({ params, request }) => {
    const body = await request.json()
    return asAdmin(() => updateMockDepartment(params.id, body), (d) => HttpResponse.json(d))
  }),
  http.get('/api/users', ({ request }) => {
    const query = queryParam(request)
    return asAdmin(
      () =>
        listMockUsers({
          search: query('search') ?? '',
          departmentId: query('departmentId') ?? '',
          role: query('role') ?? '',
          includeInactive: query('includeInactive') === 'true',
          page: Number(query('page') ?? 1),
          pageSize: Number(query('pageSize') ?? 20),
        }) satisfies PagedUsers,
      (page) => HttpResponse.json(page),
    )
  }),
  http.get<{ id: string }>('/api/users/:id', ({ params }) =>
    asAdmin(() => getMockUser(params.id) ?? ({ status: 404, code: 'not_found' } satisfies AdminFailure), (u: UserDto) => HttpResponse.json(u)),
  ),
  http.post<never, CreateUserRequest>('/api/users', async ({ request }) => {
    const body = await request.json()
    return asAdmin((admin) => createMockUser(admin.id, body), (u) => HttpResponse.json(u, { status: 201 }))
  }),
  http.put<{ id: string }, UpdateUserRequest>('/api/users/:id', async ({ params, request }) => {
    const body = await request.json()
    return asAdmin((admin) => updateMockUser(admin.id, params.id, body), (u) => HttpResponse.json(u))
  }),
  http.put<{ id: string }, { roles: Role[] }>('/api/users/:id/roles', async ({ params, request }) => {
    const body = await request.json()
    return asAdmin(
      (admin) => updateMockRoles(admin.id, params.id, body.roles),
      // Your own roles are queued for approval (ADR-0012): 202 with the role change, not the user.
      (result) => HttpResponse.json(result, { status: 'proposedRoles' in result ? 202 : 200 }),
    )
  }),
  http.get('/api/admin/role-changes', ({ request }) => {
    const query = queryParam(request)
    return asAdmin(
      (admin) => listMockRoleChanges(admin.id, { status: (query('status') ?? '') as RoleChangeStatus | '', page: Number(query('page') ?? 1), pageSize: Number(query('pageSize') ?? 20) }),
      (page) => HttpResponse.json(page),
    )
  }),
  ...(['approve', 'reject', 'cancel'] as const).map((action) =>
    http.post<{ id: string }, { version: string; reason?: string }>(`/api/admin/role-changes/:id/${action}`, async ({ params, request }) => {
      const body = await request.json()
      return asAdmin((admin) => decideMockRoleChange(admin.id, params.id, action, body), (change) => HttpResponse.json(change))
    }),
  ),
  http.post<{ id: string }>('/api/users/:id/reset-password', ({ params }) =>
    asAdmin(() => resetMockPassword(params.id) ?? true, () => new HttpResponse(null, { status: 204 })),
  ),
]

/** Approve / reject / fulfil — backend BE-12 not merged, but these act on the mock requests above, so only tests use them. */
export const decisionHandlers = [
  ...(['approve', 'reject', 'fulfill'] as const).map((action) =>
    http.post<{ taskId: string }, { version: string; comment?: string | null; reason?: string; note?: string | null }, Failure | null>(
      `/api/tasks/:taskId/${action}`,
      async ({ params, request }) => {
        const user = await currentActor()
        if (!user) return problem(401, 'auth.unauthenticated')
        const body = await request.json()
        const text = body.comment ?? body.reason ?? body.note ?? null
        const failure = decideMockTask(user, params.taskId, action, body.version, text)
        if (!failure) return new HttpResponse(null, { status: 204 })
        return failure.status === 400 ? validationProblem({ reason: ['Enter a reason.'] }) : problem(failure.status, failure.code)
      },
    ),
  ),
]

/** Endpoints whose backend task isn't merged yet (dashboard BE-15, tasks BE-12, notifications BE-13). Also mocked in the dev browser. */
export const pendingHandlers = [
  http.get<never, never, DashboardDto | Failure>('/api/dashboard', async () => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    return HttpResponse.json(buildMockDashboard(user))
  }),

  http.get<never, never, PagedInboxItems | Failure>('/api/tasks', async ({ request }) => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    if (!user.capabilities.includes('tasks.view')) return problem(403, 'auth.forbidden')
    const query = queryParam(request)
    const page = Number(query('page') ?? 1)
    const pageSize = Number(query('pageSize') ?? 20)
    const status = query('status') === 'Done' ? 'Done' : 'Open'
    return HttpResponse.json(listMockInbox(user, { status, page, pageSize }))
  }),

  http.get<never, never, PagedStalledTasks | Failure>('/api/admin/stalled-tasks', async ({ request }) => {
    const query = queryParam(request)
    return asAdmin(() => listMockStalledTasks(Number(query('page') ?? 1), Number(query('pageSize') ?? 20)), (page) => HttpResponse.json(page))
  }),

  http.get<never, never, UnreadCountDto | Failure>('/api/notifications/unread-count', async () => {
    const user = await currentActor()
    if (!user) return problem(401, 'auth.unauthenticated')
    return HttpResponse.json({ count: user.roles.includes('Employee') && !user.capabilities.includes('tasks.view') ? 2 : 0 })
  }),
]

export const handlers = [...authHandlers, ...adminHandlers, ...requestHandlers, ...decisionHandlers, ...pendingHandlers]
