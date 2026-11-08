import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { ApiError, UNKNOWN_ERROR_CODE } from '@/lib/api/apiError'
import { api } from '@/lib/api/client'
import { queryKeys } from '@/lib/api/queryKeys'
import type { PagedNotifications, UnreadCountDto } from '@/lib/api/types'
import { useToast } from '@/components/ui/toastContext'

export type NotificationListParams = {
  unreadOnly: boolean
  page: number
  pageSize: number
}

/** `GET /api/notifications` (own only, newest first). Keeps the previous page visible while the next loads. */
export function useNotifications(params: NotificationListParams, enabled = true) {
  return useQuery({
    queryKey: queryKeys.notifications.list(params),
    queryFn: async () => {
      const { data } = await api.GET('/api/notifications', { params: { query: params } })
      if (!data) throw new ApiError({ status: 200, code: UNKNOWN_ERROR_CODE })
      return data
    },
    placeholderData: keepPreviousData,
    enabled,
  })
}

/** `GET /api/notifications/unread-count`: the bell's badge. */
export function useUnreadCount() {
  return useQuery({
    queryKey: queryKeys.notifications.unread,
    queryFn: async () => {
      const { data } = await api.GET('/api/notifications/unread-count')
      if (!data) throw new ApiError({ status: 200, code: UNKNOWN_ERROR_CODE })
      return data.count
    },
  })
}

type ListSnapshot = [readonly unknown[], PagedNotifications | undefined][]

/** Marks one notification read in every cached list and lowers the unread count — only if it was unread. */
function applyMarkRead(queryClient: QueryClient, id: string, readAt: string): { lists: ListSnapshot; unread: number | undefined } {
  const lists = queryClient.getQueriesData<PagedNotifications>({ queryKey: queryKeys.notifications.all })
    // The unread count shares the prefix but is a number, not a page.
    .filter(([, data]) => data !== undefined && typeof data === 'object' && 'items' in data)
  const unread = queryClient.getQueryData<UnreadCountDto['count']>(queryKeys.notifications.unread)
  const wasUnread = lists.some(([, data]) => data?.items.some((item) => item.id === id && item.readAt === null))
  for (const [key, data] of lists) {
    if (!data) continue
    queryClient.setQueryData<PagedNotifications>(key, {
      ...data,
      items: data.items.map((item) => (item.id === id && item.readAt === null ? { ...item, readAt } : item)),
    })
  }
  if (wasUnread && unread !== undefined) queryClient.setQueryData(queryKeys.notifications.unread, Math.max(0, unread - 1))
  return { lists, unread }
}

/**
 * `POST /api/notifications/{id}/read`, applied to the screen at once (the one optimistic update
 * the app allows). On failure the previous values are restored and the user is told; either way
 * the server's numbers are refetched.
 */
export function useMarkNotificationRead() {
  const queryClient = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: async (id: string) => {
      await api.POST('/api/notifications/{id}/read', { params: { path: { id } } })
    },
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.notifications.all })
      return applyMarkRead(queryClient, id, new Date().toISOString())
    },
    onError: (_error, _id, snapshot) => {
      if (!snapshot) return
      for (const [key, data] of snapshot.lists) queryClient.setQueryData(key, data)
      queryClient.setQueryData(queryKeys.notifications.unread, snapshot.unread)
      toast.error('Could not mark the notification as read. Please try again.')
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all }),
  })
}

/** `POST /api/notifications/read-all`. */
export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      await api.POST('/api/notifications/read-all')
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all }),
  })
}
