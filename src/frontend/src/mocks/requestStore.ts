import type { CurrentUserDto, RequestDetailsDto, SubmitLeaveRequest, SubmitPurchaseRequest } from '@/lib/api/types'
import { calendarDaysInclusive, formatDateOnly } from '@/lib/format/date'

/**
 * In-memory requests for the mock API, reset between tests. Only what a mocked endpoint needs;
 * the real rules (numbering, routing, visibility) live in the backend.
 */
let created: RequestDetailsDto[] = []

export function resetMockRequests() {
  created = []
}

export function mockRequests(): readonly RequestDetailsDto[] {
  return created
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
