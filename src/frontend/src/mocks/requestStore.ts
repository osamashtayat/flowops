import type {
  CurrentUserDto,
  RequestDetailsDto,
  RequestStatus,
  RequestSummaryDto,
  SubmitLeaveRequest,
  SubmitPurchaseRequest,
} from '@/lib/api/types'
import { calendarDaysInclusive, formatDateOnly } from '@/lib/format/date'
import { MOCK_USERS } from './users'

/**
 * In-memory requests for the mock API, reset between tests. Only what a mocked endpoint needs;
 * the real rules (numbering, routing, visibility) live in the backend.
 */
let created: RequestDetailsDto[] = []

export function resetMockRequests() {
  created = []
}

/** 23 older requests from the mock employee, so lists have something to filter and page through. */
const SEEDED_STATUSES: RequestStatus[] = ['InReview', 'Approved', 'Completed', 'Rejected']
const SEEDED: RequestDetailsDto[] = Array.from({ length: 23 }, (_, index) => {
  const status = SEEDED_STATUSES[index % SEEDED_STATUSES.length] ?? 'InReview'
  const isPurchase = index % 3 !== 2
  const submittedAt = new Date(Date.UTC(2026, 9, 28 - index, 9)).toISOString()
  const base = {
    id: `5eed0000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
    number: `FO-2026-${String(800 - index).padStart(6, '0')}`,
    requester: { id: MOCK_USERS.employee.id, displayName: MOCK_USERS.employee.displayName },
    department: MOCK_USERS.employee.department ?? { id: '', name: '' },
    status,
    stage: status === 'InReview' ? 'FinanceApproval' : 'Closed',
    submittedAt,
    closedAt: status === 'InReview' ? null : submittedAt,
    tasks: [],
    availableActions: null,
    canComment: true,
  } satisfies Partial<RequestDetailsDto>
  return isPurchase
    ? {
        ...base,
        type: 'EquipmentPurchase',
        title: ['Dell XPS 15 laptop', 'Monitor 27 inch', 'Standing desk', 'Noise-cancelling headset'][index % 4] ?? 'Laptop',
        purchase: {
          itemName: 'Equipment',
          description: 'Mock description',
          estimatedCost: { amount: `${(index + 1) * 100}.000`, currency: 'JOD' },
          justification: 'Mock justification',
        },
        leave: null,
      }
    : {
        ...base,
        type: 'Leave',
        title: `Annual leave · ${index + 1} Dec`,
        purchase: null,
        leave: { startDate: '2026-12-01', endDate: '2026-12-03', days: 3, leaveType: 'Annual', reason: null, reasonHidden: false },
      }
})

export function mockRequests(): readonly RequestDetailsDto[] {
  return [...created, ...SEEDED]
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

function nextIdentity() {
  const index = created.length + 1
  return {
    id: `c0de0000-0000-4000-8000-${String(index).padStart(12, '0')}`,
    number: `FO-2026-${String(900 + index).padStart(6, '0')}`,
  }
}

type Header = Omit<RequestDetailsDto, 'purchase' | 'leave' | 'title' | 'type'>

function header(user: CurrentUserDto): Header {
  return {
    ...nextIdentity(),
    requester: { id: user.id, displayName: user.displayName },
    // The caller checks the department before getting here.
    department: user.department ?? { id: '', name: '' },
    status: 'InReview',
    stage: 'ManagerApproval',
    submittedAt: new Date().toISOString(),
    closedAt: null,
    tasks: [],
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
