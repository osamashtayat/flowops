import type { NotificationDto, NotificationType, PagedNotifications } from '@/lib/api/types'

/**
 * In-memory notifications for the mock API (tests only; the dev server uses the real BE-13
 * endpoints). Keyed by user id; the employee starts with two unread, matching what the session
 * tests expect from the unread count.
 */
const EMPLOYEE_ID = '3f0c5a2e-0000-4000-8000-0000000000a1'
const ADMIN_ID = '3f0c5a2e-0000-4000-8000-0000000000a4'
export const MOCK_REQUEST_ID = '7b1e0000-0000-4000-8000-000000000001'

let counter = 0
function notification(type: NotificationType, title: string, body: string, minutesAgo: number, links: Partial<NotificationDto> = {}): NotificationDto {
  counter += 1
  return {
    id: `9a000000-0000-4000-8000-${String(counter).padStart(12, '0')}`,
    type,
    title,
    body,
    requestId: null,
    taskId: null,
    createdAt: new Date(Date.now() - minutesAgo * 60_000).toISOString(),
    readAt: null,
    ...links,
  }
}

function seed(): Map<string, NotificationDto[]> {
  counter = 0
  return new Map([
    [
      EMPLOYEE_ID,
      [
        notification('StepApproved', 'Step approved', 'Your manager approved FO-2026-0001.', 5, { requestId: MOCK_REQUEST_ID }),
        notification('RequestRejected', 'Request rejected', 'FO-2026-0002 was rejected.', 90, { requestId: '7b1e0000-0000-4000-8000-000000000002' }),
        { ...notification('RequestCompleted', 'Request completed', 'FO-2026-0003 is complete.', 3000, { requestId: '7b1e0000-0000-4000-8000-000000000003' }), readAt: new Date().toISOString() },
      ],
    ],
    [ADMIN_ID, [notification('RoleChangePending', 'Role change awaiting approval', 'A role change needs a second administrator.', 2)]],
  ])
}

let store = seed()

export function resetMockNotifications() {
  store = seed()
}

function mine(userId: string): NotificationDto[] {
  return store.get(userId) ?? []
}

export function listMockNotifications(userId: string, params: { unreadOnly: boolean; page: number; pageSize: number }): PagedNotifications {
  const all = mine(userId).filter((item) => !params.unreadOnly || item.readAt === null)
  const start = (params.page - 1) * params.pageSize
  return { items: all.slice(start, start + params.pageSize), page: params.page, pageSize: params.pageSize, totalCount: all.length }
}

export function countMockUnread(userId: string): number {
  return mine(userId).filter((item) => item.readAt === null).length
}

/** False when the id is not one of the user's (the real API answers 404). */
export function markMockRead(userId: string, id: string): boolean {
  const item = mine(userId).find((candidate) => candidate.id === id)
  if (!item) return false
  item.readAt ??= new Date().toISOString()
  return true
}

export function markAllMockRead(userId: string) {
  for (const item of mine(userId)) item.readAt ??= new Date().toISOString()
}
