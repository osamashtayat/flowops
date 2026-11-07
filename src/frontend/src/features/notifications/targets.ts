import type { NotificationDto } from '@/lib/api/types'

export const ROLE_CHANGES_PATH = '/admin/role-changes'

/**
 * Where clicking a notification goes. The role-change types carry neither a request nor a task id
 * (API_CONTRACT §6), so the type decides; everything else follows its ids. `null` = nowhere to go.
 */
export function notificationTarget({ type, requestId, taskId }: Pick<NotificationDto, 'type' | 'requestId' | 'taskId'>): string | null {
  switch (type) {
    case 'RoleChangePending':
    case 'RoleChangeApproved':
    case 'RoleChangeRejected':
      return ROLE_CHANGES_PATH
    case 'TaskAssigned':
    case 'StepApproved':
    case 'RequestApproved':
    case 'RequestRejected':
    case 'RequestCompleted':
    case 'CommentAdded':
      if (requestId) return `/requests/${requestId}`
      return taskId ? '/inbox' : null
    default:
      return type satisfies never
  }
}
