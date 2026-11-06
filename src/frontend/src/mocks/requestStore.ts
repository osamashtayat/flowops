import type {
  ActivityDto,
  CurrentUserDto,
  InboxItemDto,
  PagedInboxItems,
  DashboardDto,
  RequestDetailsDto,
  RequestStage,
  RequestStatus,
  RequestSummaryDto,
  Role,
  SubmitLeaveRequest,
  SubmitPurchaseRequest,
  TaskAction,
  UserRef,
  WorkTaskDto,
} from '@/lib/api/types'
import { calendarDaysInclusive, formatDateOnly } from '@/lib/format/date'
import { formatMoney } from '@/lib/format/money'
import { MOCK_USERS } from './users'

/**
 * In-memory requests for the mock API, reset between tests. Only what a mocked endpoint needs;
 * the real rules (numbering, routing, visibility, eligibility) live in the backend and are only
 * imitated here, coarsely, so screens can be exercised without it.
 */
const MOCK_VERSION = 'v1'
const SEEDED_STATUSES: RequestStatus[] = ['InReview', 'Approved', 'Completed', 'Rejected']

let created: RequestDetailsDto[] = []
let seeded: RequestDetailsDto[] = []
let comments = new Map<string, ActivityDto[]>()

export function resetMockRequests() {
  created = []
  seeded = buildSeeded()
  comments = new Map()
}

const userRef = (user: CurrentUserDto): UserRef => ({ id: user.id, displayName: user.displayName })
const EMPLOYEE = userRef(MOCK_USERS.employee)
const MANAGER = userRef(MOCK_USERS.manager)
const FINANCE = userRef(MOCK_USERS.finance)

export function mockTask(init: {
  id: string
  stage: RequestStage
  createdAt: string
  assignee: WorkTaskDto['assignee']
  decided?: { outcome: 'Approved' | 'Fulfilled'; by: UserRef; at: string; comment?: string | null }
}): WorkTaskDto {
  return {
    id: init.id,
    kind: init.stage === 'ProcurementFulfillment' ? 'Fulfillment' : 'Approval',
    stage: init.stage,
    status: init.decided ? 'Done' : 'Open',
    assignee: init.assignee,
    outcome: init.decided?.outcome ?? null,
    decidedBy: init.decided?.by ?? null,
    decidedAt: init.decided?.at ?? null,
    comment: init.decided?.comment ?? null,
    createdAt: init.createdAt,
  }
}

/** Seeded in-review requests 4, 12 and 20 still wait for the manager; the rest wait for Finance. */
const waitsForManager = (index: number, status: RequestStatus) => status === 'InReview' && index % 8 === 4

function seededTasks(id: string, status: RequestStatus, at: string, atManager: boolean): WorkTaskDto[] {
  const task = (n: number, stage: RequestStage, assignee: WorkTaskDto['assignee'], decided?: { by: UserRef; comment?: string }) =>
    mockTask({
      id: `${id}-t${n}`,
      stage,
      createdAt: at,
      assignee,
      decided: decided ? { outcome: 'Approved', by: decided.by, at, comment: decided.comment } : undefined,
    })
  const manager = { user: MANAGER, role: null }
  const finance = { user: null, role: 'FinanceOfficer' as Role }
  switch (status) {
    case 'InReview':
      if (atManager) return [task(1, 'ManagerApproval', manager)]
      return [task(1, 'ManagerApproval', manager, { by: MANAGER, comment: 'Looks reasonable.' }), task(2, 'FinanceApproval', finance)]
    case 'Approved':
      return [
        task(1, 'ManagerApproval', manager, { by: MANAGER }),
        task(2, 'FinanceApproval', finance, { by: FINANCE }),
        task(3, 'ProcurementFulfillment', { user: null, role: 'ProcurementOfficer' }),
      ]
    case 'Completed':
      return [task(1, 'ManagerApproval', manager, { by: MANAGER }), task(2, 'FinanceApproval', finance, { by: FINANCE })]
    case 'Rejected':
      return [{ ...task(1, 'ManagerApproval', manager, { by: MANAGER, comment: 'Not in this quarter’s budget.' }), outcome: 'Rejected' }]
    default:
      return status satisfies never
  }
}

