import type { QueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/lib/api/queryKeys'

/** Hub path and events: docs/API_CONTRACT.md §9. Server → client only. */
export const NOTIFICATIONS_HUB_PATH = '/hubs/notifications'

export const REALTIME_EVENTS = ['notificationCreated', 'requestChanged', 'tasksChanged'] as const
export type RealtimeEvent = (typeof REALTIME_EVENTS)[number]

/**
 * What a server event makes stale (API_CONTRACT §9, "Client reaction"). Payloads are hints:
 * only a request id is read from them, and nothing from a payload is ever rendered.
 */
export function invalidateForEvent(queryClient: QueryClient, event: RealtimeEvent, payload: unknown): void {
  const invalidate = (queryKey: readonly unknown[]) => void queryClient.invalidateQueries({ queryKey })
  switch (event) {
    case 'notificationCreated':
      invalidate(queryKeys.notifications.all)
      invalidate(queryKeys.dashboard)
      return
    case 'requestChanged': {
      const requestId = requestIdOf(payload)
      // Without a usable id every request view is suspect, so fall back to all of them.
      if (requestId === null) invalidate(queryKeys.requests.all)
      else {
        invalidate(queryKeys.requests.detail(requestId))
        invalidate(queryKeys.requests.activity(requestId))
        invalidate(queryKeys.requests.lists)
      }
      invalidate(queryKeys.dashboard)
      return
    }
    case 'tasksChanged':
      invalidate(queryKeys.tasks.all)
      invalidate(queryKeys.dashboard)
      return
    default:
      return event satisfies never
  }
}

/** After a (re)connect events may have been missed, so everything the events cover is refetched. */
export function invalidateAllRealtime(queryClient: QueryClient): void {
  for (const queryKey of [queryKeys.notifications.all, queryKeys.requests.all, queryKeys.tasks.all, queryKeys.dashboard]) {
    void queryClient.invalidateQueries({ queryKey })
  }
}

function requestIdOf(payload: unknown): string | null {
  if (typeof payload !== 'object' || payload === null || !('requestId' in payload)) return null
  return typeof payload.requestId === 'string' && payload.requestId !== '' ? payload.requestId : null
}