/** 23 older requests from the mock employee, so lists have something to filter and page through. */
function buildSeeded(): RequestDetailsDto[] {
  return Array.from({ length: 23 }, (_, index) => {
    const status = SEEDED_STATUSES[index % SEEDED_STATUSES.length] ?? 'InReview'
    const submittedAt = new Date(Date.UTC(2026, 9, 28 - index, 9)).toISOString()
    const id = `5eed0000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`
    const base = {
      id,
      number: `FO-2026-${String(800 - index).padStart(6, '0')}`,
      requester: EMPLOYEE,
      department: MOCK_USERS.employee.department ?? { id: '', name: '' },
      status,
      stage: (status === 'InReview' ? (waitsForManager(index, status) ? 'ManagerApproval' : 'FinanceApproval') : status === 'Approved' ? 'ProcurementFulfillment' : 'Closed') as RequestStage,
      submittedAt,
      closedAt: status === 'Completed' || status === 'Rejected' ? submittedAt : null,
      tasks: seededTasks(id, status, submittedAt, waitsForManager(index, status)),
      availableActions: null,
      canComment: true,
    }
    return index % 3 !== 2
      ? {
          ...base,
          type: 'EquipmentPurchase' as const,
          title: ['Dell XPS 15 laptop', 'Monitor 27 inch', 'Standing desk', 'Noise-cancelling headset'][index % 4] ?? 'Laptop',
          purchase: {
            itemName: 'Equipment',
            description: 'Mock description',
            estimatedCost: { amount: `${(index + 1) * 100}.000`, currency: 'JOD' as const },
            justification: 'Mock justification',
          },
          leave: null,
        }
      : {
          ...base,
          type: 'Leave' as const,
          title: `Annual leave · ${index + 1} Dec`,
          purchase: null,
          leave: { startDate: '2026-12-01', endDate: '2026-12-03', days: 3, leaveType: 'Annual' as const, reason: 'Family trip', reasonHidden: false },
        }
  })
}

export function mockRequests(): readonly RequestDetailsDto[] {
  seeded = seeded.length > 0 ? seeded : buildSeeded()
  return [...created, ...seeded]
}

export interface MockRequestQuery {
  scope: 'mine' | 'visible'
  status: string | null
  type: string | null
  search: string | null
  page: number
  pageSize: number
}

function toSummary(request: RequestDetailsDto): RequestSummaryDto {
  const { purchase: _purchase, leave: _leave, tasks: _tasks, availableActions: _actions, canComment: _comment, ...summary } = request
  return summary
}

/** Filtering and paging as the contract describes (§4); visibility is simplified to "mine" or "everything". */
export function listMockRequests(user: CurrentUserDto, query: MockRequestQuery) {
  const needle = query.search?.trim().toLowerCase() ?? ''
  const matches = mockRequests()
    .filter((request) => query.scope === 'visible' || request.requester.id === user.id)
    .filter((request) => !query.status || request.status === query.status)
    .filter((request) => !query.type || request.type === query.type)
    .filter((request) => !needle || request.number.toLowerCase().startsWith(needle) || request.title.toLowerCase().includes(needle))
    .toSorted((a, b) => b.submittedAt.localeCompare(a.submittedAt))
  const start = (query.page - 1) * query.pageSize
  return {
    items: matches.slice(start, start + query.pageSize).map(toSummary),
    page: query.page,
    pageSize: query.pageSize,
    totalCount: matches.length,
  }
}

/** Stand-in for the server's visibility rule: your own, or anything if you work the queues. */
export function canSeeMockRequest(user: CurrentUserDto, request: RequestDetailsDto): boolean {
  return request.requester.id === user.id || user.capabilities.includes('tasks.view')
}

/** Stand-in for TaskEligibility: not your own request, and the task is assigned to you or your role. */
export function mockEligibleActions(user: CurrentUserDto, request: RequestDetailsDto, task: WorkTaskDto): TaskAction[] {
  if (task.status !== 'Open' || request.requester.id === user.id) return []
  const assignedToMe = task.assignee.user?.id === user.id || (task.assignee.role !== null && user.roles.includes(task.assignee.role))
  if (!assignedToMe) return []
  return task.kind === 'Fulfillment' ? ['fulfill'] : ['approve', 'reject']
}

export const mockTaskVersion = () => MOCK_VERSION

/** The request as the given user would receive it (reason hidden, actions computed). */
export function getMockRequestFor(user: CurrentUserDto, id: string): RequestDetailsDto | null {
  const request = mockRequests().find((candidate) => candidate.id === id)
  if (!request || !canSeeMockRequest(user, request)) return null
  const openTask = request.tasks.find((task) => task.status === 'Open')
  const actions = openTask ? mockEligibleActions(user, request, openTask) : []
  const mayReadReason = request.requester.id === user.id || user.roles.includes('HrOfficer') || user.id === MOCK_USERS.manager.id
  return {
    ...request,
    leave: request.leave && !mayReadReason ? { ...request.leave, reason: null, reasonHidden: true } : request.leave,
    availableActions: openTask && actions.length > 0 ? { taskId: openTask.id, actions, version: MOCK_VERSION } : null,
  }
}

const SUMMARY_DETAIL = (request: RequestDetailsDto) =>
  request.purchase ? formatMoney(request.purchase.estimatedCost) : request.leave ? `${request.leave.days} days · ${request.leave.leaveType}` : ''

/**
 * `GET /api/tasks`: Open = tasks the user could decide, oldest first; Done = tasks they decided,
 * newest first (docs/API_CONTRACT.md §5). Eligibility is the mock's stand-in for `TaskEligibility`.
 */
export function listMockInbox(user: CurrentUserDto, query: { status: 'Open' | 'Done'; page: number; pageSize: number }): PagedInboxItems {
  const rows = mockRequests().flatMap((request) =>
    request.tasks
      .filter((task) => (query.status === 'Open' ? mockEligibleActions(user, request, task).length > 0 : task.decidedBy?.id === user.id))
      .map((task): InboxItemDto => ({
        actions: mockEligibleActions(user, request, task),
        taskId: task.id,
        kind: task.kind,
        stage: task.stage,
        createdAt: task.createdAt,
        version: MOCK_VERSION,
        status: task.status,
        outcome: task.outcome,
        decidedAt: task.decidedAt,
        request: { ...toSummary(request), keyDetail: SUMMARY_DETAIL(request) },
      })),
  )
  const sorted = rows.toSorted((a, b) => (query.status === 'Open' ? a.createdAt.localeCompare(b.createdAt) : (b.decidedAt ?? '').localeCompare(a.decidedAt ?? '')))
  const start = (query.page - 1) * query.pageSize
  return { items: sorted.slice(start, start + query.pageSize), page: query.page, pageSize: query.pageSize, totalCount: sorted.length }
}

export function findMockTask(taskId: string): { request: RequestDetailsDto; task: WorkTaskDto } | null {
  for (const request of mockRequests()) {
    const task = request.tasks.find((candidate) => candidate.id === taskId)
    if (task) return { request, task }
  }
  return null
}

/** Replaces a request in whichever list holds it (used by the workflow mock). */
export function updateMockRequest(next: RequestDetailsDto) {
  created = created.map((request) => (request.id === next.id ? next : request))
  seeded = seeded.map((request) => (request.id === next.id ? next : request))
}

export function mockActivities(request: RequestDetailsDto): ActivityDto[] {
  const entry = (id: string, type: ActivityDto['type'], actor: UserRef | null, stage: RequestStage | null, body: string | null, at: string): ActivityDto => ({
    id,
    type,
    actor,
    stage,
    body,
    occurredAt: at,
  })
  const items: ActivityDto[] = [entry(`${request.id}-a0`, 'Submitted', request.requester, null, null, request.submittedAt)]
  for (const task of request.tasks) {
    items.push(entry(`${task.id}-assigned`, 'TaskAssigned', null, task.stage, null, task.createdAt))
    if (task.decidedAt && task.decidedBy) {
      const type = task.outcome === 'Rejected' ? 'Rejected' : task.outcome === 'Fulfilled' ? 'Fulfilled' : 'Approved'
      items.push(entry(`${task.id}-decided`, type, task.decidedBy, task.stage, task.comment, task.decidedAt))
    }
  }
  if (request.status === 'Completed' && request.closedAt) items.push(entry(`${request.id}-done`, 'Completed', null, null, null, request.closedAt))
  return [...items, ...(comments.get(request.id) ?? [])].toSorted((a, b) => a.occurredAt.localeCompare(b.occurredAt))
}

export function addMockComment(user: CurrentUserDto, request: RequestDetailsDto, body: string): ActivityDto {
  const comment: ActivityDto = {
    id: `${request.id}-c${(comments.get(request.id)?.length ?? 0) + 1}`,
    type: 'CommentAdded',
    actor: userRef(user),
    stage: null,
    body,
    occurredAt: new Date().toISOString(),
  }
  comments.set(request.id, [...(comments.get(request.id) ?? []), comment])
  return comment
}

function nextIdentity() {
  const index = created.length + 1
  return {
    id: `c0de0000-0000-4000-8000-${String(index).padStart(12, '0')}`,
    number: `FO-2026-${String(900 + index).padStart(6, '0')}`,
  }
}

type Header = Omit<RequestDetailsDto, 'purchase' | 'leave' | 'title' | 'type'>

function header(user: CurrentUserDto): Header {
  const identity = nextIdentity()
  const submittedAt = new Date().toISOString()
  return {
    ...identity,
    requester: userRef(user),
    // The caller checks the department before getting here.
    department: user.department ?? { id: '', name: '' },
    status: 'InReview',
    stage: 'ManagerApproval',
    submittedAt,
    closedAt: null,
    tasks: [mockTask({ id: `${identity.id}-t1`, stage: 'ManagerApproval', createdAt: submittedAt, assignee: { user: user.manager, role: null } })],
    availableActions: null,
    canComment: true,
  }
}

export function addMockPurchase(user: CurrentUserDto, body: SubmitPurchaseRequest): RequestDetailsDto {
  const request: RequestDetailsDto = {
    ...header(user),
    type: 'EquipmentPurchase',
    title: body.itemName,
    purchase: { ...body },
    leave: null,
  }
  created = [request, ...created]
  return request
}

export function addMockLeave(user: CurrentUserDto, body: SubmitLeaveRequest): RequestDetailsDto {
  const days = calendarDaysInclusive(body.startDate, body.endDate) ?? 1
  const request: RequestDetailsDto = {
    ...header(user),
    type: 'Leave',
    title: `${body.leaveType} leave · ${formatDateOnly(body.startDate)}`,
    purchase: null,
    leave: {
      startDate: body.startDate,
      endDate: body.endDate,
      days,
      leaveType: body.leaveType,
      reason: body.reason ?? null,
      reasonHidden: false,
    },
  }
  created = [request, ...created]
  return request
}

/** The dashboard as the server would build it for this user (counts and lists from the mock store). */
export function buildMockDashboard(user: CurrentUserDto): DashboardDto {
  const mine = mockRequests().filter((request) => request.requester.id === user.id)
  const count = (status: RequestStatus) => mine.filter((request) => request.status === status).length
  const sorted = (requests: readonly RequestDetailsDto[]) => requests.toSorted((a, b) => b.submittedAt.localeCompare(a.submittedAt))

  const workQueue = user.capabilities.includes('tasks.view')
  const open = mockRequests().flatMap((request) =>
    request.tasks.filter((task) => mockEligibleActions(user, request, task).length > 0),
  )
  const visible = sorted(mockRequests().filter((request) => canSeeMockRequest(user, request)))
  const recentActivity = visible
    .flatMap((request) => mockActivities(request).map((entry) => ({ ...entry, requestId: request.id, requestNumber: request.number })))
    .toSorted((a, b) => b.occurredAt.localeCompare(a.occurredAt))
    .slice(0, 10)

  return {
    myRequests: {
      inReview: count('InReview'),
      approved: count('Approved'),
      completed: count('Completed'),
      rejected: count('Rejected'),
      recent: sorted(mine).slice(0, 5).map(toSummary),
    },
    myTasks: workQueue
      ? {
          open: open.length,
          oldestOpenCreatedAt: open.map((task) => task.createdAt).toSorted()[0] ?? null,
          decidedLast7Days: 0,
        }
      : null,
    recentActivity,
    admin: user.capabilities.includes('admin.manage') ? { activeUsers: 5, departments: 3, stalledTasks: 2 } : null,
  }
}
